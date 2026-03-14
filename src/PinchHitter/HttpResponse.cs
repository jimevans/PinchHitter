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
    /// <summary>
    /// A mapping of HTTP status codes to their reason phrases. This is not an
    /// exhaustive list of all HTTP status codes, but it includes the most
    /// common ones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reason phrases for HTTP status codes are not standardized and can vary
    /// between different implementations. The reason phrases included in this
    /// mapping are based on the most commonly used ones, but they may not be
    /// the same as the reason phrases used by other implementations.
    /// </para>
    /// <para>
    /// We maintain this list here, because the System.Net.HttpStatusDescription
    /// class is not available in .NET Standard 2.0, which is the target framework
    /// for this library. Likewise, we omit values in the HttpStatusCode enum that
    /// are not present in .NET Standard 2.0. Notable status code omissions include:
    /// <list>
    ///   <item>308 Permanent Redirect</item>
    ///   <item>422 Unprocessable Entity</item>
    ///   <item>429 Too Many Requests</item>
    /// </list>
    /// </para>
    /// </remarks>
    private static readonly Dictionary<HttpStatusCode, string> ReasonPhrases = new()
    {
        { HttpStatusCode.SwitchingProtocols,  "Switching Protocols" },
        { HttpStatusCode.OK, "OK" },
        { HttpStatusCode.Created, "Created" },
        { HttpStatusCode.NoContent, "No Content" },
        { HttpStatusCode.Found, "Found" },
        { HttpStatusCode.TemporaryRedirect, "Temporary Redirect" },
        { HttpStatusCode.MovedPermanently, "Moved Permanently" },
        { HttpStatusCode.BadRequest, "Bad Request" },
        { HttpStatusCode.Unauthorized, "Unauthorized" },
        { HttpStatusCode.Forbidden, "Forbidden" },
        { HttpStatusCode.NotFound, "Not Found" },
        { HttpStatusCode.MethodNotAllowed, "Method Not Allowed" },
        { HttpStatusCode.Conflict, "Conflict" },
        { HttpStatusCode.InternalServerError, "Internal Server Error" },
        { HttpStatusCode.ServiceUnavailable, "Service Unavailable" },
    };

    private readonly string requestId;
    private readonly Dictionary<string, List<string>> headers = new(StringComparer.OrdinalIgnoreCase);
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
    /// Gets the body content of this HTTP response as an array of bytes.
    /// </summary>
    public ReadOnlyMemory<byte> BodyContentBytes { get => new ReadOnlyMemory<byte>(this.bodyContent); }

    /// <summary>
    /// Gets or sets the body content as a string.
    /// </summary>
    public string TextBodyContent { get => Encoding.UTF8.GetString(this.bodyContent); set => this.bodyContent = Encoding.UTF8.GetBytes(value); }

    /// <summary>
    /// Sets the body content of this HTTP response as an array of bytes.
    /// </summary>
    /// <param name="content">The byte array containing the body content.</param>
    public void SetBodyContent(byte[] content)
    {
        this.bodyContent = content;
    }

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

        List<byte> responseBuffer = [.. Encoding.UTF8.GetBytes(string.Join("\r\n", [.. responseLines]))];
        if (this.bodyContent.Length != 0)
        {
            responseBuffer.AddRange(this.bodyContent);
        }

        return responseBuffer.ToArray();
    }
}
