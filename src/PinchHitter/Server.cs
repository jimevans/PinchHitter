// <copyright file="Server.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

/// <summary>
/// An abstract base class for a server listening on a port for TCP messages and able
/// to process incoming data received on that port.
/// </summary>
public class Server
{
    private readonly ConcurrentDictionary<string, ClientConnection> activeConnections = new();
    private readonly TcpListener listener;
    private readonly List<string> serverLog = new();
    private readonly HttpRequestProcessor httpProcessor = new();
    private readonly ServerObservableEvent<ServerDataReceivedEventArgs> onServerDataReceivedEvent = new();
    private readonly ServerObservableEvent<ServerDataSentEventArgs> onServerDataSentEvent = new();
    private readonly ServerObservableEvent<ClientConnectionEventArgs> onClientConnectedEvent = new();
    private readonly ServerObservableEvent<ClientConnectionEventArgs> onClientDisconnectedEvent = new();
    private int port = 0;
    private int bufferSize = 1024;
    private bool isAcceptingConnections = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    public Server()
        : this(0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class listening on a specific port.
    /// </summary>
    /// <param name="port">The port on which to listen. Passing zero (0) for the port will select a random port.</param>
    public Server(int port)
    {
        this.port = port;
        this.listener = new(new IPEndPoint(IPAddress.Loopback, this.port));
    }

    /// <summary>
    /// Gets the event raised when data is received by the server.
    /// </summary>
    public ServerObservableEvent<ServerDataReceivedEventArgs> OnDataReceived => this.onServerDataReceivedEvent;

    /// <summary>
    /// Gets the event raised when data is received by the server.
    /// </summary>
    public ServerObservableEvent<ServerDataSentEventArgs> OnDataSent => this.onServerDataSentEvent;

    /// <summary>
    /// Gets the event raised when a client connects to the server.
    /// </summary>
    public ServerObservableEvent<ClientConnectionEventArgs> OnClientConnected => this.onClientConnectedEvent;

    /// <summary>
    /// Gets the event raised when a client disconnects from the server.
    /// </summary>
    public ServerObservableEvent<ClientConnectionEventArgs> OnClientDisconnected => this.onClientDisconnectedEvent;

    /// <summary>
    /// Gets the port on which the server is listening for connections.
    /// </summary>
    public int Port => this.port;

    /// <summary>
    /// Gets the read-only communication log of the server.
    /// </summary>
    public IList<string> Log => this.serverLog.AsReadOnly();

    /// <summary>
    /// Gets or sets the size in bytes of the buffer for receiving incoming requests.
    /// Defaults to 1024 bytes. Cannot be set once the server has started listening.
    /// </summary>
    public int BufferSize
    {
        get
        {
            return this.bufferSize;
        }

        set
        {
            if (this.isAcceptingConnections)
            {
                throw new ArgumentException("Cannot set buffer size once server has started listening for requests");
            }

            this.bufferSize = value;
        }
    }

    /// <summary>
    /// Starts the server listening for incoming connections.
    /// </summary>
    public void Start()
    {
        this.listener.Start();
        IPEndPoint? localEndpoint = this.listener.LocalEndpoint as IPEndPoint;
        if (localEndpoint is not null)
        {
            this.port = localEndpoint.Port;
        }

        this.isAcceptingConnections = true;
        _ = Task.Run(() => this.AcceptConnectionsAsync()).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the server from listening for incoming connections.
    /// </summary>
    public void Stop()
    {
        if (this.isAcceptingConnections)
        {
            // Stop accepting connections, so that the population of the
            // dictionary of connections is stable.
            this.isAcceptingConnections = false;
            foreach (KeyValuePair<string, ClientConnection> pair in this.activeConnections)
            {
                pair.Value.StopReceiving();
            }

            this.activeConnections.Clear();
            this.listener.Stop();
        }
    }

    /// <summary>
    /// Asynchronously forcibly disconnects the server without following the appropriate shutdown procedure.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection to disconnect.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task DisconnectAsync(string connectionId)
    {
        if (!this.activeConnections.TryGetValue(connectionId, out ClientConnection? connection))
        {
            throw new PinchHitterException($"Unknown connection ID {connectionId}");
        }

        await connection.DisconnectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a resource with this web server to be returned when requested.
    /// </summary>
    /// <param name="url">The relative URL associated with this resource.</param>
    /// <param name="handler">The handler to handle HTTP requests for the given URL.</param>
    public void RegisterHandler(string url, HttpRequestHandler handler)
    {
        this.httpProcessor.RegisterHandler(url, handler);
    }

    /// <summary>
    /// Registers a resource with this web server to be returned when requested.
    /// </summary>
    /// <param name="url">The relative URL associated with this resource.</param>
    /// <param name="method">The HTTP method for which to add the handler.</param>
    /// <param name="handler">The handler to handle HTTP requests for the given URL.</param>
    public void RegisterHandler(string url, HttpMethod method, HttpRequestHandler handler)
    {
        this.httpProcessor.RegisterHandler(url, method, handler);
    }

    /// <summary>
    /// Asynchronously sends data to the client connected via this client connection.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection to send data to.</param>
    /// <param name="data">The data to be sent.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task SendDataAsync(string connectionId, string data)
    {
        WebSocketFrame frame = WebSocketFrame.Encode(data, WebSocketOpcodeType.Text);
        await this.SendDataAsync(connectionId, frame.Data).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a value indicating whether the client connection should ignore requests
    /// from the client to close the WebSocket. This allows simulating servers that
    /// do not properly implement cleanly closing a WebSocket.
    /// </summary>
    /// <param name="connectionId">The ID of the connection for which to set the close request behavior.</param>
    /// <param name="ignoreCloseConnectionRequest"><see langword="true"/> to have the client connection ignore close requests; otherwise, <see langword="false"/>.</param>
    /// <exception cref="PinchHitterException">Thrown when an invalid connection ID is specified.</exception>
    public void IgnoreCloseConnectionRequest(string connectionId, bool ignoreCloseConnectionRequest)
    {
        if (!this.activeConnections.TryGetValue(connectionId, out ClientConnection? connection))
        {
            throw new PinchHitterException($"Unknown connection ID {connectionId}");
        }

        connection.IgnoreCloseRequest = ignoreCloseConnectionRequest;
    }

    /// <summary>
    /// Asynchronously sends data to the client requesting data from this server.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection to send data to.</param>
    /// <param name="data">A byte array representing the data to be sent.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="PinchHitterException">Thrown when an invalid connection ID is specified.</exception>
    protected async Task SendDataAsync(string connectionId, byte[] data)
    {
        if (!this.activeConnections.TryGetValue(connectionId, out ClientConnection? connection))
        {
            throw new PinchHitterException($"Unknown connection ID {connectionId}");
        }

        await connection.SendDataAsync(data).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a message to the server log.
    /// </summary>
    /// <param name="message">The message to add.</param>
    protected void LogMessage(string message)
    {
        this.serverLog.Add(message);
    }

    private async Task AcceptConnectionsAsync()
    {
        while (true)
        {
            Socket socket = await this.listener.AcceptSocketAsync().ConfigureAwait(false);
            if (this.isAcceptingConnections)
            {
                ClientConnection clientConnection = new(socket, this.httpProcessor, this.bufferSize);
                clientConnection.OnDataReceived.AddObserver(async (e) =>
                {
                    await this.onServerDataReceivedEvent.NotifyObserversAsync(new ServerDataReceivedEventArgs(e.ConnectionId, e.DataReceived)).ConfigureAwait(false);
                });
                clientConnection.OnDataSent.AddObserver(async (e) =>
                {
                    await this.onServerDataSentEvent.NotifyObserversAsync(new ServerDataSentEventArgs(e.ConnectionId, e.DataSent)).ConfigureAwait(false);
                });
                clientConnection.OnLogMessage.AddObserver((e) =>
                {
                    this.LogMessage(e.Message);
                });
                clientConnection.OnStarting.AddObserver(async (e) =>
                {
                    await this.OnClientConnectionStarting(clientConnection).ConfigureAwait(false);
                });
                clientConnection.OnStopped.AddObserver(async (e) =>
                {
                    await this.OnClientConnectionStopped(e.ConnectionId).ConfigureAwait(false);
                });
                clientConnection.StartReceiving();
                this.LogMessage("Client connected");
            }
        }
    }

    private async Task OnClientConnectionStarting(ClientConnection connection)
    {
        this.activeConnections.TryAdd(connection.ConnectionId, connection);
        await this.onClientConnectedEvent.NotifyObserversAsync(new ClientConnectionEventArgs(connection.ConnectionId)).ConfigureAwait(false);
    }

    private async Task OnClientConnectionStopped(string connectionId)
    {
        this.activeConnections.TryRemove(connectionId, out ClientConnection? _);
        await this.onClientDisconnectedEvent.NotifyObserversAsync(new ClientConnectionEventArgs(connectionId)).ConfigureAwait(false);
    }
}