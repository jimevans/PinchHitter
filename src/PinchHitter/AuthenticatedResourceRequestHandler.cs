// <copyright file="AuthenticatedResourceRequestHandler.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net;

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
    /// <param name="additionalData">Additional data passed into the method for handling requests.</param>
    /// <returns>The response to the HTTP request.</returns>
    protected override HttpResponse ProcessRequest(HttpRequest request, params object[] additionalData)
    {
        HttpResponse responseData;
        if (!request.Headers.ContainsKey("Authorization"))
        {
            responseData = new HttpResponse(request.Id)
            {
                StatusCode = HttpStatusCode.Unauthorized,
                BodyContent = WebContent.AsHtmlDocument("<h1>401 Unauthorized</h1><div>You are not authorized to view this resource</div>"),
            };
            this.AddStandardResponseHeaders(responseData);
            responseData.Headers["Www-Authenticate"] = new List<string>() { "Basic" };
        }
        else
        {
            if (request.Headers["Authorization"].Count == 0)
            {
                responseData = new HttpResponse(request.Id)
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    BodyContent = WebContent.AsHtmlDocument("<h1>400 Invalid request</h1><div>The authorization request was incorrect</div>"),
                };
                this.AddStandardResponseHeaders(responseData);
            }
            else
            {
                string authorizationHeader = request.Headers["Authorization"][0];
                if (!this.TryAuthenticate(authorizationHeader))
                {
                    responseData = new HttpResponse(request.Id)
                    {
                        StatusCode = HttpStatusCode.Forbidden,
                        BodyContent = WebContent.AsHtmlDocument("<h1>403 Forbidden</h1><div>You do not have the permissions to view this resource</div>"),
                    };
                    this.AddStandardResponseHeaders(responseData);
                }
                else
                {
                    responseData = base.ProcessRequest(request);
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