// <copyright file="ServerDataSentEventArgs.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Object containing event data for events raised when data is sent from a the test web server.
/// </summary>
public class ServerDataSentEventArgs : EventArgs
{
    private readonly string data;
    private readonly string connectionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDataSentEventArgs" /> class.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection from which the data is received.</param>
    /// <param name="data">The data sent to the connection.</param>
    public ServerDataSentEventArgs(string connectionId, string data)
    {
        this.data = data;
        this.connectionId = connectionId;
    }

    /// <summary>
    /// Gets the ID of the client connection from which the data is received.
    /// </summary>
    public string ConnectionId => this.connectionId;

    /// <summary>
    /// Gets the data sent to the connection.
    /// </summary>
    public string Data => this.data;
}