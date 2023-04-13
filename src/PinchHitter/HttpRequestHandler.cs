// <copyright file="HttpRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Text;

/// <summary>
/// Handles an HTTP request.
/// </summary>
public abstract class HttpRequestHandler
{
    private readonly string data;
    private string mimeType = "text/html;charset=utf-8";

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestHandler"/> class with a string.
    /// </summary>
    /// <param name="data">A string representing the data of this handler to be served. The string will be converted to a byte array using UTF-8 encoding.</param>
    public HttpRequestHandler(string data)
    {
        this.data = data;
    }

    /// <summary>
    /// Gets the data for this resource as an array of bytes.
    /// </summary>
    public string Data => this.data;

    /// <summary>
    /// Gets or sets the MIME type of this resource.
    /// </summary>
    public string MimeType { get => this.mimeType; set => this.mimeType = value; }

    /// <summary>
    /// Handles an HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request to handle.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>The response to the HTTP request.</returns>
    public abstract HttpResponse HandleRequest(HttpRequest request, params object[] additionalData);

    /// <summary>
    /// Creates an HttpResponse object from this resource.
    /// </summary>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <returns>The HTTP response to be transmitted.</returns>
    protected HttpResponse CreateHttpResponse(HttpStatusCode statusCode)
    {
        HttpResponse response = new()
        {
            StatusCode = statusCode,
        };
        this.AddStandardResponseHeaders(response);
        response.BodyContent = this.data;
        return response;
    }

    /// <summary>
    /// Adds standards HTTP headers to the HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response to which to add the standard HTTP headers.</param>
    protected void AddStandardResponseHeaders(HttpResponse response)
    {
        response.Headers["Connection"] = new List<string>() { "keep-alive" };
        response.Headers["Server"] = new List<string>() { "PinchHitter/0.1 .NET/6.0" };
        response.Headers["Date"] = new List<string>() { DateTime.UtcNow.ToString("ddd, dd MMM yyy HH:mm:ss GMT") };
        response.Headers["Content-Type"] = new List<string>() { this.mimeType };
        response.Headers["Content-Length"] = new List<string>() { Encoding.UTF8.GetByteCount(this.data).ToString() };
    }
}