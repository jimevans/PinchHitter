// <copyright file="NotFoundRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Text;

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
        : base(Encoding.UTF8.GetBytes(content))
    {
    }

    /// <summary>
    /// Process an HTTP request where the requested resource is not found.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    protected override Task<HttpResponse> ProcessRequestAsync(HttpRequest request, params object[] additionalData)
    {
        return Task.FromResult<HttpResponse>(this.CreateHttpResponse(request.Id, HttpStatusCode.NotFound));
    }
}