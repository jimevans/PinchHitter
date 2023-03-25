// <copyright file="WebContent.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Text;

/// <summary>
/// Represents a web page of HTML content.
/// </summary>
public static class WebContent
{
    /// <summary>
    /// Creates the web page content as a string.
    /// </summary>
    /// <param name="bodyContent">The body of the web page.</param>
    /// <param name="headContent">The head of the web page. Defaults to an empty string.</param>
    /// <returns>The web page as a string.</returns>
    public static string AsHtmlDocument(string bodyContent, string headContent = "")
    {
        return $"<!DOCTYPE html><html><head>{headContent}</head><body>{bodyContent}</body></html>";
    }
}