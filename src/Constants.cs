﻿// <copyright file="Constants.cs" company="Microsoft Open Technologies, Inc.">
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

namespace Owin.Host.MonoHttpListener
{
    internal static class Constants
    {
        internal const string VersionKey = "owin.Version";
        internal const string OwinVersion = "1.0";
        internal const string CallCancelledKey = "owin.CallCancelled";

        internal const string ServerCapabilitiesKey = "server.Capabilities";
        internal const string ServerNameKey = "server.Name";
        internal const string ServerVersionKey = "mshttplistener.AdapterVersion";

        internal const string RequestBodyKey = "owin.RequestBody";
        internal const string RequestHeadersKey = "owin.RequestHeaders";
        internal const string RequestSchemeKey = "owin.RequestScheme";
        internal const string RequestMethodKey = "owin.RequestMethod";
        internal const string RequestPathBaseKey = "owin.RequestPathBase";
        internal const string RequestPathKey = "owin.RequestPath";
        internal const string RequestQueryStringKey = "owin.RequestQueryString";
        internal const string HttpRequestProtocolKey = "owin.RequestProtocol";
        internal const string HttpResponseProtocolKey = "owin.ResponseProtocol";

        internal const string ResponseStatusCodeKey = "owin.ResponseStatusCode";
        internal const string ResponseReasonPhraseKey = "owin.ResponseReasonPhrase";
        internal const string ResponseHeadersKey = "owin.ResponseHeaders";
        internal const string ResponseBodyKey = "owin.ResponseBody";

        internal const string ClientCertifiateKey = "ssl.ClientCertificate";
        internal const string LoadClientCertAsyncKey = "ssl.LoadClientCertAsync";

        internal const string RemoteIpAddressKey = "server.RemoteIpAddress";
        internal const string RemotePortKey = "server.RemotePort";
        internal const string LocalIpAddressKey = "server.LocalIpAddress";
        internal const string LocalPortKey = "server.LocalPort";
        internal const string IsLocalKey = "server.IsLocal";
        internal const string ServerOnSendingHeadersKey = "server.OnSendingHeaders";
        internal const string ServerUserKey = "server.User";

        internal const string WebSocketVersionKey = "websocket.Version";
        internal const string WebSocketVersion = "1.0";
        internal const string WebSocketAcceptKey = "websocket.Accept";
        internal const string WebSocketSubProtocolKey = "websocket.SubProtocol";

        internal const string HostHeader = "Host";
        internal const string WwwAuthenticateHeader = "WWW-Authenticate";
        internal const string ContentLengthHeader = "Content-Length";
        internal const string TransferEncodingHeader = "Transfer-Encoding";
        internal const string KeepAliveHeader = "Keep-Alive";
        internal const string ConnectionHeader = "Connection";
        internal const string SecWebSocketProtocol = "Sec-WebSocket-Protocol";

        internal const string SetPumpLimitsKey = "mshttplistener.SetPumpLimits";

        internal const int ErrorConnectionNoLongerValid = 1229;

#if NET40
        internal static readonly string ServerName = "HttpListener .NET 4.0, Microsoft.Owin.Host.HttpListener " + typeof(Constants).Assembly.GetName().Version.ToString();
#else
        internal static readonly string ServerName = "HttpListener .NET 4.5, Microsoft.Owin.Host.HttpListener " + typeof(Constants).Assembly.GetName().Version.ToString();
#endif
        internal static readonly string ServerVersion = typeof(Constants).Assembly.GetName().Version.ToString();
    }
}
