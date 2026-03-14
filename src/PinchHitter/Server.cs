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
public class Server : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ClientConnection> activeConnections = new();
    private readonly ConcurrentQueue<string> serverLog = new();
    private readonly ServerObservableEventSource<ServerDataReceivedEventArgs> onServerDataReceivedEvent = new();
    private readonly ServerObservableEventSource<ServerDataSentEventArgs> onServerDataSentEvent = new();
    private readonly ServerObservableEventSource<ClientConnectionEventArgs> onClientConnectedEvent = new();
    private readonly ServerObservableEventSource<ClientConnectionEventArgs> onClientDisconnectedEvent = new();
    private readonly ServerObservableEventSource<EventArgs> onSocketConnectedEvent = new();
    private readonly TcpListener listener;
    private readonly HttpRequestProcessor httpProcessor;
    private Task acceptConnectionsTask = Task.CompletedTask;
    private int port = 0;
    private int bufferSize = 1024;
    private int isAcceptingConnectionsFlag = 0;

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
        : this(port, new HttpRequestProcessor())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class listening on a specific port with a custom processor for HTTP requests.
    /// </summary>
    /// <param name="port">The port on which to listen. Passing zero (0) for the port will select a random port.</param>
    /// <param name="httpProcessor">The <see cref="HttpRequestProcessor"/> to use for processing HTTP requests.</param>
    public Server(int port, HttpRequestProcessor httpProcessor)
    {
        this.port = port;
        this.listener = new(new IPEndPoint(IPAddress.Loopback, this.port));
        this.httpProcessor = httpProcessor;
    }

    /// <summary>
    /// Gets the event raised when data is received by the server.
    /// </summary>
    public ServerObservableEvent<ServerDataReceivedEventArgs> OnDataReceived => this.onServerDataReceivedEvent;

    /// <summary>
    /// Gets the event raised when data is sent by the server.
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
    /// Gets the event raised when a socket connection is accepted by the server.
    /// </summary>
    public ServerObservableEvent<EventArgs> OnSocketAccepted => this.onSocketConnectedEvent;

    /// <summary>
    /// Gets the port on which the server is listening for connections.
    /// </summary>
    public int Port => this.port;

    /// <summary>
    /// Gets the read-only communication log of the server.
    /// </summary>
    public IReadOnlyList<string> Log => this.serverLog.ToList().AsReadOnly();

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
            if (this.IsAcceptingConnections)
            {
                throw new InvalidOperationException("Cannot set buffer size once server has started listening for requests");
            }

            this.bufferSize = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this server is accepting connections.
    /// </summary>
    private bool IsAcceptingConnections
    {
        get => Interlocked.CompareExchange(ref this.isAcceptingConnectionsFlag, 0, 0) == 1;
        set => Interlocked.Exchange(ref this.isAcceptingConnectionsFlag, value ? 1 : 0);
    }

    /// <summary>
    /// Asynchronously starts the server listening for incoming connections.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync()
    {
        this.listener.Start();
        IPEndPoint? localEndpoint = this.listener.LocalEndpoint as IPEndPoint;
        if (localEndpoint is not null)
        {
            this.port = localEndpoint.Port;
        }

        this.IsAcceptingConnections = true;
        this.acceptConnectionsTask = Task.Run(() => this.AcceptConnectionsAsync());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously stops the server from listening for incoming connections, awaiting the
    /// graceful teardown of all active connections before returning.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task StopAsync()
    {
        List<Task> tasks = this.CloseConnections();

        // Wait for all receive loops to complete. ContinueWith swallows per-task
        // OperationCanceledExceptions that result from canceling the receive token,
        // and prevents UnobservedTaskException crashes due to those expected exceptions.
        await Task.WhenAll(tasks.Select(t => t.ContinueWith(_ => { }, TaskScheduler.Default))).ConfigureAwait(false);

        // Also swallow any OperationCanceledException that may be thrown by the
        // main accept loop, and prevent UnobservedTaskException crashes due to
        // that expected exception.
        await this.acceptConnectionsTask.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously releases all resources used by the <see cref="Server"/>.
    /// </summary>
    /// <returns>The value task object representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.StopAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
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
    public void RegisterHandler(string url, HttpRequestMethod method, HttpRequestHandler handler)
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
    /// <remarks>
    /// This method is intended for advanced users who want to send raw byte data to the client,
    /// and should be used with caution. Improper use of this method can lead to malformed
    /// responses that may cause clients to crash or behave unexpectedly. This is currently
    /// implemented for WebSocket connections, but is also intended to provide support for
    /// future protocols like HTTP2 or QUIC.
    /// </remarks>
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
        this.serverLog.Enqueue(message);
    }

    private async Task AcceptConnectionsAsync()
    {
        while (true)
        {
            Socket socket = await this.listener.AcceptSocketAsync().ConfigureAwait(false);
            await this.onSocketConnectedEvent.NotifyObserversAsync(EventArgs.Empty).ConfigureAwait(false);
            if (this.IsAcceptingConnections)
            {
                ClientConnection clientConnection = new(socket, this.httpProcessor, this.bufferSize);
                this.activeConnections.TryAdd(clientConnection.ConnectionId, clientConnection);
                clientConnection.OnDataReceived.AddObserver(async (e) =>
                {
                    await this.onServerDataReceivedEvent.NotifyObserversAsync(new ServerDataReceivedEventArgs(e.ConnectionId, e.ByteCount, e.DataReceived)).ConfigureAwait(false);
                });
                clientConnection.OnDataSent.AddObserver(async (e) =>
                {
                    await this.onServerDataSentEvent.NotifyObserversAsync(new ServerDataSentEventArgs(e.ConnectionId, e.ByteCount, e.DataSent)).ConfigureAwait(false);
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
            else
            {
                socket.Close();
            }
        }
    }

    private List<Task> CloseConnections()
    {
        List<Task> tasks = [];
        if (this.IsAcceptingConnections)
        {
            this.IsAcceptingConnections = false;

            // Snapshot before canceling: OnClientConnectionStopped removes tasks from the
            // dictionary asynchronously as each connection winds down.
            List<ClientConnection> connections = [.. this.activeConnections.Values];
            foreach (ClientConnection connection in connections)
            {
                connection.StopReceiving();
                tasks.Add(connection.DataReceivedTask);
            }

            this.activeConnections.Clear();
            this.listener.Stop();
        }

        return tasks;
    }

    private async Task OnClientConnectionStarting(ClientConnection connection)
    {
        await this.onClientConnectedEvent.NotifyObserversAsync(new ClientConnectionEventArgs(connection.ConnectionId)).ConfigureAwait(false);
    }

    private async Task OnClientConnectionStopped(string connectionId)
    {
        this.activeConnections.TryRemove(connectionId, out ClientConnection? _);
        await this.onClientDisconnectedEvent.NotifyObserversAsync(new ClientConnectionEventArgs(connectionId)).ConfigureAwait(false);
    }
}
