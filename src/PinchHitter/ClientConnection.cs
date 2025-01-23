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
public class ClientConnection
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly string connectionId = Guid.NewGuid().ToString();
    private readonly Socket clientSocket;
    private readonly int bufferSize;
    private readonly HttpRequestProcessor httpProcessor;
    private readonly ServerObservableEvent<ClientConnectionEventArgs> onStartingEvent = new();
    private readonly ServerObservableEvent<ClientConnectionEventArgs> onStoppedEvent = new();
    private readonly ServerObservableEvent<ClientConnectionDataReceivedEventArgs> onDataReceivedEvent = new();
    private readonly ServerObservableEvent<ClientConnectionDataSentEventArgs> onDataSentEvent = new();
    private readonly ServerObservableEvent<ClientConnectionLogMessageEventArgs> onLogMessageEvent = new();
    private WebSocketState state = WebSocketState.None;
    private bool ignoreCloseRequest = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientConnection"/> class.
    /// </summary>
    /// <param name="clientSocket">The Socket used to communicate with the client.</param>
    /// <param name="httpProcessor">An HttpRequestProcessor used to process HTTP requests.</param>
    /// <param name="bufferSize">The size of the buffer used for socket communication.</param>
    public ClientConnection(Socket clientSocket, HttpRequestProcessor httpProcessor, int bufferSize = 1024)
    {
        this.clientSocket = clientSocket;
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
    /// Gets the event raised when data is received from this client connection.
    /// </summary>
    public ServerObservableEvent<ClientConnectionDataReceivedEventArgs> OnDataReceived => this.onDataReceivedEvent;

    /// <summary>
    /// Gets the event raised when data is received from this client connection.
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
    public bool IgnoreCloseRequest { get => this.ignoreCloseRequest; set => this.ignoreCloseRequest = value; }

    /// <summary>
    /// Starts receiving data on this client connection.
    /// </summary>
    public void StartReceiving()
    {
        _ = Task.Run(() => this.ReceiveDataAsync());
    }

    /// <summary>
    /// Stops receiving data on this client connection.
    /// </summary>
    public void StopReceiving()
    {
        this.cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Asynchronously forcibly disconnects the server without following the appropriate shutdown procedure.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task DisconnectAsync()
    {
        if (this.state == WebSocketState.None)
        {
            this.cancellationTokenSource.Cancel();
        }

        if (this.state == WebSocketState.Open)
        {
            await this.SendCloseFrameAsync("Initiating close").ConfigureAwait(false);
            this.state = WebSocketState.CloseSent;
        }
    }

    /// <summary>
    /// Asynchronously sends data to the client requesting data from this server.
    /// </summary>
    /// <param name="data">A byte array representing the data to be sent.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="PinchHitterException">Thrown when there is no client socket connected.</exception>
    public async Task SendDataAsync(byte[] data)
    {
        // In .NETStandard 2.1, we could simply call Socket.SendAsync,
        // which is awaitable already. For .NETStandard 2.0, we will use
        // a synchronous call, but schedule it as a task to make it awaitable.
        int bytesSent = await Task.Run(() => this.SendDataInternal(data)).ConfigureAwait(false);
        await this.onLogMessageEvent.NotifyObserversAsync(new ClientConnectionLogMessageEventArgs($"SEND {bytesSent} bytes")).ConfigureAwait(false);
        string text = Encoding.UTF8.GetString(data);
        await this.onDataSentEvent.NotifyObserversAsync(new ClientConnectionDataSentEventArgs(this.connectionId, text)).ConfigureAwait(false);
    }

    private async Task ReceiveDataAsync()
    {
        await this.onStartingEvent.NotifyObserversAsync(new ClientConnectionEventArgs(this.connectionId)).ConfigureAwait(false);
        try
        {
            while (this.state != WebSocketState.Closed)
            {
                // In .NETStandard 2.1, we could use a NetworkStream to wrap the
                // socket and call ReadAsync(), which is awaitable to read the
                // incoming data. For .NETStandard 2.0, we must use the original
                // ReceiveAsync method on the socket directly, which is not
                // awaitable, but we will wrap that usage in a Task to make it so.
                byte[] receivedData = await Task.Run(() => this.ReceiveDataInternal()).ConfigureAwait(false);
                await this.ProcessIncomingDataAsync(receivedData, receivedData.Length).ConfigureAwait(false);
            }
        }
       finally
        {
            this.clientSocket.Close();
            await this.onStoppedEvent.NotifyObserversAsync(new ClientConnectionEventArgs(this.connectionId)).ConfigureAwait(false);
        }
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
        if (this.state == WebSocketState.None)
        {
            // A WebSocket connection has not yet been established. Treat the
            // incoming data as a standard HTTP request. If the HTTP request
            // is a request to upgrade the connection, we will handle sending
            // the expected response in ProcessHttpRequest.
            string rawRequest = Encoding.UTF8.GetString(buffer, 0, receivedLength);
            await this.onDataReceivedEvent.NotifyObserversAsync(new ClientConnectionDataReceivedEventArgs(this.connectionId, rawRequest)).ConfigureAwait(false);
            _ = HttpRequest.TryParse(rawRequest, out HttpRequest request);
            HttpResponse response = await this.httpProcessor.ProcessRequestAsync(this.connectionId, request).ConfigureAwait(false);
            if (request.IsWebSocketHandshakeRequest)
            {
                this.state = WebSocketState.Connecting;
            }

            await this.SendDataAsync(response.ToByteArray()).ConfigureAwait(false);

            if (request.IsWebSocketHandshakeRequest)
            {
                this.state = WebSocketState.Open;
            }
        }
        else
        {
            // Note: We do not handle continuation frames (WebSocketOpcodeType.Fragment)
            // in this implementation. Consider it a feature for a future iteration.
            // Likewise, we do not handle non-text frames (WebSocketOpcodeType.Binary)
            // in this implementation.
            // Finally, we do not handle ping and pong frames.
            WebSocketFrame frame = WebSocketFrame.Decode(buffer);
            if (frame.Opcode == WebSocketOpcodeType.Text)
            {
                string text = Encoding.UTF8.GetString(frame.Data);
                await this.onDataReceivedEvent.NotifyObserversAsync(new ClientConnectionDataReceivedEventArgs(this.connectionId, text)).ConfigureAwait(false);
            }

            if (frame.Opcode == WebSocketOpcodeType.ClosedConnection)
            {
                if (!this.ignoreCloseRequest)
                {
                    this.state = WebSocketState.CloseReceived;
                    await this.SendCloseFrameAsync("Acknowledge close").ConfigureAwait(false);
                }

                this.state = WebSocketState.Closed;
            }
        }
    }

    private async Task SendCloseFrameAsync(string message)
    {
        WebSocketFrame closeFrame = WebSocketFrame.Encode(message, WebSocketOpcodeType.ClosedConnection);
        await this.SendDataAsync(closeFrame.Data).ConfigureAwait(false);
    }

    private int SendDataInternal(byte[] data)
    {
        SocketAsyncEventArgs socketAsyncEventArgs = new()
        {
            SocketFlags = SocketFlags.None,
        };
        socketAsyncEventArgs.SetBuffer(data, 0, data.Length);

        ManualResetEventSlim completedEvent = new(false);
        socketAsyncEventArgs.Completed += (sender, e) =>
        {
            completedEvent.Set();
        };

        bool operationIsPending = this.clientSocket.SendAsync(socketAsyncEventArgs);
        if (operationIsPending)
        {
            completedEvent.Wait(this.cancellationTokenSource.Token);
        }

        return socketAsyncEventArgs.BytesTransferred;
    }

    private byte[] ReceiveDataInternal()
    {
        byte[] buffer = new byte[this.bufferSize];
        SocketAsyncEventArgs socketAsyncEventArgs = new()
        {
            SocketFlags = SocketFlags.None,
        };
        socketAsyncEventArgs.SetBuffer(buffer, 0, buffer.Length);

        ManualResetEventSlim completedEvent = new(false);
        socketAsyncEventArgs.Completed += (sender, e) =>
        {
            completedEvent.Set();
        };

        List<byte> receivedBytes = new();
        do
        {
            completedEvent.Reset();
            bool operationIsPending = this.clientSocket.ReceiveAsync(socketAsyncEventArgs);
            if (operationIsPending)
            {
                completedEvent.Wait(this.cancellationTokenSource.Token);
            }

            receivedBytes.AddRange(new ArraySegment<byte>(socketAsyncEventArgs.Buffer!, 0, socketAsyncEventArgs.BytesTransferred));
        }
        while (this.clientSocket.Available > 0);
        return receivedBytes.ToArray();
    }
}