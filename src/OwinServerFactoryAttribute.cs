﻿// <copyright file="OwinServerFactoryAttribute.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Owin.Host.MonoHttpListener;

[assembly: OwinServerFactory]

namespace Owin.Host.MonoHttpListener
{
    /// <summary>
    /// Implements the Katana setup pattern for the OwinHttpListener server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class OwinServerFactoryAttribute : Attribute
    {
        /// <summary>
        /// Advertise the capabilities of the server.
        /// </summary>
        /// <param name="properties"></param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed by server later.")]
        public static void Initialize(IDictionary<string, object> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            properties[Constants.VersionKey] = Constants.OwinVersion;

            IDictionary<string, object> capabilities =
                properties.Get<IDictionary<string, object>>(Constants.ServerCapabilitiesKey)
                    ?? new Dictionary<string, object>();
            properties[Constants.ServerCapabilitiesKey] = capabilities;

            capabilities[Constants.ServerNameKey] = Constants.ServerName;
            capabilities[Constants.ServerVersionKey] = Constants.ServerVersion;

            DetectWebSocketSupport(properties);

            // Let users set advanced configurations directly.
            var wrapper = new OwinHttpListener();
            properties[typeof(OwinHttpListener).FullName] = wrapper;
            properties[typeof(Mono.Net.HttpListener).FullName] = wrapper.Listener;
        }

        private static void DetectWebSocketSupport(IDictionary<string, object> properties)
        {
            // There is no explicit API to detect server side websockets, just check for v4.5 / Win8.
            // Per request we can provide actual verification.
            if (Environment.OSVersion.Version >= new Version(6, 2))
            {
                var capabilities = properties.Get<IDictionary<string, object>>(Constants.ServerCapabilitiesKey);
                capabilities[Constants.WebSocketVersionKey] = Constants.WebSocketVersion;
            }
            else
            {
                // TODO: Trace
            }
        }

        /// <summary>
        /// Creates an OwinHttpListener and starts listening on the given URL.
        /// </summary>
        /// <param name="app">The application entry point.</param>
        /// <param name="properties">The addresses to listen on.</param>
        /// <returns>The OwinHttpListener.  Invoke Dispose to shut down.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed by caller")]
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "By design")]
        public static IDisposable Create(Func<IDictionary<string, object>, Task> app, IDictionary<string, object> properties)
        {
            if (app == null)
            {
                throw new ArgumentNullException("app");
            }

            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            // Retrieve the instances created in Initialize
            OwinHttpListener wrapper = properties.Get<OwinHttpListener>(typeof(OwinHttpListener).FullName)
                ?? new OwinHttpListener();
            Mono.Net.HttpListener listener = properties.Get<Mono.Net.HttpListener>(typeof(Mono.Net.HttpListener).FullName)
                ?? new Mono.Net.HttpListener();

            IList<IDictionary<string, object>> addresses = properties.Get<IList<IDictionary<string, object>>>("host.Addresses")
                ?? new List<IDictionary<string, object>>();

            IDictionary<string, object> capabilities =
                properties.Get<IDictionary<string, object>>(Constants.ServerCapabilitiesKey)
                    ?? new Dictionary<string, object>();

            wrapper.Start(listener, app, addresses, capabilities);
            return wrapper;
        }
    }
}
