// <copyright file="HttpRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Threading.Tasks;

/// <summary>
/// Handles an HTTP request.
/// </summary>
public abstract class HttpRequestHandler
{
    private readonly ServerObservableEvent<RequestHandlingEventArgs> onRequestHandlingEvent = new();
    private readonly ServerObservableEvent<RequestHandledEventArgs> onRequestHandledEvent = new();
    private readonly byte[] data;
    private string mimeType = "text/html;charset=utf-8";

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestHandler"/> class with a byte array.
    /// </summary>
    /// <param name="data">A byte array representing the data of this handler to be served.</param>
    public HttpRequestHandler(byte[] data)
    {
        this.data = data;
    }

    /// <summary>
    /// Gets the event that occurs before the handler handles the HTTP request.
    /// </summary>
    public ServerObservableEvent<RequestHandlingEventArgs> OnRequestHandling => this.onRequestHandlingEvent;

    /// <summary>
    /// Gets the event that occurs after the handler handles the HTTP request, but before the response is sent to the requester.
    /// </summary>
    public ServerObservableEvent<RequestHandledEventArgs> OnRequestHandled => this.onRequestHandledEvent;

    /// <summary>
    /// Gets the data for this resource as an array of bytes.
    /// </summary>
    public byte[] Data => this.data;

    /// <summary>
    /// Gets or sets the MIME type of this resource.
    /// </summary>
    public string MimeType { get => this.mimeType; set => this.mimeType = value; }

    /// <summary>
    /// Handles an HTTP request.
    /// </summary>
    /// <param name="connectionId">The connection from which the HTTP request to be handled was received.</param>
    /// <param name="request">The HTTP request to handle.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>The response to the HTTP request.</returns>
    public async Task<HttpResponse> HandleRequestAsync(string connectionId, HttpRequest request, params object[] additionalData)
    {
        await this.onRequestHandlingEvent.NotifyObserversAsync(new RequestHandlingEventArgs(connectionId, request)).ConfigureAwait(false);
        HttpResponse response = await this.ProcessRequestAsync(request, additionalData).ConfigureAwait(false);
        await this.onRequestHandledEvent.NotifyObserversAsync(new RequestHandledEventArgs(connectionId, response)).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Processes an HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request to handle.</param>
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>The response to the HTTP request.</returns>
    protected abstract Task<HttpResponse> ProcessRequestAsync(HttpRequest request, params object[] additionalData);

    /// <summary>
    /// Creates an HttpResponse object from this resource.
    /// </summary>
    /// <param name="requestId">The ID of the request to which the response applies.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <returns>The HTTP response to be transmitted.</returns>
    protected HttpResponse CreateHttpResponse(string requestId, HttpStatusCode statusCode)
    {
        HttpResponse response = new(requestId)
        {
            StatusCode = statusCode,
            BodyContent = this.data,
        };
        this.AddStandardResponseHeaders(response);
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
        response.Headers["Content-Length"] = new List<string>() { response.BodyContent.Length.ToString() };
    }
}