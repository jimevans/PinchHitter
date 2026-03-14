// <copyright file="MethodNotAllowedRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Reflection;
using System.Text;

/// <summary>
/// Handles requests where the method is not allowed for the URL.
/// </summary>
public class MethodNotAllowedRequestHandler : HttpRequestHandler
{
    private readonly List<HttpRequestMethod> allowedMethods;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodNotAllowedRequestHandler"/> class.
    /// </summary>
    /// <param name="content">The content of the Method Not Allowed page to be served.</param>
    /// <param name="allowedMethods">A list of HTTP methods allowed for the requested URL.</param>
    /// <exception cref="ArgumentNullException">Thrown when the list of allowed methods is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the list of allowed methods is empty.</exception>
    public MethodNotAllowedRequestHandler(string content, List<HttpRequestMethod> allowedMethods)
        : base(Encoding.UTF8.GetBytes(content))
    {
        if (allowedMethods is null)
        {
            throw new ArgumentNullException("Request handler requires list of valid methods.", nameof(allowedMethods));
        }

        if (allowedMethods.Count == 0)
        {
            throw new ArgumentException("List of HttpMethod values must contain at least one entry.", nameof(allowedMethods));
        }

        this.allowedMethods = allowedMethods;
    }

    /// <summary>
    /// Process an HTTP request where the provided method is not allowed for the URL.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    protected override Task<HttpResponse> ProcessRequestAsync(HttpRequest request)
    {
        List<string> methodStrings = this.allowedMethods.ConvertAll((x) => x.ToString().ToUpperInvariant());
        methodStrings.Sort();
        HttpResponse responseData = this.CreateHttpResponse(request.Id, HttpStatusCode.MethodNotAllowed);
        responseData.Headers["Allow"] = new List<string>() { string.Join(", ", methodStrings) };
        return Task.FromResult<HttpResponse>(responseData);
    }
}
