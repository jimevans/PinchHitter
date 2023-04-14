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
    private readonly Dictionary<string, Dictionary<HttpMethod, HttpRequestHandler>> handlers = new();
    private readonly NotFoundRequestHandler notFoundHandler = new(WebContent.AsHtmlDocument("<h1>404 Not Found</h1><div>The requested resource was not found</div>"));
    private readonly BadRequestHandler invalidRequestHandler = new(WebContent.AsHtmlDocument("<h1>400 Invalid Request</h1><div>The authorization request was incorrect</div>"));
    private readonly MethodNotAllowedRequestHandler methodNotAllowedHandler = new(WebContent.AsHtmlDocument("<h1>405 Method Not Allowed</h1><div>The requested URL does not support the requested method</div>"));

    /// <summary>
    /// Process an HTTP request, returning a response.
    /// </summary>
    /// <param name="connectionId">The ID of the connection from which the received request is to be processed.</param>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    public virtual HttpResponse ProcessRequest(string connectionId, HttpRequest request)
    {
        HttpResponse responseData;
        if (request.Uri is null)
        {
            responseData = this.invalidRequestHandler.HandleRequest(connectionId, request);
        }
        else
        {
            if (request.IsWebSocketHandshakeRequest)
            {
                responseData = new WebSocketHandshakeRequestHandler().HandleRequest(connectionId, request);
            }
            else
            {
                if (!this.handlers.ContainsKey(request.Uri.AbsolutePath))
                {
                    responseData = this.notFoundHandler.HandleRequest(connectionId, request);
                }
                else
                {
                    if (!this.handlers[request.Uri.AbsolutePath].ContainsKey(request.Method))
                    {
                        responseData = this.methodNotAllowedHandler.HandleRequest(connectionId, request, this.handlers[request.Uri.AbsolutePath].Keys.ToList());
                    }
                    else
                    {
                        responseData = this.handlers[request.Uri.AbsolutePath][request.Method].HandleRequest(connectionId, request);
                    }
                }
            }
        }

        return responseData;
    }

    /// <summary>
    /// Registers a request handler with the processor for the HTTP GET method.
    /// </summary>
    /// <param name="url">The URL, relative from the root, for which the handler handles requests.</param>
    /// <param name="handler">The handler to register.</param>
    public virtual void RegisterHandler(string url, HttpRequestHandler handler)
    {
        this.RegisterHandler(url, HttpMethod.Get, handler);
    }

    /// <summary>
    /// Registers a request handler with the processor.
    /// </summary>
    /// <param name="url">The URL, relative from the root, for which the handler handles requests.</param>
    /// <param name="method">The HTTP method for which the handler handles requests.</param>
    /// <param name="handler">The handler to register.</param>
    public virtual void RegisterHandler(string url, HttpMethod method, HttpRequestHandler handler)
    {
        if (!this.handlers.ContainsKey(url))
        {
            this.handlers[url] = new Dictionary<HttpMethod, HttpRequestHandler>();
        }

        this.handlers[url][method] = handler;
    }
}