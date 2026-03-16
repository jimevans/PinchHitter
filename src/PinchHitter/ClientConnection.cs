// <copyright file="ClientConnection.cs" company="PinchHitter Committers">
// Copyright (c) PinchHitter Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PinchHitter;

using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Handles a client connection to the PinchHitter server.
/// </summary>
internal class ClientConnection
{
    private readonly object stateLock = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Socket clientSocket;
    private readonly string connectionId = Guid.NewGuid().ToString();
    private readonly int bufferSize;
    private readonly HttpRequestProcessor httpProcessor;
    private readonly ServerObservableEventSource<ClientConnectionEventArgs> onStartingEvent = new();
    private readonly ServerObservableEventSource<ClientConnectionEventArgs> onStoppedEvent = new();
    private readonly ServerObservableEventSource<ClientConnectionDataReceivedEventArgs> onDataReceivedEvent = new();
    private readonly ServerObservableEventSource<ClientConnectionDataSentEventArgs> onDataSentEvent = new();
    private readonly ServerObservableEventSource<ClientConnectionLogMessageEventArgs> onLogMessageEvent = new();
    private WebSocketState state = WebSocketState.None;
    private Task receiveDataTask = Task.CompletedTask;
    private int ignoreCloseRequestFlag = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientConnection"/> class.
    /// </summary>
    /// <param name="clientSocket">The Socket used to communicate with the client.</param>
    /// <param name="httpProcessor">An HttpRequestProcessor used to process HTTP requests.</param>
    /// <param name="bufferSize">The size of the buffer used for socket communication.</param>
    internal ClientConnection(Socket clientSocket, HttpRequestProcessor httpProcessor, int bufferSize = 1024)
    {
        this.clientSocket = clientSocket;
        this.cancellationTokenSource.Token.Register(this.clientSocket.Close);
        this.bufferSize = bufferSize;
        this.httpProcessor = httpProcessor;
    }

    /// <summary>
    /// Gets the event raised when the connection is starting and ready to receive data from the client.
    /// </summary>
    public ServerObservableEvent<ClientConnectionEventArgs> OnStarting => this.onStartingEvent;

    /// <summary>
    /// Gets the event raised when the connection is stopped and can no longer receive data from the client.
    /// </summary>
    public ServerObservableEvent<ClientConnectionEventArgs> OnStopped => this.onStoppedEvent;

    /// <summary>
    /// Gets the event raised when data is received from this client connection. For WebSocket connections,
    /// this event is only raised for text frames.
    /// </summary>
    public ServerObservableEvent<ClientConnectionDataReceivedEventArgs> OnDataReceived => this.onDataReceivedEvent;

    /// <summary>
    /// Gets the event raised when data is sent over this client connection. For WebSocket connections,
    /// this event can be raised with invalid strings for binary frames.
    /// </summary>
    public ServerObservableEvent<ClientConnectionDataSentEventArgs> OnDataSent => this.onDataSentEvent;

    /// <summary>
    /// Gets the event raised when messages should be logged from this client connection.
    /// </summary>
    public ServerObservableEvent<ClientConnectionLogMessageEventArgs> OnLogMessage => this.onLogMessageEvent;

    /// <summary>
    /// Gets the unique ID of this client connection.
    /// </summary>
    public string ConnectionId => this.connectionId;

    /// <summary>
    /// Gets or sets a value indicating whether this client connection should ignore WebSocket close
    /// requests. Allows the server to simulate malformed clients who do not correctly complete the
    /// WebSocket close handshake.
    /// </summary>
    public bool IgnoreCloseRequest
    {
        get => Interlocked.CompareExchange(ref this.ignoreCloseRequestFlag, 0, 0) == 1;
        set => Interlocked.Exchange(ref this.ignoreCloseRequestFlag, value ? 1 : 0);
    }

    private WebSocketState State
    {
        get
        {
            lock (this.stateLock)
            {
                return this.state;
            }
        }

        set
        {
            lock (this.stateLock)
            {
                this.state = value;
            }
        }
    }

    /// <summary>
    /// Starts receiving data on this client connection.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public Task StartReceivingAsync()
    {
        this.receiveDataTask = this.ReceiveDataAsync();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops receiving data on this client connection. Synchronizing with the
    /// completion of the receiving task is the responsibility of the consumer.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task StopReceivingAsync()
    {
        this.cancellationTokenSource.Cancel();
        await this.receiveDataTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously forcibly disconnects the server without following the appropriate shutdown procedure.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task DisconnectAsync()
    {
        WebSocketState currentState = this.State;
        if (currentState == WebSocketState.None)
        {
            await this.StopReceivingAsync().ConfigureAwait(false);
        }

        if (currentState == WebSocketState.Open)
        {
            await this.SendCloseFrameAsync("Initiating close").ConfigureAwait(false);
            this.State = WebSocketState.CloseSent;
        }
    }

    /// <summary>
    /// Asynchronously sends data to the client requesting data from this server.
    /// </summary>
    /// <param name="data">A byte array representing the data to be sent. Must not be <see langword="null"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="PinchHitterException">Thrown when there is no client socket connected.</exception>
    /// <remarks>
    /// This method does not include a check for data being null; it is the responsibility of the caller
    /// to ensure that data is not null before calling this method.
    /// </remarks>
    public async Task SendDataAsync(byte[] data)
    {
        int bytesSent = await this.clientSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None).ConfigureAwait(false);
        await this.onLogMessageEvent.NotifyObserversAsync(new ClientConnectionLogMessageEventArgs($"SEND {bytesSent} bytes")).ConfigureAwait(false);
        string text = Encoding.UTF8.GetString(data);
        await this.onDataSentEvent.NotifyObserversAsync(new ClientConnectionDataSentEventArgs(this.connectionId, bytesSent, text)).ConfigureAwait(false);
    }

    private async Task ReceiveDataAsync()
    {
        await this.onStartingEvent.NotifyObserversAsync(new ClientConnectionEventArgs(this.connectionId)).ConfigureAwait(false);
        while (!this.cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                List<byte> receivedBytes = new();
                byte[] buffer = new byte[this.bufferSize];
                int frameBytesReceived = await this.clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None).ConfigureAwait(false);
                receivedBytes.AddRange(new ArraySegment<byte>(buffer, 0, frameBytesReceived));
                while (this.clientSocket.Available > 0)
                {
                    frameBytesReceived = await this.clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None).ConfigureAwait(false);
                    receivedBytes.AddRange(new ArraySegment<byte>(buffer, 0, frameBytesReceived));
                }

                if (receivedBytes.Count == 0)
                {
                    // Zero bytes received means the peer closed the TCP connection.
                    // Exit without attempting to process or respond to the empty buffer.
                    break;
                }

                await this.ProcessIncomingDataAsync(receivedBytes.ToArray(), receivedBytes.Count).ConfigureAwait(false);
                if (this.State == WebSocketState.Closed)
                {
                    // The State property only transitions to Closed after the
                    // WebSocket close handshake is complete, so it is now safe
                    // to close the underlying socket connection.
                    this.clientSocket.Close();
                    break;
                }
            }
            catch (SocketException)
            {
                // Socket exceptions can occur when the client disconnects unexpectedly.
                // Exit the receiving loop to clean up resources and raise the appropriate events.
                break;
            }
            catch (OperationCanceledException)
            {
                // Operation canceled exceptions can occur when the client disconnects with
                // in-progress send or receive operations.
                // Exit the receiving loop to clean up resources and raise the appropriate events.
                break;
            }
        }

        this.clientSocket.Dispose();
        this.cancellationTokenSource.Dispose();
        await this.onStoppedEvent.NotifyObserversAsync(new ClientConnectionEventArgs(this.connectionId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously processes incoming data from the client.
    /// </summary>
    /// <param name="buffer">A byte array buffer containing the data.</param>
    /// <param name="receivedLength">The length of the data in the buffer.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private async Task ProcessIncomingDataAsync(byte[] buffer, int receivedLength)
    {
        await this.onLogMessageEvent.NotifyObserversAsync(new ClientConnectionLogMessageEventArgs($"RECV {receivedLength} bytes")).ConfigureAwait(false);
        if (this.State == WebSocketState.None)
        {
            // A WebSocket connection has not yet been established. Treat the
            // incoming data as a standard HTTP request. If the HTTP request
            // is a request to upgrade the connection, we will handle sending
            // the expected response in ProcessHttpRequest.
            string rawRequest = Encoding.UTF8.GetString(buffer, 0, receivedLength);
            await this.onDataReceivedEvent.NotifyObserversAsync(new ClientConnectionDataReceivedEventArgs(this.connectionId, receivedLength, rawRequest)).ConfigureAwait(false);
            _ = HttpRequest.TryParse(rawRequest, out HttpRequest request);
            HttpResponse response = await this.httpProcessor.ProcessRequestAsync(this.connectionId, request).ConfigureAwait(false);
            if (request.IsWebSocketHandshakeRequest)
            {
                this.State = WebSocketState.Connecting;
            }

            await this.SendDataAsync(response.ToByteArray()).ConfigureAwait(false);

            if (request.IsWebSocketHandshakeRequest)
            {
                this.State = WebSocketState.Open;
            }
        }
        else
        {
            // Note: We do not handle continuation frames (WebSocketOpcodeType.Fragment)
            // in this implementation. Consider it a feature for a future iteration.
            // We also do not handle ping and pong frames. We purposefully do not
            // notification of the receipt of binary frames.
            WebSocketFrame frame = WebSocketFrame.Decode(buffer);
            if (frame.Opcode == WebSocketOpcodeType.Text)
            {
                string text = Encoding.UTF8.GetString(frame.Data);
                await this.onDataReceivedEvent.NotifyObserversAsync(new ClientConnectionDataReceivedEventArgs(this.connectionId, receivedLength, text)).ConfigureAwait(false);
            }

            if (frame.Opcode == WebSocketOpcodeType.Close)
            {
                if (!this.IgnoreCloseRequest && this.State != WebSocketState.CloseSent)
                {
                    this.State = WebSocketState.CloseReceived;
                    await this.SendCloseFrameAsync("Acknowledge close").ConfigureAwait(false);
                }

                this.State = WebSocketState.Closed;
            }
        }
    }

    private async Task SendCloseFrameAsync(string message)
    {
        WebSocketFrame closeFrame = WebSocketFrame.Encode(Encoding.UTF8.GetBytes(message), WebSocketOpcodeType.Close);
        await this.SendDataAsync(closeFrame.Data).ConfigureAwait(false);
    }
}
