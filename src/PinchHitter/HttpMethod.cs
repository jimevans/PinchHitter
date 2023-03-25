// <copyright file="HttpMethod.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Represents a method of an HTTP request.
/// </summary>
public enum HttpMethod
{
    /// <summary>
    /// The HTTP GET method.
    /// </summary>
    Get,

    /// <summary>
    /// The HTTP POST method.
    /// </summary>
    Post,

    /// <summary>
    /// The HTTP DELETE method.
    /// </summary>
    Delete,

    /// <summary>
    /// The PUT DELETE method.
    /// </summary>
    Put,

    /// <summary>
    /// The HTTP HEAD method.
    /// </summary>
    Head,

    /// <summary>
    /// The HTTP OPTIONS method.
    /// </summary>
    Options,

    /// <summary>
    /// The HTTP TRACE method.
    /// </summary>
    Trace,

    /// <summary>
    /// The HTTP CONNECT method.
    /// </summary>
    Connect,
}