// <copyright file="AuthenticatedResourceRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;
using System.Text;

/// <summary>
/// Handles an HTTP request requiring authentication.
/// </summary>
public class AuthenticatedResourceRequestHandler : WebResourceRequestHandler
{
    private readonly List<WebAuthenticator> authenticators = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatedResourceRequestHandler"/> class.
    /// </summary>
    /// <param name="content">The content returned by a successfully authenticated HTTP request.</param>
    public AuthenticatedResourceRequestHandler(string content)
        : base(content)
    {
    }

    /// <summary>
    /// Adds an authenticator to the request handler.
    /// </summary>
    /// <param name="authenticator">The authenticator to handle.</param>
    public void AddAuthenticator(WebAuthenticator authenticator)
    {
        this.authenticators.Add(authenticator);
    }

    /// <summary>
    /// Handles an HTTP request requiring authentication.
    /// </summary>
    /// <param name="request">The HTTP request to handle.</param>
    /// <returns>The response to the HTTP request.</returns>
    public override HttpResponse HandleRequest(HttpRequest request)
    {
        HttpResponse responseData;
        if (!request.Headers.ContainsKey("Authorization"))
        {
            responseData = new HttpResponse()
            {
                StatusCode = HttpStatusCode.Unauthorized,
            };
            this.AddStandardResponseHeaders(responseData);
            responseData.Headers["Www-Authenticate"] = new List<string>() { "Basic" };
            responseData.BodyContent = Array.Empty<byte>();
        }
        else
        {
            if (request.Headers["Authorization"].Count == 0)
            {
                responseData = new HttpResponse()
                {
                    StatusCode = HttpStatusCode.BadRequest,
                };
                this.AddStandardResponseHeaders(responseData);
                responseData.BodyContent = Encoding.UTF8.GetBytes(WebContent.AsHtmlDocument("<h1>400 Invalid request</h1><div>The authorization request was incorrect</div>"));
            }
            else
            {
                string authorizationHeader = request.Headers["Authorization"][0];
                if (!this.TryAuthenticate(authorizationHeader))
                {
                    responseData = new HttpResponse()
                    {
                        StatusCode = HttpStatusCode.Forbidden,
                    };
                    this.AddStandardResponseHeaders(responseData);
                    responseData.BodyContent = Encoding.UTF8.GetBytes(WebContent.AsHtmlDocument("<h1>403 Forbidden</h1><div>You do not have the permissions to view this resource</div>"));
                }
                else
                {
                    responseData = base.HandleRequest(request);
                }
            }
        }

        return responseData;
    }

    private bool TryAuthenticate(string authorizationHeader)
    {
        if (this.authenticators.Count == 0)
        {
            return true;
        }

        foreach (WebAuthenticator authenticator in this.authenticators)
        {
            if (authenticator.IsAuthenticated(authorizationHeader))
            {
                return true;
            }
        }

        return false;
    }
}