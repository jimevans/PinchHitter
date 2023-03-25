// <copyright file="WebResourceRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;

/// <summary>
/// Handles HTTP requests for a web resource where the requested resource is valid and successfully returned.
/// </summary>
public class WebResourceRequestHandler : HttpRequestHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebResourceRequestHandler"/> class.
    /// </summary>
    /// <param name="content">The content of the response.</param>
    public WebResourceRequestHandler(string content)
        : base(content)
    {
    }

    /// <summary>
    /// Process an HTTP request where the requested resource is valid and successfully returned.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    public override HttpResponse HandleRequest(HttpRequest request)
    {
        return this.CreateHttpResponse(HttpStatusCode.OK);
    }
}