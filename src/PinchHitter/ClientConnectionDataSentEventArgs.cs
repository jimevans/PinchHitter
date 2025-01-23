// <copyright file="ClientConnectionDataSentEventArgs.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

/// <summary>
/// Object containing event data for events raised when data is sent to a client connection to the PinchHitter server.
/// </summary>
public class ClientConnectionDataSentEventArgs : EventArgs
{
    private readonly string connectionId;
    private readonly string dataSent;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientConnectionDataSentEventArgs"/> class.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection from which the data is received.</param>
    /// <param name="dataSent">The data sent to the client connection.</param>
    public ClientConnectionDataSentEventArgs(string connectionId, string dataSent)
    {
        this.connectionId = connectionId;
        this.dataSent = dataSent;
    }

    /// <summary>
    /// Gets the ID of the client connection from which the data is received.
    /// </summary>
    public string ConnectionId => this.connectionId;

    /// <summary>
    /// Gets the data sent to the client connection.
    /// </summary>
    public string DataSent => this.dataSent;
}