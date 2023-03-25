// <copyright file="HttpRequestProcessor.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// A processor for managing HTTP requests, including generating responses
/// based on registered resource.
/// </summary>
public class HttpRequestProcessor
{
    private readonly Dictionary<string, HttpRequestHandler> handlers = new();
    private readonly NotFoundRequestHandler notFoundHandler = new(WebContent.AsHtmlDocument("<h1>404 Not Found</h1><div>The requested resource was not found</div>"));
    private readonly BadRequestHandler invalidRequestHandler = new(WebContent.AsHtmlDocument("<h1>400 Invalid request</h1><div>The authorization request was incorrect</div>"));

    /// <summary>
    /// Process an HTTP request, returning a response.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    public virtual HttpResponse ProcessRequest(HttpRequest request)
    {
        HttpResponse responseData;
        if (request.Uri is null)
        {
            responseData = this.invalidRequestHandler.HandleRequest(request);
        }
        else
        {
            if (request.IsWebSocketHandshakeRequest)
            {
                responseData = new WebSocketHandshakeRequestHandler().HandleRequest(request);
            }
            else
            {
                if (!this.handlers.ContainsKey(request.Uri.AbsolutePath))
                {
                    responseData = this.notFoundHandler.HandleRequest(request);
                }
                else
                {
                    responseData = this.handlers[request.Uri.AbsolutePath].HandleRequest(request);
                }
            }
        }

        return responseData;
    }

    /// <summary>
    /// Registers a request handler with the processor.
    /// </summary>
    /// <param name="url">The URL, relative from the root, for which the handler handles requests.</param>
    /// <param name="handler">The handler to register.</param>
    public virtual void RegisterHandler(string url, HttpRequestHandler handler)
    {
        this.handlers[url] = handler;
    }
}