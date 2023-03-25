// <copyright file="RedirectRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;

/// <summary>
/// Handles a request to redirect to a different resource.
/// </summary>
public class RedirectRequestHandler : HttpRequestHandler
{
    private readonly string redirectUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectRequestHandler"/> class.
    /// </summary>
    /// <param name="redirectUrl">The URL, relative to the root, to which to redirect the request.</param>
    public RedirectRequestHandler(string redirectUrl)
        : base(Array.Empty<byte>())
    {
        this.redirectUrl = redirectUrl;
    }

    /// <summary>
    /// Handles an HTTP request that redirects to another resource.
    /// </summary>
    /// <param name="request">The HTTP request to handle.</param>
    /// <returns>The response to the HTTP request.</returns>
    public override HttpResponse HandleRequest(HttpRequest request)
    {
        HttpResponse responseData = this.CreateHttpResponse(HttpStatusCode.MovedPermanently);
        responseData.Headers["Location"] = new List<string>() { this.redirectUrl };
        responseData.Headers["Content-Length"] = new List<string>() { "0" };
        responseData.BodyContent = Array.Empty<byte>();
        return responseData;
    }
}