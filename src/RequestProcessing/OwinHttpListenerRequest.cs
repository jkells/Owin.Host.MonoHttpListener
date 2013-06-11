﻿// <copyright file="OwinHttpListenerRequest.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using Mono.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Owin.Host.MonoHttpListener.RequestProcessing
{
    /// <summary>
    /// This wraps an HttpListenerRequest and exposes it as an OWIN environment IDictionary.
    /// </summary>
    internal class OwinHttpListenerRequest
    {
        private readonly CallEnvironment _environment;
        private readonly HttpListenerRequest _request;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwinHttpListenerRequest"/> class.
        /// Uses the given request object to populate the OWIN standard keys in the environment IDictionary.
        /// Most values are copied so that they can be mutable, but the headers collection is only wrapped.
        /// </summary>
        internal OwinHttpListenerRequest(HttpListenerRequest request, string basePath, string path, string query, CallEnvironment environment)
        {
            Contract.Requires(request != null);

            _request = request;
            _environment = environment;

            _environment.RequestProtocol = GetProtocol(request.ProtocolVersion);
            _environment.RequestScheme = request.Url.Scheme;
            _environment.RequestMethod = request.HttpMethod;
            _environment.RequestPathBase = basePath;
            _environment.RequestPath = path;
            _environment.RequestQueryString = query;

            _environment.RequestHeaders = new RequestHeadersDictionary(request);

            if (_request.IsSecureConnection)
            {
                // TODO: Add delay sync load for folks that directly access the client cert key
                _environment.LoadClientCert = (Func<Task>)LoadClientCertAsync;
            }
        }

        private static string GetProtocol(Version version)
        {
            if (version.Major == 1)
            {
                if (version.Minor == 1)
                {
                    return "HTTP/1.1";
                }
                else if (version.Minor == 0)
                {
                    return "HTTP/1.0";
                }
            }
            return "HTTP/" + version.ToString(2);
        }

        internal bool TryGetClientCert(ref X509Certificate value, ref Exception errors)
        {
            if (!_request.IsSecureConnection)
            {
                return false;
            }

            try
            {
                value = _request.GetClientCertificate();
                if (_request.ClientCertificateError != 0)
                {
                    errors = new Win32Exception(_request.ClientCertificateError);
                }
                return value != null;
            }
            catch (HttpListenerException)
            {
                // TODO: LOG
                return false;
            }
        }

        private Task LoadClientCertAsync()
        {
            try
            {
                if (!_environment.ClientCertNeedsInit)
                {
                    return TaskHelpers.Completed();
                }

                return _request.GetClientCertificateAsync()
                    .Then(cert =>
                    {
                        _environment.ClientCert = cert;
                        _environment.ClientCertErrors =
                            (_request.ClientCertificateError == 0) ? null
                            : new Win32Exception(_request.ClientCertificateError);
                    })
                    .Catch(errorInfo =>
                    {
                        _environment.ClientCert = null;
                        _environment.ClientCertErrors = null;
                        // TODO: LOG
                        return errorInfo.Handled();
                    });
            }
            catch (HttpListenerException)
            {
                _environment.ClientCert = null;
                _environment.ClientCertErrors = null;
                // TODO: LOG
                return TaskHelpers.Completed();
            }
        }

        internal Stream GetRequestBody()
        {
            return new HttpListenerStreamWrapper(_request.InputStream);
        }

        internal string GetRemoteIpAddress()
        {
            return _request.RemoteEndPoint.Address.ToString();
        }

        internal string GetRemotePort()
        {
            return _request.RemoteEndPoint.Port.ToString(CultureInfo.InvariantCulture);
        }

        internal string GetLocalIpAddress()
        {
            return _request.LocalEndPoint.Address.ToString();
        }

        internal string GetLocalPort()
        {
            return _request.LocalEndPoint.Port.ToString(CultureInfo.InvariantCulture);
        }

        internal bool GetIsLocal()
        {
            return _request.IsLocal;
        }
    }
}
