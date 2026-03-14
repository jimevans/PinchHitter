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
    private readonly HttpStatusCode statusCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectRequestHandler"/> class.
    /// </summary>
    /// <param name="redirectUrl">The URL, relative to the root, to which to redirect the request.</param>
    /// <param name="statusCode">The HTTP status code for the redirect.</param>
    public RedirectRequestHandler(string redirectUrl, HttpStatusCode statusCode = HttpStatusCode.MovedPermanently)
        : base(""u8.ToArray())
    {
        this.redirectUrl = redirectUrl;
        this.statusCode = statusCode;
    }

    /// <summary>
    /// Handles an HTTP request that redirects to another resource.
    /// </summary>
    /// <param name="request">The HTTP request to handle.</param>
    /// <returns>The response to the HTTP request.</returns>
    protected override Task<HttpResponse> ProcessRequestAsync(HttpRequest request)
    {
        HttpResponse responseData = this.CreateHttpResponse(request.Id, this.statusCode);
        responseData.Headers["Location"] = new List<string>() { this.redirectUrl };
        responseData.Headers["Content-Length"] = new List<string>() { "0" };
        return Task.FromResult<HttpResponse>(responseData);
    }
}
