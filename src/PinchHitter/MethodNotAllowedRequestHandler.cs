// <copyright file="MethodNotAllowedRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;

/// <summary>
/// Handles requests where the request is invalid.
/// </summary>
public class MethodNotAllowedRequestHandler : HttpRequestHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MethodNotAllowedRequestHandler"/> class.
    /// </summary>
    /// <param name="content">The content of the Not Found page to be served.</param>
    public MethodNotAllowedRequestHandler(string content)
        : base(content)
    {
    }

    /// <summary>
    /// Process an HTTP request where the requested resource is not found.
    /// </summary>
    /// <param name="request">The HttpRequest object representing the request.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>An HttpResponse object representing the response.</returns>
    protected override HttpResponse ProcessRequest(HttpRequest request, params object[] additionalData)
    {
        if (additionalData.Length == 0)
        {
            throw new ArgumentException("Request handler requires list of valid methods.", nameof(additionalData));
        }

        if (additionalData[0] is not List<HttpMethod> validMethods)
        {
            throw new ArgumentException("Additional data must be a list of HttpMethod values.", nameof(additionalData));
        }

        if (validMethods.Count == 0)
        {
            throw new ArgumentException("List of HttpMethod values most contain at least one entry.", nameof(additionalData));
        }

        List<string> methodStrings = validMethods.ConvertAll((x) => x.ToString().ToUpperInvariant());
        methodStrings.Sort();
        HttpResponse responseData = this.CreateHttpResponse(request.Id, HttpStatusCode.MethodNotAllowed);
        responseData.Headers["Allow"] = new List<string>() { string.Join(", ", methodStrings) };
        return responseData;
    }
}