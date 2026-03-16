// <copyright file="WebSocketOpCodeType.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Enum for WebSocket opcode types.
/// </summary>
public enum WebSocketOpcodeType
{
    /// <summary>
    /// Denotes a continuation code.
    /// </summary>
    /// <remarks>
    /// This opcode is used for fragment frames, but is not yet supported by this implementation.
    /// Implementation may be added in a future release.
    /// </remarks>
    Fragment = 0,

    /// <summary>
    /// Denotes a text code.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Denotes a binary code.
    /// </summary>
    Binary = 2,

    /// <summary>
    /// Denotes a connection close frame.
    /// </summary>
    Close = 8,

    /// <summary>
    /// Denotes a ping.
    /// </summary>
    /// <remarks>
    /// This opcode is used as a heartbeat for WebSocket connections, but is not yet supported by this implementation.
    /// Implementation may be added in a future release.
    /// </remarks>
    Ping = 9,

    /// <summary>
    /// Denotes a pong.
    /// </summary>
    /// <remarks>
    /// This opcode is used as a response to a ping frame, but is not yet supported by this implementation.
    /// Implementation may be added in a future release.
    /// </remarks>
    Pong = 10,
}
