﻿// <copyright file="OwinWebSocketTests.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Owin.Host.MonoHttpListener.Tests
{
    public class OwinWebSocketTests
    {
        private static readonly string[] HttpServerAddress = new string[] { "http", "localhost", "8080", "/BaseAddress/" };
        private const string WsClientAddress = "ws://localhost:8080/BaseAddress/";

        [Fact]
        public async Task EndToEnd_ConnectAndClose_Success()
        {
            OwinHttpListener listener = CreateServer(env =>
            {
                var accept = (Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>)env["websocket.Accept"];
                Assert.NotNull(accept);

                accept(
                    null,
                    async wsEnv =>
                    {
                        var sendAsync1 = wsEnv.Get<Func<ArraySegment<byte>, int, bool, CancellationToken, Task>>("websocket.SendAsync");
                        var receiveAsync1 = wsEnv.Get<Func<ArraySegment<byte>, CancellationToken, Task<Tuple<int, bool, int>>>>("websocket.ReceiveAsync");
                        var closeAsync1 = wsEnv.Get<Func<int, string, CancellationToken, Task>>("websocket.CloseAsync");

                        var buffer1 = new ArraySegment<byte>(new byte[10]);
                        await receiveAsync1(buffer1, CancellationToken.None);
                        await closeAsync1((int)WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    });

                return TaskHelpers.Completed();
            },
                HttpServerAddress);

            using (listener)
            {
                using (var client = new ClientWebSocket())
                {
                    await client.ConnectAsync(new Uri(WsClientAddress), CancellationToken.None);

                    await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    WebSocketReceiveResult readResult = await client.ReceiveAsync(new ArraySegment<byte>(new byte[10]), CancellationToken.None);

                    Assert.Equal(WebSocketCloseStatus.NormalClosure, readResult.CloseStatus);
                    Assert.Equal("Closing", readResult.CloseStatusDescription);
                    Assert.Equal(0, readResult.Count);
                    Assert.True(readResult.EndOfMessage);
                    Assert.Equal(WebSocketMessageType.Close, readResult.MessageType);
                }
            }
        }

        [Fact]
        public async Task EndToEnd_EchoData_Success()
        {
            OwinHttpListener listener = CreateServer(env =>
            {
                var accept = (Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>)env["websocket.Accept"];
                Assert.NotNull(accept);

                accept(
                    null,
                    async wsEnv =>
                    {
                        var sendAsync = wsEnv.Get<Func<ArraySegment<byte>, int, bool, CancellationToken, Task>>("websocket.SendAsync");
                        var receiveAsync = wsEnv.Get<Func<ArraySegment<byte>, CancellationToken, Task<Tuple<int, bool, int>>>>("websocket.ReceiveAsync");
                        var closeAsync = wsEnv.Get<Func<int, string, CancellationToken, Task>>("websocket.CloseAsync");

                        var buffer = new ArraySegment<byte>(new byte[100]);
                        Tuple<int, bool, int> serverReceive = await receiveAsync(buffer, CancellationToken.None);
                        await sendAsync(new ArraySegment<byte>(buffer.Array, 0, serverReceive.Item3),
                            serverReceive.Item1, serverReceive.Item2, CancellationToken.None);
                        await closeAsync((int)WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    });

                return TaskHelpers.Completed();
            },
                HttpServerAddress);

            using (listener)
            {
                using (var client = new ClientWebSocket())
                {
                    await client.ConnectAsync(new Uri(WsClientAddress), CancellationToken.None);

                    byte[] sendBody = Encoding.UTF8.GetBytes("Hello World");
                    await client.SendAsync(new ArraySegment<byte>(sendBody), WebSocketMessageType.Text, true, CancellationToken.None);
                    var receiveBody = new byte[100];
                    WebSocketReceiveResult readResult = await client.ReceiveAsync(new ArraySegment<byte>(receiveBody), CancellationToken.None);

                    Assert.Equal(WebSocketMessageType.Text, readResult.MessageType);
                    Assert.True(readResult.EndOfMessage);
                    Assert.Equal(sendBody.Length, readResult.Count);
                    Assert.Equal("Hello World", Encoding.UTF8.GetString(receiveBody, 0, readResult.Count));
                }
            }
        }

        [Fact]
        public async Task SubProtocol_SelectLastSubProtocol_Success()
        {
            OwinHttpListener listener = CreateServer(env =>
            {
                var accept = (Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>)env["websocket.Accept"];
                Assert.NotNull(accept);

                var requestHeaders = env.Get<IDictionary<string, string[]>>("owin.RequestHeaders");
                var responseHeaders = env.Get<IDictionary<string, string[]>>("owin.ResponseHeaders");

                // Select the last sub-protocol from the client.
                string subProtocol = requestHeaders["Sec-WebSocket-Protocol"].Last().Split(',').Last().Trim();

                responseHeaders["Sec-WebSocket-Protocol"] = new string[] { subProtocol + "A" };

                accept(
                    new Dictionary<string, object>() { { "websocket.SubProtocol", subProtocol } },
                    async wsEnv =>
                    {
                        var sendAsync = wsEnv.Get<Func<ArraySegment<byte>, int, bool, CancellationToken, Task>>("websocket.SendAsync");
                        var receiveAsync = wsEnv.Get<Func<ArraySegment<byte>, CancellationToken, Task<Tuple<int, bool, int>>>>("websocket.ReceiveAsync");
                        var closeAsync = wsEnv.Get<Func<int, string, CancellationToken, Task>>("websocket.CloseAsync");

                        var buffer = new ArraySegment<byte>(new byte[100]);
                        Tuple<int, bool, int> serverReceive = await receiveAsync(buffer, CancellationToken.None);
                        // Assume close received
                        await closeAsync((int)WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    });

                return TaskHelpers.Completed();
            },
                HttpServerAddress);

            using (listener)
            {
                using (var client = new ClientWebSocket())
                {
                    client.Options.AddSubProtocol("protocol1");
                    client.Options.AddSubProtocol("protocol2");

                    await client.ConnectAsync(new Uri(WsClientAddress), CancellationToken.None);

                    await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    var receiveBody = new byte[100];
                    WebSocketReceiveResult readResult = await client.ReceiveAsync(new ArraySegment<byte>(receiveBody), CancellationToken.None);
                    Assert.Equal(WebSocketMessageType.Close, readResult.MessageType);
                    Assert.Equal("protocol2", client.SubProtocol);
                }
            }
        }

        private OwinHttpListener CreateServer(Func<IDictionary<string, object>, Task> app, string[] addressParts)
        {
            var wrapper = new OwinHttpListener();
            wrapper.Start(wrapper.Listener, app, CreateAddress(addressParts), null);
            return wrapper;
        }

        private static IList<IDictionary<string, object>> CreateAddress(string[] addressParts)
        {
            var address = new Dictionary<string, object>();
            address["scheme"] = addressParts[0];
            address["host"] = addressParts[1];
            address["port"] = addressParts[2];
            address["path"] = addressParts[3];

            IList<IDictionary<string, object>> list = new List<IDictionary<string, object>>();
            list.Add(address);
            return list;
        }
    }
}
