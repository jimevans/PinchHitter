// <copyright file="RequestHandledEventArgs.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Collections.ObjectModel;
using System.Net;

/// <summary>
/// Object containing information about the RequestHandled event.
/// </summary>
public class RequestHandledEventArgs : EventArgs
{
    private readonly string connectionId;
    private readonly string requestId;
    private readonly string httpVersion;
    private readonly string? reasonPhrase;
    private readonly HttpStatusCode statusCode;
    private readonly string body;
    private readonly IDictionary<string, IList<string>> headers;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestHandledEventArgs"/> class.
    /// </summary>
    /// <param name="connectionId">The ID of the connection from which the request was received.</param>
    /// <param name="response">The HTTP response for the request being handled.</param>
    public RequestHandledEventArgs(string connectionId, HttpResponse response)
    {
        this.connectionId = connectionId;
        this.requestId = response.RequestId;
        this.httpVersion = response.HttpVersion;
        this.statusCode = response.StatusCode;
        this.reasonPhrase = response.ReasonPhrase;
        this.body = response.BodyContent;
        Dictionary<string, IList<string>> readOnlyHeaders = new();
        foreach (KeyValuePair<string, List<string>> pair in response.Headers)
        {
            readOnlyHeaders[pair.Key] = pair.Value.AsReadOnly();
        }

        this.headers = new ReadOnlyDictionary<string, IList<string>>(readOnlyHeaders);
    }

    /// <summary>
    /// Gets the ID of the connection for which the response is being processed.
    /// </summary>
    public string ConnectionId => this.connectionId;

    /// <summary>
    /// Gets the ID of the request being processed.
    /// </summary>
    public string RequestId => this.requestId;

    /// <summary>
    /// Gets the status code of the response.
    /// </summary>
    public HttpStatusCode StatusCode => this.statusCode;

    /// <summary>
    /// Gets the reason phrase of the response.
    /// </summary>
    public string? ReasonPhrase => this.reasonPhrase;

    /// <summary>
    /// Gets the HTTP version of the response.
    /// </summary>
    public string HttpVersion => this.httpVersion;

    /// <summary>
    /// Gets a read-only copy of the headers of the response.
    /// </summary>
    public IDictionary<string, IList<string>> Headers => this.headers;

    /// <summary>
    /// Gets the body of the response.
    /// </summary>
    public string Body => this.body;
}