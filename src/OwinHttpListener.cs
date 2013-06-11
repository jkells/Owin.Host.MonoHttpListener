﻿// <copyright file="OwinHttpListener.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Diagnostics.Contracts;
using Mono.Net;
using System.Threading;
using System.Threading.Tasks;
using Owin.Host.MonoHttpListener.RequestProcessing;

namespace Owin.Host.MonoHttpListener
{
    /// <summary>
    /// This wraps HttpListener and exposes it as an OWIN compatible server.
    /// </summary>
    public sealed class OwinHttpListener : IDisposable
    {
        private static readonly int DefaultMaxAccepts = 10 * Environment.ProcessorCount;
        private static readonly int DefaultMaxRequests = 100 * Environment.ProcessorCount;

        private Mono.Net.HttpListener _listener;
        private IList<string> _basePaths;
        private Func<IDictionary<string, object>, Task> _appFunc;
        private DisconnectHandler _disconnectHandler;
        private IDictionary<string, object> _capabilities;
        private PumpLimits _pumpLimits;
        private int _currentOutstandingAccepts;
        private int _currentOutstandingRequests;

        /// <summary>
        /// Creates a listener wrapper that can be configured by the user before starting.
        /// </summary>
        internal OwinHttpListener()
        {
            _listener = new Mono.Net.HttpListener();
            SetPumpLimits(DefaultMaxAccepts, DefaultMaxRequests);
        }

        /// <summary>
        /// The HttpListener instance wrapped by this wrapper.
        /// </summary>
        public Mono.Net.HttpListener Listener
        {
            get { return _listener; }
        }

        private bool CanAcceptMoreRequests
        {
            get
            {
                PumpLimits limits = _pumpLimits;
                return (_currentOutstandingAccepts < limits.MaxOutstandingAccepts
                    && _currentOutstandingRequests < limits.MaxOutstandingRequests - _currentOutstandingAccepts);
            }
        }

        /// <summary>
        /// These are merged as one call because they should be swapped out atomically.
        /// This controls how many requests the server attempts to process concurrently.
        /// </summary>
        /// <param name="maxAccepts">The maximum number of pending request receives.</param>
        /// <param name="maxRequests">The maximum number of active requests being processed.</param>
        public void SetPumpLimits(int maxAccepts, int maxRequests)
        {
            _pumpLimits = new PumpLimits(maxAccepts, maxRequests);

            if (_listener.IsListening)
            {
                // Kick the pump in case we went from zero to non-zero limits.
                OffloadStartNextRequest();
            }
        }

        /// <summary>
        /// Starts the listener and request processing threads.
        /// </summary>
        internal void Start(Mono.Net.HttpListener listener, Func<IDictionary<string, object>, Task> appFunc, IList<IDictionary<string, object>> addresses,
            IDictionary<string, object> capabilities)
        {
            Contract.Assert(_appFunc == null); // Start should only be called once
            Contract.Assert(listener != null);
            Contract.Assert(appFunc != null);
            Contract.Assert(addresses != null);

            _listener = listener;
            _appFunc = appFunc;

            _basePaths = new List<string>();

            foreach (var address in addresses)
            {
                // build url from parts
                string scheme = address.Get<string>("scheme") ?? Uri.UriSchemeHttp;
                string host = address.Get<string>("host") ?? "localhost";
                string port = address.Get<string>("port") ?? "5000";
                string path = address.Get<string>("path") ?? string.Empty;

                // if port is present, add delimiter to value before concatenation
                if (!string.IsNullOrWhiteSpace(port))
                {
                    port = ":" + port;
                }

                // Assume http(s)://+:9090/BasePath/, including the first path slash.  May be empty. Must end with a slash.
                if (!path.EndsWith("/", StringComparison.Ordinal))
                {
                    // Http.Sys requires that the URL end in a slash
                    path += "/";
                }
                _basePaths.Add(path);

                // add a server for each url
                string url = scheme + "://" + host + port + path;
                _listener.Prefixes.Add(url);
            }

            _capabilities = capabilities;
            _disconnectHandler = new DisconnectHandler(_listener);

            if (!_listener.IsListening)
            {
                _listener.Start();
                _disconnectHandler.Initialize();
            }

            OffloadStartNextRequest();
        }

        private void OffloadStartNextRequest()
        {
            if (_listener.IsListening && CanAcceptMoreRequests)
            {
                Task.Factory.StartNew(StartNextRequestAsync)
                    .Catch(errorInfo =>
                    {
                        // StartNextRequestAsync should handle it's own exceptions.
                        Contract.Assert(false, "Un-expected exception path: " + errorInfo.Exception.ToString());
                        System.Diagnostics.Debugger.Break();
                        return errorInfo.Throw();
                    });
            }
        }

        private void StartNextRequestAsync()
        {
            if (!_listener.IsListening || !CanAcceptMoreRequests)
            {
                return;
            }

            Interlocked.Increment(ref _currentOutstandingAccepts);

            try
            {
                _listener.GetContextAsync()
                    .Then((Action<HttpListenerContext>)StartProcessingRequest, runSynchronously: true)
                    .Catch(HandleAcceptError);
            }
            catch (ApplicationException ae)
            {
                // These come from the thread pool if HttpListener tries to call BindHandle after the listener has been disposed.
                HandleAcceptError(ae);
            }
            catch (HttpListenerException hle)
            {
                // These happen if HttpListener has been disposed
                HandleAcceptError(hle);
            }
        }

        private CatchInfoBase<Task>.CatchResult HandleAcceptError(CatchInfo errorInfo)
        {
            HandleAcceptError(errorInfo.Exception);
            return errorInfo.Handled();
        }

        private void HandleAcceptError(Exception ex)
        {
            Interlocked.Decrement(ref _currentOutstandingAccepts);
            // TODO: Log?
            System.Diagnostics.Debug.Write(ex);
            // Listener is disposed, but HttpListener.IsListening is not updated until the end of HttpListener.Dispose().
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is logged")]
        private void StartProcessingRequest(HttpListenerContext context)
        {
            Interlocked.Decrement(ref _currentOutstandingAccepts);
            Interlocked.Increment(ref _currentOutstandingRequests);
            OffloadStartNextRequest();
            OwinHttpListenerContext owinContext = null;

            try
            {
                string pathBase, path, query;
                GetPathAndQuery(context.Request.RawUrl, out pathBase, out path, out query);
                owinContext = new OwinHttpListenerContext(context, pathBase, path, query, _disconnectHandler);
                PopulateServerKeys(owinContext.Environment);
                Contract.Assert(!owinContext.Environment.IsExtraDictionaryCreated,
                    "All keys set by the server should have reserved slots.");

                _appFunc(owinContext.Environment)
                    .Then((Func<Task>)owinContext.Response.CompleteResponseAsync, runSynchronously: true)
                    .Then(() =>
                    {
                        owinContext.Response.Close();
                        EndRequest(owinContext, null);
                    }, runSynchronously: true)
                    .Catch(errorInfo =>
                    {
                        EndRequest(owinContext, errorInfo.Exception);
                        return errorInfo.Handled();
                    });
            }
            catch (Exception ex)
            {
                // TODO: Katana#5 - Don't catch everything, only catch what we think we can handle?  Otherwise crash the process.
                EndRequest(owinContext, ex);
            }
        }

        private void EndRequest(OwinHttpListenerContext owinContext, Exception ex)
        {
            // TODO: Log the exception, if any
            Interlocked.Decrement(ref _currentOutstandingRequests);
            if (owinContext != null)
            {
                owinContext.End(ex);
                owinContext.Dispose();
            }
            // Make sure we start the next request on a new thread, need to prevent stack overflows.
            OffloadStartNextRequest();
        }

        // When the server is listening on multiple urls, we need to decide which one is the correct base path for this request.
        private void GetPathAndQuery(string rawUrl, out string pathBase, out string path, out string query)
        {
            // Starting with the full url or just a path, extract the path and query.  There must never be a fragment.
            // http://host:port/path?query or /path?query
            string rawPathAndQuery;
            if (rawUrl.StartsWith("/", StringComparison.Ordinal))
            {
                rawPathAndQuery = rawUrl;
            }
            else
            {
                rawPathAndQuery = rawUrl.Substring(rawUrl.IndexOf('/', "https://x".Length));
            }

            if (rawPathAndQuery.Equals("/", StringComparison.Ordinal))
            {
                pathBase = string.Empty;
                path = "/";
                query = string.Empty;
                return;
            }

            // Split off the query
            string unescapedPath;
            int queryIndex = rawPathAndQuery.IndexOf('?');
            if (queryIndex < 0)
            {
                unescapedPath = Uri.UnescapeDataString(rawPathAndQuery);
                query = string.Empty;
            }
            else
            {
                unescapedPath = Uri.UnescapeDataString(rawPathAndQuery.Substring(0, queryIndex));
                query = rawPathAndQuery.Substring(queryIndex + 1); // Leave off the '?'
            }

            // Find the split between path and pathBase.
            // This will only do full segment path matching because all _basePaths end in a '/'.
            string bestMatch = "/";
            for (int i = 0; i < _basePaths.Count; i++)
            {
                string pathTest = _basePaths[i];
                if (unescapedPath.StartsWith(pathTest, StringComparison.OrdinalIgnoreCase)
                    && pathTest.Length > bestMatch.Length)
                {
                    bestMatch = pathTest;
                }
            }

            // pathBase must be empty or start with a slash and not end with a slash (/pathBase)
            // path must start with a slash (/path)
            // Move the matched '/' from the end of the pathBase to the start of the path.
            pathBase = bestMatch.Substring(0, bestMatch.Length - 1);
            path = unescapedPath.Substring(bestMatch.Length - 1);
        }

        private void PopulateServerKeys(CallEnvironment env)
        {
            env.ServerCapabilities = _capabilities;
            env.Listener = _listener;
            env.OwinHttpListener = this;
        }

        internal void Stop()
        {
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Shuts down the listener and disposes it.
        /// </summary>
        public void Dispose()
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            ((IDisposable)_listener).Dispose();
        }
    }
}
