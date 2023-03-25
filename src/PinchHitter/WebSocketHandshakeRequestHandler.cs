// <copyright file="WebSocketHandshakeRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Handles a request to upgrade an HTTP connection to use a WebSocket.
/// </summary>
public class WebSocketHandshakeRequestHandler : HttpRequestHandler
{
    // A special GUID used in the WebSocket handshake for upgrading an HTTP
    // connection to use the WebSocket protocol. Specified by RFC 6455.
    private static readonly string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketHandshakeRequestHandler"/> class.
    /// </summary>
    public WebSocketHandshakeRequestHandler()
        : base(string.Empty)
    {
    }

    /// <summary>
    /// Handles an HTTP request to upgrade the connection to use a WebSocket.
    /// </summary>
    /// <param name="request">The HTTP request to handle.</param>
    /// <returns>The response to the HTTP request.</returns>
    public override HttpResponse HandleRequest(HttpRequest request)
    {
        string websocketSecureKey = request.Headers["Sec-WebSocket-Key"][0];

        // 1. Obtain the passed-in value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
        // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
        // 3. Compute SHA-1 and Base64 hash of the new value
        // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
        byte[] websocketSecureResponseBytes = Encoding.UTF8.GetBytes($"{websocketSecureKey.Trim()}{WebSocketGuid}");
        byte[] websocketResponseHash = SHA1.Create().ComputeHash(websocketSecureResponseBytes);
        string websocketAcceptResponseHash = Convert.ToBase64String(websocketResponseHash);

        HttpResponse response = this.CreateHttpResponse(HttpStatusCode.SwitchingProtocols);
        response.Headers["Connection"] = new List<string>() { "Upgrade" };
        response.Headers["Upgrade"] = new List<string>() { "websocket" };
        response.Headers["Sec-WebSocket-Accept"] = new List<string>() { websocketAcceptResponseHash };
        return response;
    }
}