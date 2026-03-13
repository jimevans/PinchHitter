// <copyright file="MethodNotAllowedRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Text;

/// <summary>
/// Handles requests where the method is not allowed for the URL.
/// </summary>
public class MethodNotAllowedRequestHandler : HttpRequestHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MethodNotAllowedRequestHandler"/> class.
    /// </summary>
    /// <param name="content">The content of the Not Found page to be served.</param>
    public MethodNotAllowedRequestHandler(string content)
        : base(Encoding.UTF8.GetBytes(content))
    {
    }

    /// <summary>
    /// Handles an HTTP request.
    /// </summary>
    /// <param name="connectionId">The connection from which the HTTP request to be handled was received.</param>
    /// <param name="request">The HTTP request to handle.</param>
    /// <param name="verbsAllowedForUrl">A list of HTTP methods allowed for the requested URL.</param>
    /// <returns>The response to the HTTP request.</returns>
    public Task<HttpResponse> HandleRequestAsync(string connectionId, HttpRequest request, List<HttpRequestMethod> verbsAllowedForUrl)
    {
        return this.HandleRequestAsync(connectionId, request, (object)verbsAllowedForUrl);
    }

    /// <summary>
    /// Process an HTTP request where the provided method is not allowed for the URL.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    protected override Task<HttpResponse> ProcessRequestAsync(HttpRequest request, params object[] additionalData)
    {
        if (additionalData.Length == 0)
        {
            throw new ArgumentException("Request handler requires list of valid methods.", nameof(additionalData));
        }

        if (additionalData[0] is not List<HttpRequestMethod> validMethods)
        {
            throw new ArgumentException("Additional data must be a list of HttpMethod values.", nameof(additionalData));
        }

        if (validMethods.Count == 0)
        {
            throw new ArgumentException("List of HttpMethod values must contain at least one entry.", nameof(additionalData));
        }

        List<string> methodStrings = validMethods.ConvertAll((x) => x.ToString().ToUpperInvariant());
        methodStrings.Sort();
        HttpResponse responseData = this.CreateHttpResponse(request.Id, HttpStatusCode.MethodNotAllowed);
        responseData.Headers["Allow"] = new List<string>() { string.Join(", ", methodStrings) };
        return Task.FromResult<HttpResponse>(responseData);
    }
}
