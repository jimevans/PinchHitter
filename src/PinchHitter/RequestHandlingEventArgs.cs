// <copyright file="RequestHandlingEventArgs.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Collections.ObjectModel;

/// <summary>
/// Object containing information about the RequestHandling event.
/// </summary>
public class RequestHandlingEventArgs : EventArgs
{
    private readonly string connectionId;
    private readonly string requestId;
    private readonly string httpVersion;
    private readonly HttpMethod method;
    private readonly Uri uri;
    private readonly string body;
    private readonly IDictionary<string, IList<string>> headers;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestHandlingEventArgs"/> class.
    /// </summary>
    /// <param name="connectionId">The ID of the connection from which the request was received.</param>
    /// <param name="request">The HTTP request to be handled.</param>
    public RequestHandlingEventArgs(string connectionId, HttpRequest request)
    {
        this.connectionId = connectionId;
        this.requestId = request.Id;
        this.httpVersion = request.HttpVersion;
        this.method = request.Method;
        this.uri = request.Uri;
        this.body = request.Body;
        Dictionary<string, IList<string>> readOnlyHeaders = new();
        foreach (KeyValuePair<string, List<string>> pair in request.Headers)
        {
            readOnlyHeaders[pair.Key] = pair.Value.AsReadOnly();
        }

        this.headers = new ReadOnlyDictionary<string, IList<string>>(readOnlyHeaders);
    }

    /// <summary>
    /// Gets the ID of the connection for which the request is being processed.
    /// </summary>
    public string ConnectionId => this.connectionId;

    /// <summary>
    /// Gets the ID of the request being processed.
    /// </summary>
    public string RequestId => this.requestId;

    /// <summary>
    /// Gets the method of the request being processed.
    /// </summary>
    public HttpMethod Method => this.method;

    /// <summary>
    /// Gets the URI of the request being processed.
    /// </summary>
    public Uri Uri => this.uri;

    /// <summary>
    /// Gets the HTTP version of the request being processed.
    /// </summary>
    public string HttpVersion => this.httpVersion;

    /// <summary>
    /// Gets a read-only copy of the headers of the request being processed.
    /// </summary>
    public IDictionary<string, IList<string>> Headers => this.headers;

    /// <summary>
    /// Gets the body of the request being processed.
    /// </summary>
    public string Body => this.body;
}