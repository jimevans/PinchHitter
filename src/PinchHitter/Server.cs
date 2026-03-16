// <copyright file="Server.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// An abstract base class for a server listening on a port for TCP messages and able
/// to process incoming data received on that port.
/// </summary>
public class Server : IAsyncDisposable
{
    private readonly SemaphoreSlim startStopSemaphore = new(1, 1);
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
    private CancellationTokenSource? acceptConnectionsCancellationTokenSource;
    private int port = 0;
    private int bufferSize = 1024;
    private int isAcceptingConnectionsFlag = 0;
    private bool isDisposed = false;

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
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed <see cref="Server"/>.</exception>
    public async Task StartAsync()
    {
        this.ThrowIfDisposed();
        await this.AcquireStartStopLockAsync().ConfigureAwait(false);
        try
        {
            if (this.IsAcceptingConnections)
            {
                throw new InvalidOperationException("Server is already accepting connections");
            }

            // Set the accepting connections flag before starting the listener
            // to ensure that the accept loop is able to begin accepting
            // connections immediately.
            this.IsAcceptingConnections = true;

            // Set up the cancellation token source for the accept loop. Note
            // that the token source is disposed in StopAsync after canceling.
            // The CancellationToken is registered to call listener.Stop(),
            // which will cause the AcceptSocketAsync call in the accept loop
            // to throw a SocketException, allowing the loop to exit gracefully.
            this.acceptConnectionsCancellationTokenSource = new CancellationTokenSource();
            this.acceptConnectionsCancellationTokenSource.Token.Register(this.listener.Stop);

            // Start the listener, and capture the port.
            this.listener.Start();
            IPEndPoint? localEndpoint = this.listener.LocalEndpoint as IPEndPoint;
            if (localEndpoint is not null)
            {
                this.port = localEndpoint.Port;
            }

            // Start the accept loop.
            this.acceptConnectionsTask = this.AcceptConnectionsAsync(this.acceptConnectionsCancellationTokenSource.Token);
        }
        finally
        {
            await this.ReleaseStartStopLockAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously stops the server from listening for incoming connections, awaiting the
    /// graceful teardown of all active connections before returning.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed <see cref="Server"/>.</exception>
    public async Task StopAsync()
    {
        this.ThrowIfDisposed();
        await this.AcquireStartStopLockAsync().ConfigureAwait(false);
        try
        {
            List<Task> tasks = [];
            if (this.IsAcceptingConnections)
            {
                // Step 1: Stop accepting new connections. Future incoming
                // connections will be rejected immediately after this point,
                // but existing connections will remain active until they are
                // stopped in Step 3.
                this.IsAcceptingConnections = false;

                // Step 2: Stop the accept loop and close the listener socket.
                // The CancellationToken is registered to call listener.Stop(),
                // which will cause the AcceptSocketAsync call in the accept
                // loop to throw a SocketException, allowing the loop to exit
                // gracefully. This allows any in-flight accepts to complete
                // and ensures that the accept loop task completes before we
                // proceed with shutting down active connections.
                if (this.acceptConnectionsCancellationTokenSource is not null)
                {
                    this.acceptConnectionsCancellationTokenSource.Cancel();
                    await this.acceptConnectionsTask.ConfigureAwait(false);
                    this.acceptConnectionsCancellationTokenSource.Dispose();
                    this.acceptConnectionsCancellationTokenSource = null;
                }

                // Step 3: Stop all active connections and gather their receive tasks.
                // Snapshot before canceling: OnClientConnectionStopped removes tasks
                // from the dictionary asynchronously as each connection winds down.
                List<ClientConnection> connections = [.. this.activeConnections.Values];
                foreach (ClientConnection connection in connections)
                {
                    Task connectionTask = connection.StopReceivingAsync();
                    tasks.Add(connectionTask);
                }

                this.activeConnections.Clear();
            }

            // Step 4: Wait for all receive loops to complete. ContinueWith swallows per-task
            // OperationCanceledExceptions that result from canceling the receive token,
            // and prevents UnobservedTaskException crashes due to those expected exceptions.
            await Task.WhenAll(tasks.Select(t => t.ContinueWith(_ => { }, TaskScheduler.Default))).ConfigureAwait(false);
        }
        finally
        {
            await this.ReleaseStartStopLockAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously releases all resources used by the <see cref="Server"/>.
    /// </summary>
    /// <returns>The value task object representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!this.isDisposed)
        {
            await this.DisposeAsyncCore().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously forcibly disconnects the server without following the appropriate shutdown procedure.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection to disconnect.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task DisconnectAsync(string connectionId)
    {
        this.ThrowIfDisposed();
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
    /// <remarks>
    /// The URL to be registered should be relative to the root URL of the server.
    /// For example, if the server is listening on http://localhost:8080, and you want
    /// to register a handler for requests to http://localhost:8080/hello, you should
    /// pass "/hello" as the URL parameter. URL matching is strictly a string match,
    /// meaning that URLs must exactly match the output of Url.TryCreate() to be
    /// handled by the default HttpRequestProcessor. For more flexible URL matching,
    /// users should create their own implementation of HttpRequestProcessor.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed <see cref="Server"/>.</exception>
    public void RegisterHandler(string url, HttpRequestHandler handler)
    {
        this.ThrowIfDisposed();
        this.httpProcessor.RegisterHandler(url, handler);
    }

    /// <summary>
    /// Registers a resource with this web server to be returned when requested.
    /// </summary>
    /// <param name="url">The relative URL associated with this resource.</param>
    /// <param name="method">The HTTP method for which to add the handler.</param>
    /// <param name="handler">The handler to handle HTTP requests for the given URL.</param>
    /// <remarks>
    /// The URL to be registered should be relative to the root URL of the server.
    /// For example, if the server is listening on http://localhost:8080, and you want
    /// to register a handler for requests to http://localhost:8080/hello, you should
    /// pass "/hello" as the URL parameter. URL matching is strictly a string match,
    /// meaning that URLs must exactly match the output of Url.TryCreate() to be
    /// handled by the default HttpRequestProcessor. For more flexible URL matching,
    /// users should create their own implementation of HttpRequestProcessor.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed <see cref="Server"/>.</exception>
    public void RegisterHandler(string url, HttpRequestMethod method, HttpRequestHandler handler)
    {
        this.ThrowIfDisposed();
        this.httpProcessor.RegisterHandler(url, method, handler);
    }

    /// <summary>
    /// Asynchronously sends data formatted as a WebSocket frame to the client connected via this client connection.
    /// It is expected that the client connection is already established as a WebSocket connection.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection to send data to.</param>
    /// <param name="data">The data to be sent.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionId"/> is empty or contains only whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed <see cref="Server"/>.</exception>
    public async Task SendWebSocketDataAsync(string connectionId, string data)
    {
        data ??= string.Empty;
        await this.SendWebSocketDataInternalAsync(connectionId, Encoding.UTF8.GetBytes(data), WebSocketOpcodeType.Text).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously sends binary data formatted as a WebSocket frame to the client connected via this client connection.
    /// It is expected that the client connection is already established as a WebSocket connection.
    /// </summary>
    /// <param name="connectionId">The ID of the client connection to send data to.</param>
    /// <param name="data">The binary data to be sent.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionId"/> is empty or contains only whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed <see cref="Server"/>.</exception>
    public async Task SendWebSocketDataAsync(string connectionId, byte[] data)
    {
        data ??= Array.Empty<byte>();
        await this.SendWebSocketDataInternalAsync(connectionId, data, WebSocketOpcodeType.Binary).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a value indicating whether the client connection should ignore requests
    /// from the client to close the WebSocket. This allows simulating servers that
    /// do not properly implement cleanly closing a WebSocket.
    /// </summary>
    /// <param name="connectionId">The ID of the connection for which to set the close request behavior.</param>
    /// <param name="ignoreCloseConnectionRequest"><see langword="true"/> to have the client connection ignore close requests; otherwise, <see langword="false"/>.</param>
    /// <exception cref="PinchHitterException">Thrown when an invalid connection ID is specified.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionId"/> is empty or contains only whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when called on a disposed <see cref="Server"/>.</exception>
    public void IgnoreCloseConnectionRequest(string connectionId, bool ignoreCloseConnectionRequest)
    {
        this.ThrowIfDisposed();
        if (connectionId is null)
        {
            throw new ArgumentNullException(nameof(connectionId), "Connection ID cannot be null.");
        }

        connectionId = connectionId.Trim();
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException("Connection ID cannot be empty.", nameof(connectionId));
        }

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
    /// Asynchronously releases all resources used by the <see cref="Server"/>.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        // StopAsync calls Stop() on the listener.
        await this.StopAsync().ConfigureAwait(false);
        this.listener.Server.Dispose();
        this.startStopSemaphore.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    /// Adds a message to the server log.
    /// </summary>
    /// <param name="message">The message to add.</param>
    protected void LogMessage(string message)
    {
        this.serverLog.Enqueue(message);
    }

    /// <summary>
    /// Asynchronously accepts an incoming socket connection.
    /// </summary>
    /// <returns>The Task containing the Socket object accepted by the listener.</returns>
    protected virtual async Task<Socket> AcceptSocketAsync()
    {
        Socket socket = await this.listener.AcceptSocketAsync().ConfigureAwait(false);
        return socket;
    }

    private async Task SendWebSocketDataInternalAsync(string connectionId, byte[] data, WebSocketOpcodeType opcode)
    {
        this.ThrowIfDisposed();
        if (connectionId is null)
        {
            throw new ArgumentNullException(nameof(connectionId), "Connection ID cannot be null.");
        }

        connectionId = connectionId.Trim();
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException("Connection ID cannot be empty.", nameof(connectionId));
        }

        WebSocketFrame frame = WebSocketFrame.Encode(data, opcode);
        await this.SendDataAsync(connectionId, frame.Data).ConfigureAwait(false);
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested == false)
        {
            Socket? socket = null;
            try
            {
                socket = await this.AcceptSocketAsync().ConfigureAwait(false);
                await this.onSocketConnectedEvent.NotifyObserversAsync(EventArgs.Empty).ConfigureAwait(false);
                if (this.IsAcceptingConnections)
                {
                    // Create ClientConnection, and transfer ownership of the Socket
                    // to ClientConnection, which will prevent disposal in finally block
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
                    await clientConnection.StartReceivingAsync().ConfigureAwait(false);
                    socket = null;
                    this.LogMessage("Client connected");
                }
                else
                {
                    // If we're not accepting connections, immediately close the
                    // socket to reject the connection attempt.
                    socket.Close();
                }
            }
            catch (SocketException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            finally
            {
                socket?.Dispose();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (this.isDisposed)
        {
            throw new ObjectDisposedException(nameof(Server));
        }
    }

    private async Task AcquireStartStopLockAsync(CancellationToken cancellationToken = default)
    {
        await this.startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReleaseStartStopLockAsync()
    {
        this.startStopSemaphore.Release();
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
