// <copyright file="HttpResponse.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Text;

/// <summary>
/// Represents the data of an HTTP response.
/// </summary>
public class HttpResponse
{
    private static readonly Dictionary<HttpStatusCode, string> ReasonPhrases = new()
    {
        { HttpStatusCode.SwitchingProtocols, "Switching Protocols" },
        { HttpStatusCode.OK, "OK" },
        { HttpStatusCode.MovedPermanently, "Moved Permanently" },
        { HttpStatusCode.BadRequest, "Bad Request" },
        { HttpStatusCode.Unauthorized, "Unauthorized" },
        { HttpStatusCode.Forbidden, "Forbidden" },
        { HttpStatusCode.NotFound, "Not Found" },
        { HttpStatusCode.InternalServerError, "Internal Server Error" },
    };

    private readonly string requestId;
    private readonly Dictionary<string, List<string>> headers = new();
    private string httpVersion = "HTTP/1.1";
    private HttpStatusCode statusCode = HttpStatusCode.OK;
    private byte[] bodyContent = { };

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResponse"/> class.
    /// </summary>
    /// <param name="requestId">The ID of the HTTP request for which this HTTP response is intended.</param>
    public HttpResponse(string requestId)
    {
        this.requestId = requestId;
    }

    /// <summary>
    /// Gets the ID of the HTTP request for which this HTTP response is intended.
    /// </summary>
    public string RequestId => this.requestId;

    /// <summary>
    /// Gets or sets the status code of the HTTP response.
    /// </summary>
    public HttpStatusCode StatusCode { get => this.statusCode; set => this.statusCode = value; }

    /// <summary>
    /// Gets the reason phrase for the HTTP response.
    /// </summary>
    public string? ReasonPhrase
    {
        get
        {
            if (!ReasonPhrases.ContainsKey(this.statusCode))
            {
                return null;
            }

            return ReasonPhrases[this.statusCode];
        }
    }

    /// <summary>
    /// Gets the headers for this HTTP response.
    /// </summary>
    public Dictionary<string, List<string>> Headers => this.headers;

    /// <summary>
    /// Gets or sets the HTTP version of this HTTP response.
    /// </summary>
    public string HttpVersion { get => this.httpVersion; set => this.httpVersion = value; }

    /// <summary>
    /// Gets or sets the body content of this HTTP response as an array of bytes.
    /// </summary>
    public byte[] BodyContent { get => this.bodyContent; set => this.bodyContent = value; }

    /// <summary>
    /// Gets or sets the body content as a string.
    /// </summary>
    public string TextBodyContent { get => Encoding.UTF8.GetString(this.bodyContent); set => this.bodyContent = Encoding.UTF8.GetBytes(value); }

    /// <summary>
    /// Converts this HTTP response into an array of bytes suitable for sending across a socket connection.
    /// </summary>
    /// <returns>The byte array containing the data of this HTTP response.</returns>
    public byte[] ToByteArray()
    {
        List<string> responseLines = new()
        {
            $"{this.httpVersion} {(int)this.statusCode} {this.ReasonPhrase ?? string.Empty}".Trim(),
        };
        foreach (KeyValuePair<string, List<string>> pair in this.headers)
        {
            foreach (string headerValue in pair.Value)
            {
                responseLines.Add($"{pair.Key}: {headerValue}");
            }
        }

        responseLines.Add(string.Empty);
        responseLines.Add(string.Empty);

        string[] headerArray = responseLines.ToArray();

        string header = string.Join("\r\n", responseLines.ToArray());
        List<byte> responseBuffer = new();
        responseBuffer.AddRange(Encoding.UTF8.GetBytes(string.Join("\r\n", responseLines.ToArray())));
        if (this.bodyContent.Length != 0)
        {
            responseBuffer.AddRange(this.bodyContent);
        }

        return responseBuffer.ToArray();
    }
}