// <copyright file="WebResourceRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Text;

/// <summary>
/// Handles HTTP requests for a web resource where the requested resource is valid and successfully returned.
/// </summary>
public class WebResourceRequestHandler : HttpRequestHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebResourceRequestHandler"/> class.
    /// </summary>
    /// <param name="content">A string containing the content of the response.</param>
    public WebResourceRequestHandler(string content)
        : this(Encoding.UTF8.GetBytes(content))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebResourceRequestHandler"/> class.
    /// </summary>
    /// <param name="content">The byte array containing the content of the response.</param>
    public WebResourceRequestHandler(byte[] content)
        : base(content)
    {
    }

    /// <summary>
    /// Process an HTTP request where the requested resource is valid and successfully returned.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    protected override Task<HttpResponse> ProcessRequestAsync(HttpRequest request, params object[] additionalData)
    {
        return Task.FromResult<HttpResponse>(this.CreateHttpResponse(request.Id, HttpStatusCode.OK));
    }
}