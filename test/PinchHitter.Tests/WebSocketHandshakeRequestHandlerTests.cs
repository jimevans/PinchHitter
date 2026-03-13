// <copyright file="WebSocketHandshakeRequestHandlerTests.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Threading.Tasks;

[TestFixture]
public class WebSocketHandshakeRequestHandlerTests
{
    [Test]
    public async Task TestHandlerReturnsExpectedHandshakeResponse()
    {
        // Use the RFC 6455 Section 1.3 canonical key/accept pair to verify the
        // handler applies the correct algorithm: Base64(SHA1(key + RFC_GUID)).
        const string websocketKey = "dGhlIHNhbXBsZSBub25jZQ==";
        const string expectedAcceptValue = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        request.Headers["Connection"] = new List<string>() { "Upgrade" };
        request.Headers["Upgrade"] = new List<string>() { "websocket" };
        request.Headers["Sec-WebSocket-Key"] = new List<string>() { websocketKey };

        WebSocketHandshakeRequestHandler handler = new();
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SwitchingProtocols));
            Assert.That(response.Headers, Contains.Key("Connection"));
            Assert.That(response.Headers["Connection"][0], Is.EqualTo("Upgrade"));
            Assert.That(response.Headers, Contains.Key("Upgrade"));
            Assert.That(response.Headers["Upgrade"][0], Is.EqualTo("websocket"));
            Assert.That(response.Headers, Contains.Key("Sec-WebSocket-Accept"));
            Assert.That(response.Headers["Sec-WebSocket-Accept"][0], Is.EqualTo(expectedAcceptValue));
        });
    }
}
