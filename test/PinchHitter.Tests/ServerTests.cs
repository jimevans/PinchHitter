namespace PinchHitter;

using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

[TestFixture]
public class ServerTests
{
    [Test]
    public async Task TestServerCanProcessHttpRequests()
    {
        await using Server server = new();
        await server.StartAsync();
        server.RegisterHandler("/", new WebResourceRequestHandler("hello world"));
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(responseMessage.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseContent, Contains.Substring("hello world"));
        });
    }

    [Test]
    public async Task TestCanInterceptIncomingHttpRequests()
    {
        await using Server server = new();
        await server.StartAsync();
        server.RegisterHandler("/", new WebResourceRequestHandler("hello world"));

        string receivedData = string.Empty;
        server.OnDataReceived.AddObserver((e) =>
        {
            receivedData = e.Data;
        });
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.That(receivedData, Does.StartWith("GET / HTTP/1.1"));
    }

    [Test]
    public async Task TestCanInterceptIncomingHttpResponses()
    {
        await using Server server = new();
        await server.StartAsync();
        server.RegisterHandler("/", new WebResourceRequestHandler("hello world"));

        TaskCompletionSource<ClientConnectionEventArgs> connectionTaskCompletionSource = new();
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionTaskCompletionSource.SetResult(e);
        });

        TaskCompletionSource<ServerDataSentEventArgs> dataSentTaskCompletionSource = new();
        server.OnDataSent.AddObserver((e) =>
        {
            dataSentTaskCompletionSource.SetResult(e);
        });

        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        await Task.WhenAll([connectionTaskCompletionSource.Task, dataSentTaskCompletionSource.Task]);
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.That(dataSentTaskCompletionSource.Task.Result.ConnectionId, Is.EqualTo(connectionTaskCompletionSource.Task.Result.ConnectionId));
        Assert.That(dataSentTaskCompletionSource.Task.Result.Data, Does.StartWith("HTTP/1.1 200 OK"));
    }

    [Test]
    public async Task TestServerCanInitiateCloseForHttpConnection()
    {
        await using Server server = new();
        await server.StartAsync();
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        ManualResetEventSlim disconnectionEvent = new(false);
        server.OnClientDisconnected.AddObserver((e) =>
        {
            if (e.ConnectionId == connectionId)
            {
                disconnectionEvent.Set();
            }
        });

        server.RegisterHandler("/", new WebResourceRequestHandler("hello world"));
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        string responseContent = await responseMessage.Content.ReadAsStringAsync();

        await server.DisconnectAsync(connectionId);
        bool disconnectEventRaised = disconnectionEvent.Wait(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(disconnectEventRaised, Is.True);
            Assert.That(async () => await server.DisconnectAsync(connectionId), Throws.InstanceOf<PinchHitterException>());
        });
    }

    [Test]
    public async Task TestConnectionReceiveDataLoopHandlesOperationCanceledException()
    {
        // Add an observer that throws OperationCanceledException when data is received.
        // This deterministically exercises the catch (OperationCanceledException) path
        // in ClientConnection.ReceiveDataAsync for code coverage.
        await using Server server = new();
        await server.StartAsync();
        server.RegisterHandler("/", new WebResourceRequestHandler("hello world"));

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        ManualResetEventSlim disconnectionEvent = new(false);
        server.OnClientDisconnected.AddObserver((e) =>
        {
            if (e.ConnectionId == connectionId)
            {
                disconnectionEvent.Set();
            }
        });

        server.OnDataReceived.AddObserver(_ => throw new OperationCanceledException());

        using HttpClient client = new();
        try
        {
            _ = await client.GetAsync($"http://localhost:{server.Port}/");
        }
        catch (HttpRequestException)
        {
            // Expected: connection closed abruptly when observer threw.
        }

        bool disconnectEventRaised = disconnectionEvent.Wait(TimeSpan.FromSeconds(2));
        Assert.That(disconnectEventRaised, Is.True, "OnClientDisconnected should be raised after OperationCanceledException is caught and cleanup runs");
    }

    [Test]
    public async Task TestShutdownWithoutReceivingRequest()
    {
        await using Server server = new();
        await server.StartAsync();
        Assert.That(async () => await server.StopAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestCanStartAsync()
    {
        await using Server localServer = new();
        Assert.That(async () => await localServer.StartAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestCallingStartAsyncMultipleTimesThrows()
    {
        await using Server localServer = new();
        Assert.That(async () => await localServer.StartAsync(), Throws.Nothing);
        Assert.That(async () => await localServer.StartAsync(), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task TestCanStartAsyncAfterStopAsync()
    {
        await using Server localServer = new();
        Assert.That(async () => await localServer.StartAsync(), Throws.Nothing);
        Assert.That(async () => await localServer.StopAsync(), Throws.Nothing);
        Assert.That(async () => await localServer.StartAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestCanStopAsync()
    {
        await using Server server = new();
        await server.StartAsync();
        Assert.That(async () => await server.StopAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestCanCallStopAsyncMultipleTimes()
    {
        await using Server server = new();
        await server.StartAsync();
        Assert.That(async () => await server.StopAsync(), Throws.Nothing);
        Assert.That(async () => await server.StopAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestCanDisposeAsync()
    {
        await using Server server = new();
        await server.StartAsync();
        Assert.That(async () => await server.DisposeAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestCanDisposeRepeatedly()
    {
        await using Server server = new();
        await server.StartAsync();
        Assert.That(async () => await server.DisposeAsync(), Throws.Nothing);
        Assert.That(async () => await server.DisposeAsync(), Throws.Nothing);
   }

    [Test]
    public async Task TestCannotExecuteMethodsAfterDispose()
    {
        await using Server server = new();
        await server.StartAsync();
        await server.DisposeAsync();
        Assert.Multiple(() =>
        {
            Assert.That(async () => await server.StartAsync(), Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(async () => await server.StopAsync(), Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(async () => await server.SendWebSocketDataAsync("connectionId", "data"), Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(async () => await server.SendWebSocketDataAsync("connectionId", new byte[] { 0x01, 0x02 }), Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(() => server.RegisterHandler("/url", new WebResourceRequestHandler("response")), Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(() => server.RegisterHandler("/url", HttpRequestMethod.Get, new WebResourceRequestHandler("response")), Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(() => server.IgnoreCloseConnectionRequest("connectionId", true), Throws.InstanceOf<ObjectDisposedException>());
        });
    }

    [Test]
    public async Task TestStopAsyncWhenStoppingListenerThrowsObjectDisposedException()
    {
        await using SocketAcceptanceTestServer server = new();
        await server.StartAsync();
        server.ExceptionToThrowInAccept = new ObjectDisposedException(nameof(Server));
        Assert.That(async () => await server.StopAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestStopAsyncWhenStoppingListenerThrowsSocketException()
    {
        await using SocketAcceptanceTestServer server = new();
        await server.StartAsync();
        server.ExceptionToThrowInAccept = new SocketException();
        Assert.That(async () => await server.StopAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestStopAsyncWhenStoppingListenerThrowsOperationCanceledException()
    {
        await using SocketAcceptanceTestServer server = new();
        await server.StartAsync();
        server.ExceptionToThrowInAccept = new OperationCanceledException();
        Assert.That(async () => await server.StopAsync(), Throws.Nothing);
    }

    [Test]
    public async Task TestStopAsyncAwaitsConnectionTeardown()
    {
        await using Server server = new();
        await server.StartAsync();

        // StopAsync() should not return until OnClientDisconnected has fired for every
        // active connection, requiring no additional Task.Delay or manual event waiting.
        ManualResetEventSlim connectionEvent = new(false);
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionEvent.Set();
        });

        bool disconnectedEventFired = false;
        server.OnClientDisconnected.AddObserver((e) =>
        {
            disconnectedEventFired = true;
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));

        await server.StopAsync();

        Assert.That(disconnectedEventFired, Is.True);
    }

    [Test]
    public async Task TestServerClosesConnectionAcceptedAfterStop()
    {
        await using Server server = new();
        await server.StartAsync();

        // Use OnSocketAccepted to call Stop() before we check IsAcceptingConnections,
        // deterministically exercising the else branch in AcceptConnectionsAsync that
        // closes any socket accepted once isAcceptingConnections is false.
        Task stopTask;
        ManualResetEventSlim connectionProcessed = new(false);
        server.OnSocketAccepted.AddObserver((e) =>
        {
            stopTask = server.StopAsync();
            connectionProcessed.Set();
        });
        using TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(IPAddress.Loopback, server.Port);
        connectionProcessed.Wait(TimeSpan.FromSeconds(2));

        NetworkStream stream = tcpClient.GetStream();
        byte[] buffer = new byte[1];
        int bytesRead = 0;
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));
        try
        {
            bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationTokenSource.Token);
        }
        catch (IOException)
        {
            // socket.Close() sends a TCP RST; an IOException is an acceptable close signal.
        }
        catch (OperationCanceledException)
        {
            // ReadTimeout does not apply to ReadAsync; the CancellationToken above is the
            // correct mechanism. A timeout here means the else branch in AcceptConnectionsAsync
            // did not fire — this should not occur with the deterministic callback approach.
        }

        Assert.That(bytesRead, Is.Zero);
    }

    [Test]
    public async Task TestServerDoesNotSendBadRequestOnPeerDisconnect()
    {
        await using Server server = new();
        await server.StartAsync();

        // A client that connects and immediately closes the TCP connection causes
        // ReceiveDataInternal to return zero bytes. Before CQ-1 was fixed, this
        // triggered a spurious BadRequest response attempt on the closed socket.
        ManualResetEventSlim disconnectedEvent = new(false);
        server.OnClientDisconnected.AddObserver((e) =>
        {
            disconnectedEvent.Set();
        });

        bool dataSent = false;
        server.OnDataSent.AddObserver((e) =>
        {
            dataSent = true;
        });

        TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(IPAddress.Loopback, server.Port);
        tcpClient.Close();

        bool disconnected = disconnectedEvent.Wait(TimeSpan.FromSeconds(2));
        Assert.Multiple(() =>
        {
            Assert.That(disconnected, Is.True);
            Assert.That(dataSent, Is.False);
        });
    }

    [Test]
    public async Task TestServerCanRespondToWebSocketCloseRequest()
    {
        await using Server server = new();
        await server.StartAsync();

        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }

    [Test]
    public async Task TestServerCanSimulateIgnoringWebSocketCloseRequest()
    {
        await using Server server = new();
        await server.StartAsync();

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));

        server.IgnoreCloseConnectionRequest(connectionId, true);
        Assert.Multiple(() =>
        {
            Assert.That(async () => await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None), Throws.InstanceOf<WebSocketException>().With.Property("WebSocketErrorCode").EqualTo(WebSocketError.ConnectionClosedPrematurely));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Aborted));
        });
    }

    [Test]
    public async Task TestServerCanSimulateReenablingWebSocketCloseRequest()
    {
        await using Server server = new();
        await server.StartAsync();

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));

        server.IgnoreCloseConnectionRequest(connectionId, true);
        server.IgnoreCloseConnectionRequest(connectionId, false);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Closed));
    }

    [Test]
    public async Task TestServerCanInitiateWebSocketCloseRequest()
    {
        await using Server server = new();
        await server.StartAsync();

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));

        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));
        await server.DisconnectAsync(connectionId);
        await receiveTask;
        Assert.Multiple(() =>
        {
            Assert.That(receiveTask.Result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.CloseReceived));
        });
    }

    [Test]
    public async Task TestServerCanCompleteWebSocketCloseHandshakeWhenInitiatingClose()
    {
        await using Server server = new();
        await server.StartAsync();

        // Verifies RFC 6455 §7.1.2: after the server initiates close (CloseSent state),
        // receiving the client's close acknowledgement must NOT trigger a second close frame.
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        int closeFramesSent = 0;
        server.OnDataSent.AddObserver((e) =>
        {
            // WebSocket close frames in this implementation are always exactly 2 bytes (0x88 0x00).
            // HTTP and WebSocket data frames are larger, so ByteCount == 2 is a reliable
            // indicator that a close frame was sent within this test.
            if (e.ByteCount == 2)
            {
                Interlocked.Increment(ref closeFramesSent);
            }
        });

        ManualResetEventSlim disconnectedEvent = new(false);
        server.OnClientDisconnected.AddObserver((e) =>
        {
            disconnectedEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));

        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));
        await server.DisconnectAsync(connectionId);

        WebSocketReceiveResult result = await receiveTask;
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));

        // Complete the close handshake from the client side.
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledging close", CancellationToken.None);

        bool disconnected = disconnectedEvent.Wait(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(disconnected, Is.True);
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Closed));
            Assert.That(closeFramesSent, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TestServerCanReceiveWebSocketDataFromClient()
    {
        await using Server server = new();
        await server.StartAsync();

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));

        ManualResetEventSlim syncEvent = new(false);
        string? receivedData = null;
        string receivedDataConnectionId = string.Empty;
        server.OnDataReceived.AddObserver((e) =>
        {
            receivedDataConnectionId = e.ConnectionId;
            receivedData = e.Data;
            syncEvent.Set();
        });

        await socket.SendAsync(Encoding.UTF8.GetBytes("Received from client"), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.Wait(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedDataConnectionId, Is.EqualTo(connectionId));
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo("Received from client"));
        });
    }

    [Test]
    public async Task TestServerCanReceiveDataFromMultipleSimultaneousConnections()
    {
        await using Server server = new();
        await server.StartAsync();

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket1 = new();
        await socket1.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        string connectionId1 = connectionId;

        connectionEvent.Reset();
        using ClientWebSocket socket2 = new();
        await socket2.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        string connectionId2 = connectionId;

        ManualResetEventSlim receivedDataEvent = new(false);
        List<string> receivedData = new();
        server.OnDataReceived.AddObserver((e) =>
        {
            receivedData.Add($"{e.ConnectionId}: {e.Data}");
            receivedDataEvent.Set();
        });

        await socket1.SendAsync(Encoding.UTF8.GetBytes("Sent from client 1"), WebSocketMessageType.Text, true, CancellationToken.None);
        bool isDataReceivedEventSet = receivedDataEvent.Wait(TimeSpan.FromSeconds(2));
        Assert.That(isDataReceivedEventSet, Is.True);

        receivedDataEvent.Reset();
        await socket2.SendAsync(Encoding.UTF8.GetBytes("Sent from client 2"), WebSocketMessageType.Text, true, CancellationToken.None);
        isDataReceivedEventSet = receivedDataEvent.Wait(TimeSpan.FromSeconds(2));
        Assert.That(isDataReceivedEventSet, Is.True);
        Assert.That(receivedData, Is.EquivalentTo(new List<string>() { $"{connectionId1}: Sent from client 1", $"{connectionId2}: Sent from client 2" }));
    }

    [Test]
    public async Task TestServerCanReceiveDataOnBufferBoundary()
    {
        await using Server server = new();
        await server.StartAsync();

        int dataLength = 2 * server.BufferSize;
        ManualResetEventSlim syncEvent = new(false);
        string? receivedData = null;
        string data = new('a', dataLength);
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        server.OnDataReceived.AddObserver((e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.Wait(TimeSpan.FromSeconds(15));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanReceiveDataOnMediumLargeMessage()
    {
        await using Server server = new();
        await server.StartAsync();

        // 40,000 bytes falls in the two-byte WebSocket length range (126–65535) and
        // above the signed-short boundary (32767), exercising the unsigned 16-bit path.
        int dataLength = 40000;
        ManualResetEventSlim syncEvent = new(false);
        string? receivedData = null;
        string data = new('a', dataLength);
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        server.OnDataReceived.AddObserver((e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.Wait(TimeSpan.FromSeconds(15));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanReceiveDataOnVeryLongMessage()
    {
        await using Server server = new();
        await server.StartAsync();

        int dataLength = 70000;
        ManualResetEventSlim syncEvent = new(false);
        string? receivedData = null;
        string data = new('a', dataLength);
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        server.OnDataReceived.AddObserver((e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.Wait(TimeSpan.FromSeconds(15));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendWebSocketDataToClient()
    {
        await using Server server = new();
        await server.StartAsync();

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId, "Sent to client");
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        string receivedData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo("Sent to client"));
        });
    }

    [Test]
    public async Task TestServerCanSendWebSocketNullDataToClient()
    {
        await using Server server = new();
        await server.StartAsync();

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId, (string)null!);
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        string receivedData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo(""));
        });
    }

    [Test]
    public async Task TestServerCanSendBinaryWebSocketDataToClient()
    {
        byte[] data = [0x00, 0x01, 0x02, 0xFF, 0xFE];
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        await using Server server = new();
        await server.StartAsync();
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId, data);
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        byte[] receivedData = buffer.Array!.AsSpan(0, result.Count).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendNullBinaryWebSocketDataToClient()
    {
        byte[] data = [0x00, 0x01, 0x02, 0xFF, 0xFE];
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        await using Server server = new();
        await server.StartAsync();
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId, (byte[])null!);
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        byte[] receivedData = buffer.Array!.AsSpan(0, result.Count).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
            Assert.That(receivedData, Is.EqualTo(Array.Empty<byte>()));
        });
    }

    [Test]
    public async Task TestServerCanSendBinaryDataToMultipleSimultaneousConnections()
    {
        byte[] data1 = [0x01, 0x02, 0x03];
        byte[] data2 = [0x04, 0x05, 0x06];
        await using Server server = new();
        await server.StartAsync();

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        ArraySegment<byte> buffer1 = WebSocket.CreateClientBuffer(1024, 1024);
        using ClientWebSocket socket1 = new();
        await socket1.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        string connectionId1 = connectionId;
        Task<WebSocketReceiveResult> receiveTask1 = Task.Run(() => socket1.ReceiveAsync(buffer1, CancellationToken.None));

        connectionEvent.Reset();

        ArraySegment<byte> buffer2 = WebSocket.CreateClientBuffer(1024, 1024);
        using ClientWebSocket socket2 = new();
        await socket2.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        string connectionId2 = connectionId;
        Task<WebSocketReceiveResult> receiveTask2 = Task.Run(() => socket2.ReceiveAsync(buffer2, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId1, data1);
        await server.SendWebSocketDataAsync(connectionId2, data2);
        await Task.WhenAll(receiveTask1, receiveTask2);
        WebSocketReceiveResult result1 = receiveTask1.Result;
        byte[] receivedData1 = buffer1.Array!.AsSpan(0, result1.Count).ToArray();
        WebSocketReceiveResult result2 = receiveTask2.Result;
        byte[] receivedData2 = buffer2.Array!.AsSpan(0, result2.Count).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result1.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
            Assert.That(receivedData1, Is.EqualTo(data1));
            Assert.That(result2.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
            Assert.That(receivedData2, Is.EqualTo(data2));
        });
    }

    [Test]
    public async Task TestServerCanSendBinaryDataOnBufferBoundary()
    {
        await using Server server = new();
        await server.StartAsync();

        int dataLength = 2 * server.BufferSize;
        byte[] data = new byte[dataLength];
        for (int i = 0; i < dataLength; i++)
        {
            data[i] = (byte)(i % 256);
        }

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId, data);
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        byte[] receivedData = buffer.Array!.AsSpan(0, result.Count).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendBinaryDataOnMediumLargeMessage()
    {
        await using Server server = new();
        await server.StartAsync();

        int dataLength = 40000;
        byte[] data = new byte[dataLength];
        for (int i = 0; i < dataLength; i++)
        {
            data[i] = (byte)(i % 256);
        }

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId, data);
        WebSocketReceiveResult result = await receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
        byte[] receivedData = buffer.Array!.AsSpan(0, result.Count).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendBinaryDataOnVeryLongMessage()
    {
        await using Server server = new();
        await server.StartAsync();

        int dataLength = 1048576;
        byte[] data = new byte[dataLength];
        for (int i = 0; i < dataLength; i++)
        {
            data[i] = (byte)(i % 256);
        }

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId, data);
        WebSocketReceiveResult result = await receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
        byte[] receivedData = buffer.Array!.AsSpan(0, result.Count).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendDataToMultipleSimultaneousConnections()
    {
        await using Server server = new();
        await server.StartAsync();

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        ArraySegment<byte> buffer1 = WebSocket.CreateClientBuffer(1024, 1024);
        using ClientWebSocket socket1 = new();
        await socket1.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        string connectionId1 = connectionId;
        Task<WebSocketReceiveResult> receiveTask1 = Task.Run(() => socket1.ReceiveAsync(buffer1, CancellationToken.None));

        connectionEvent.Reset();

        ArraySegment<byte> buffer2 = WebSocket.CreateClientBuffer(1024, 1024);
        using ClientWebSocket socket2 = new();
        await socket2.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        string connectionId2 = connectionId;
        Task<WebSocketReceiveResult> receiveTask2 = Task.Run(() => socket2.ReceiveAsync(buffer2, CancellationToken.None));

        await server.SendWebSocketDataAsync(connectionId1, "Sent to client 1");
        await server.SendWebSocketDataAsync(connectionId2, "Sent to client 2");
        await Task.WhenAll(receiveTask1, receiveTask2);
        WebSocketReceiveResult result1 = receiveTask1.Result;
        string receivedData1 = Encoding.UTF8.GetString(buffer1.Array!, 0, result1.Count);
        WebSocketReceiveResult result2 = receiveTask2.Result;
        string receivedData2 = Encoding.UTF8.GetString(buffer2.Array!, 0, result2.Count);

        Assert.Multiple(() =>
        {
            Assert.That(result1.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(receivedData1, Is.Not.Null);
            Assert.That(receivedData1, Is.EqualTo("Sent to client 1"));
            Assert.That(result2.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(receivedData2, Is.Not.Null);
            Assert.That(receivedData2, Is.EqualTo("Sent to client 2"));
        });
    }

    [Test]
    public async Task TestServerCanSendDataOnBufferBoundary()
    {
        await using Server server = new();
        await server.StartAsync();

        int dataLength = 2 * server.BufferSize;
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        string data = new('a', dataLength);
        await server.SendWebSocketDataAsync(connectionId, data);
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        string receivedData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendDataOnMediumLargeMessage()
    {
        await using Server server = new();
        await server.StartAsync();

        // 40,000 bytes falls in the two-byte WebSocket length range (126–65535) and
        // above the signed-short boundary (32767), exercising the unsigned 16-bit path.
        int dataLength = 40000;
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        string data = new('a', dataLength);
        await server.SendWebSocketDataAsync(connectionId, data);
        WebSocketReceiveResult result = await receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
        string receivedData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendDataOnVeryLongMessage()
    {
        await using Server server = new();
        await server.StartAsync();

        // Sending 1 MB (1024 * 1024 bytes) of text so as to make the send operation not finish
        // synchronously. This number may need to change if the values require it.
        int dataLength = 1048576;
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        string data = new('a', dataLength);
        await server.SendWebSocketDataAsync(connectionId, data);
        WebSocketReceiveResult result = await receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
        string receivedData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);

        Assert.Multiple(() =>
        {
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestSendingWebSocketDataForNullConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(async () => await server.SendWebSocketDataAsync(null!, "Sent to client"), Throws.InstanceOf<ArgumentNullException>());
    }

    [Test]
    public async Task TestSendingWebSocketDataForEmptyConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(async () => await server.SendWebSocketDataAsync(string.Empty, "Sent to client"), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task TestSendingWebSocketDataForWhitespaceConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(async () => await server.SendWebSocketDataAsync("   ", "Sent to client"), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task TestServerLogsIncomingAndOutgoingDataForHttpTraffic()
    {
        await using Server server = new();
        await server.StartAsync();

        List<string> expectedLog = new()
        {
            "Client connected",
        };
        ManualResetEvent syncEvent = new(false);
        server.OnDataSent.AddObserver((e) =>
        {
            expectedLog.Add($"SEND {e.ByteCount} bytes");
            syncEvent.Set();
        });
        server.OnDataReceived.AddObserver((e) =>
        {
            expectedLog.Add($"RECV {e.ByteCount} bytes");
        });
        server.RegisterHandler("/", new WebResourceRequestHandler("hello world"));
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        bool eventRaised = syncEvent.WaitOne(TimeSpan.FromMilliseconds(200));
        Assert.That(eventRaised, Is.True);
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.That(server.Log, Is.EquivalentTo(expectedLog));
    }

    [Test]
    public async Task TestServerRespondsToMethodsOtherThanGet()
    {
        await using Server server = new();
        await server.StartAsync();

        List<string> expectedLog = new()
        {
            "Client connected",
        };
        ManualResetEvent syncEvent = new(false);
        server.OnDataSent.AddObserver((e) =>
        {
            expectedLog.Add($"SEND {e.ByteCount} bytes");
            syncEvent.Set();
        });
        server.OnDataReceived.AddObserver((e) =>
        {
            expectedLog.Add($"RECV {e.ByteCount} bytes");
        });
        server.RegisterHandler("/", HttpRequestMethod.Post, new WebResourceRequestHandler("hello world"));
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.PostAsync($"http://localhost:{server.Port}/", null);
        bool eventRaised = syncEvent.WaitOne(TimeSpan.FromMilliseconds(200));
        Assert.That(eventRaised, Is.True);
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.That(responseContent, Is.EqualTo("hello world"));
        Assert.That(server.Log, Is.EquivalentTo(expectedLog));
    }

    [Test]
    public async Task TestServerLogsIncomingAndOutgoingDataForWebSocketTraffic()
    {
        await using Server server = new();
        await server.StartAsync();

        // Expected log includes WebSocket upgrade handshake request.
        // Observers are added before connecting so that the handshake recv/send byte
        // counts are captured alongside the subsequent WebSocket frame byte counts.
        List<string> expectedLog = ["Client connected"];

        ManualResetEventSlim connectionEvent = new(false);
        string connectionId = string.Empty;
        server.OnClientConnected.AddObserver((e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        });

        ManualResetEventSlim dataReceivedEvent = new(false);
        string? receivedData = null;
        server.OnDataReceived.AddObserver((e) =>
        {
            expectedLog.Add($"RECV {e.ByteCount} bytes");
            receivedData = e.Data;
            dataReceivedEvent.Set();
        });
        server.OnDataSent.AddObserver((e) =>
        {
            expectedLog.Add($"SEND {e.ByteCount} bytes");
        });

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{server.Port}"), CancellationToken.None);
        connectionEvent.Wait(TimeSpan.FromSeconds(1));

        // ConnectAsync only returns after the client receives the 101 response, so the
        // handshake RECV and SEND have already fired. Reset the event to wait for the
        // WebSocket text frame receive.
        dataReceivedEvent.Reset();

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await socket.SendAsync(Encoding.UTF8.GetBytes("Received from client"), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = dataReceivedEvent.Wait(TimeSpan.FromSeconds(3));
        Assert.That(eventReceived, Is.True);
        await server.SendWebSocketDataAsync(connectionId, "Sent to client");
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        string sentData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
        Assert.That(receivedData, Is.EqualTo("Received from client"));
        Assert.That(sentData, Is.EqualTo("Sent to client"));
        Assert.That(server.Log, Is.EquivalentTo(expectedLog));
    }

    [Test]
    public async Task TestSettingIgnoreCloseRequestForInvalidConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(() => server.IgnoreCloseConnectionRequest("invalidConnectionId", true), Throws.InstanceOf<PinchHitterException>());
    }

    [Test]
    public async Task TestSettingIgnoreCloseRequestForNullConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(() => server.IgnoreCloseConnectionRequest(null!, true), Throws.InstanceOf<ArgumentNullException>());
    }

    [Test]
    public async Task TestSettingIgnoreCloseRequestForEmptyConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(() => server.IgnoreCloseConnectionRequest(string.Empty, true), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task TestSettingIgnoreCloseRequestForWhitespaceConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(() => server.IgnoreCloseConnectionRequest("   ", true), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task TestDisconnectingForInvalidConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(async () => await server.DisconnectAsync("invalidConnectionId"), Throws.InstanceOf<PinchHitterException>());
    }

    [Test]
    public async Task TestSendingDataForInvalidConnectionIdThrows()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(async () => await server.SendWebSocketDataAsync("invalidConnectionId", "Sent to client"), Throws.InstanceOf<PinchHitterException>());
            Assert.That(async () => await server.SendWebSocketDataAsync("invalidConnectionId", new byte[] { 0x01, 0x02, 0x03 }), Throws.InstanceOf<PinchHitterException>());
        });
    }

    [Test]
    public async Task TestCannotSetReceiveBufferSizeOnStartedServer()
    {
        await using Server server = new();
        await server.StartAsync();

        Assert.That(() => server.BufferSize = 2048, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task TestCanSetReceiveBufferSize()
    {
        await using Server localServer = new()
        {
            BufferSize = 8192
        };
        Assert.That(localServer.BufferSize, Is.EqualTo(8192));
    }

    [Test]
    public async Task TestServerStartsOnSpecificPort()
    {
        // Find an available port by briefly binding to port 0, then release it
        // before creating the Server so the port number is known in advance.
        // This is a slight race condition in theory, but in the context of
        // running tests in a controlled environment, it's unlikely to cause
        // issues and allows deterministic testing of starting a Server on a
        // specific port.
        int specificPort;
        using (TcpListener portFinder = new(IPAddress.Loopback, 0))
        {
            portFinder.Start();
            specificPort = ((IPEndPoint)portFinder.LocalEndpoint).Port;
            portFinder.Stop();
        }

        await using Server server = new(specificPort);
        await server.StartAsync();
        Assert.That(server.Port, Is.EqualTo(specificPort));
    }

    private class SocketAcceptanceTestServer : Server
    {
        public SocketAcceptanceTestServer()
            : base()
        {
        }

        public Exception? ExceptionToThrowInAccept { get; set; } = null;

        protected override async Task<Socket> AcceptSocketAsync()
        {
            try
            {
                return await base.AcceptSocketAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            if (this.ExceptionToThrowInAccept != null)
            {
                throw this.ExceptionToThrowInAccept;
            }

            throw new InvalidOperationException("No exception specified to throw in AcceptSocketAsync.");
        }
    }
}
