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
        : base(string.Empty)
    {
        this.redirectUrl = redirectUrl;
    }

    /// <summary>
    /// Handles an HTTP request that redirects to another resource.
    /// </summary>
    /// <param name="request">The HTTP request to handle.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>The response to the HTTP request.</returns>
    protected override HttpResponse ProcessRequest(HttpRequest request, params object[] additionalData)
    {
        HttpResponse responseData = this.CreateHttpResponse(request.Id, HttpStatusCode.MovedPermanently);
        responseData.Headers["Location"] = new List<string>() { this.redirectUrl };
        responseData.Headers["Content-Length"] = new List<string>() { "0" };
        return responseData;
    }
}