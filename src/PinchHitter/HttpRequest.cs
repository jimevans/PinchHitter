// <copyright file="HttpRequest.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Represents the data of an HTTP request.
/// </summary>
public class HttpRequest
{
    private readonly string requestId = Guid.NewGuid().ToString();
    private readonly Dictionary<string, List<string>> headers = new();
    private HttpMethod method = HttpMethod.Get;
    private Uri? uri;
    private string httpVersion = string.Empty;
    private string body = string.Empty;

    private HttpRequest()
    {
    }

    /// <summary>
    /// Gets the unique identifier of this HTTP request.
    /// </summary>
    public string Id => this.requestId;

    /// <summary>
    /// Gets the method of this HTTP request.
    /// </summary>
    public HttpMethod Method => this.method;

    /// <summary>
    /// Gets the URI of this HTTP request.
    /// </summary>
    public Uri Uri => this.uri!;

    /// <summary>
    /// Gets the HTTP version of this HTTP request.
    /// </summary>
    public string HttpVersion => this.httpVersion;

    /// <summary>
    /// Gets the list of headers for this HTTP request.
    /// </summary>
    public Dictionary<string, List<string>> Headers => this.headers;

    /// <summary>
    /// Gets the body of this HTTP request.
    /// </summary>
    public string Body => this.body;

    /// <summary>
    /// Gets a value indicating whether this HTTP request is a WebSocket handshake request.
    /// </summary>
    public bool IsWebSocketHandshakeRequest
    {
        get
        {
            return this.headers.ContainsKey("Connection") && this.headers["Connection"].Contains("Upgrade") &&
                this.headers.ContainsKey("Upgrade") && this.headers["Upgrade"].Contains("websocket") &&
                this.headers.ContainsKey("Sec-WebSocket-Key") && this.headers["Sec-WebSocket-Key"].Count > 0;
        }
    }

    /// <summary>
    /// Parses an incoming HTTP request.
    /// </summary>
    /// <param name="rawRequest">The string containing the HTTP request.</param>
    /// <param name="parsedRequest">The parsed HTTP request.</param>
    /// <returns>The parsed HTTP request data.</returns>
    public static bool TryParse(string rawRequest, out HttpRequest parsedRequest)
    {
        HttpRequest result = new();
        string[] requestLines = rawRequest.Split(new string[] { "\r\n" }, StringSplitOptions.None);
        int currentLine = 0;

        string navigationLine = requestLines[currentLine];
        Regex navigationRegex = new(@"(.*)\s+(.*)\s+(.*)");
        if (!navigationRegex.IsMatch(navigationLine))
        {
            parsedRequest = result;
            return false;
        }

        Match match = navigationRegex.Match(navigationLine);
        string method = match.Groups[1].Value;
        string relativeUrl = match.Groups[2].Value;
        result.httpVersion = match.Groups[3].Value;

        currentLine++;

        while (requestLines[currentLine].Length > 0)
        {
            string rawHeader = requestLines[currentLine];
            string[] readerInfo = rawHeader.Split(new string[] { ":" }, 2, StringSplitOptions.None);
            string headerName = readerInfo[0].Trim();
            string headerValue = readerInfo[1].Trim();
            if (result.headers.ContainsKey(headerName))
            {
                result.headers[headerName].Add(headerValue);
            }
            else
            {
                result.headers[headerName] = new() { headerValue };
            }

            currentLine++;
        }

        string host;
        if (!result.headers.ContainsKey("Host") || result.headers["Host"].Count != 1)
        {
            parsedRequest = result;
            return false;
        }
        else
        {
            host = result.headers["Host"][0];
        }

        if (!Enum.TryParse<HttpMethod>(method, true, out result.method))
        {
            parsedRequest = result;
            return false;
        }

        if (!Uri.TryCreate($"http://{host}{relativeUrl}", UriKind.Absolute, out result.uri))
        {
            parsedRequest = result;
            return false;
        }

        StringBuilder bodyBuilder = new();
        for (; currentLine < requestLines.Length; currentLine++)
        {
            if (bodyBuilder.Length > 0)
            {
                bodyBuilder.AppendLine();
            }

            bodyBuilder.Append(requestLines[currentLine]);
        }

        result.body = bodyBuilder.ToString();
        parsedRequest = result;
        return true;
    }
}