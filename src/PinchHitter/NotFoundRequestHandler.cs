// <copyright file="NotFoundRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;

/// <summary>
/// Handles requests where the requested resource is not found.
/// </summary>
public class NotFoundRequestHandler : HttpRequestHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundRequestHandler"/> class.
    /// </summary>
    /// <param name="content">The content of the Not Found page to be served.</param>
    public NotFoundRequestHandler(string content)
        : base(content)
    {
    }

    /// <summary>
    /// Process an HTTP request where the requested resource is not found.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    public override HttpResponse HandleRequest(HttpRequest request)
    {
        return this.CreateHttpResponse(HttpStatusCode.NotFound);
    }
}