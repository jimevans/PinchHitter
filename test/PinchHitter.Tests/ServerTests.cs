namespace PinchHitter;

using System.Net;
using System.Net.WebSockets;
using System.Text;

[TestFixture]
public class ServerTests
{
    private Server? server;

    [SetUp]
    public void Setup()
    {
        this.server = new();
        this.server.Start();
    }

    [TearDown]
    public void TearDown()
    {
        this.server?.Stop();
        this.server = null;
    }

    [Test]
    public async Task TestServerCanProcessHttpRequests()
    {
        this.server!.RegisterResource("/", WebResource.CreateHtmlResource("hello world"));
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
        this.server!.RegisterResource("/", WebResource.CreateHtmlResource("hello world"));

        string receivedData = string.Empty;
        this.server.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
        };
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.That(receivedData, Does.StartWith("GET / HTTP/1.1"));
    }

    [Test]
    public async Task TestServerCanInitiateCloseForHttpConnection()
    {
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        ManualResetEvent disconnectionEvent = new(false);
        server.ClientDisconnected += (sender, e) =>
        {
            if (e.ConnectionId == connectionId)
            {
                disconnectionEvent.Set();
            }
        };

        this.server!.RegisterResource("/", WebResource.CreateHtmlResource("hello world"));
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        string responseContent = await responseMessage.Content.ReadAsStringAsync();

        await this.server.Disconnect(connectionId);
        bool disconnectEventRaised = disconnectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(disconnectEventRaised, Is.True);
            Assert.That(async () => await this.server.Disconnect(connectionId), Throws.InstanceOf<PinchHitterException>());
        });
    }

    [Test]
    public void TestShutdownWithoutReceivingRequest()
    {
        this.server!.Stop();
    }

    [Test]
    public async Task TestServerCanRespondToWebSocketCloseRequest()
    {
        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }

    [Test]
    public async Task TestServerCanSimulateIgnoringWebSocketCloseRequest()
    {
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));

        server.IgnoreCloseConnectionRequest(connectionId, true);
        Assert.Multiple(() =>
        {
            Assert.That(async () => await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None), Throws.InstanceOf<WebSocketException>().With.Property("WebSocketErrorCode").EqualTo(WebSocketError.ConnectionClosedPrematurely));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Aborted));
        });
    }

    [Test]
    public async Task TestServerCanInitiateWebSocketCloseRequest()
    {
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));

        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));
        await server.Disconnect(connectionId);
        await receiveTask;
        Assert.Multiple(() =>
        {
            Assert.That(receiveTask.Result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.CloseReceived));
        });
    }

    [Test]
    public async Task TestServerCanReceiveWebSocketDataFromClient()
    {
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));

        ManualResetEvent syncEvent = new(false);
        string? receivedData = null;
        string receivedDataConnectionId = string.Empty;
        this.server!.DataReceived += (sender, e) =>
        {
            receivedDataConnectionId = e.ConnectionId;
            receivedData = e.Data;
            syncEvent.Set();
        };

        await socket.SendAsync(Encoding.UTF8.GetBytes("Received from client"), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.WaitOne(TimeSpan.FromSeconds(1));
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
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        this.server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        using ClientWebSocket socket1 = new();
        await socket1.ConnectAsync(new Uri($"ws://localhost:{this.server.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        string connectionId1 = connectionId;

        connectionEvent.Reset();
        using ClientWebSocket socket2 = new();
        await socket2.ConnectAsync(new Uri($"ws://localhost:{this.server.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        string connectionId2 = connectionId;

        ManualResetEvent receivedDataEvent = new(false);
        List<string> receivedData = new();
        this.server.DataReceived += (sender, e) =>
        {
            receivedData.Add($"{e.ConnectionId}: {e.Data}");
            receivedDataEvent.Set();
        };

        await socket1.SendAsync(Encoding.UTF8.GetBytes("Sent from client 1"), WebSocketMessageType.Text, true, CancellationToken.None);
        receivedDataEvent.WaitOne(TimeSpan.FromSeconds(1));

        receivedDataEvent.Reset();
        await socket2.SendAsync(Encoding.UTF8.GetBytes("Sent from client 2"), WebSocketMessageType.Text, true, CancellationToken.None);
        receivedDataEvent.WaitOne(TimeSpan.FromSeconds(1));
        Assert.That(receivedData, Is.EquivalentTo(new List<string>() { $"{connectionId1}: Sent from client 1", $"{connectionId2}: Sent from client 2" }));
    }

    [Test]
    public async Task TestServerCanReceiveDataOnBufferBoundary()
    {
        int dataLength = 2 * server!.BufferSize;
        ManualResetEvent syncEvent = new(false);
        string? receivedData = null;
        string data = new('a', dataLength);
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        this.server!.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        };
        await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.WaitOne(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanReceiveDataOnVeryLongMessage()
    {
        int dataLength = 70000;
        ManualResetEvent syncEvent = new(false);
        string? receivedData = null;
        string data = new('a', dataLength);
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        this.server!.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        };
        await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.WaitOne(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendWebSocketDataToClient()
    {
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendData(connectionId, "Sent to client");
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
    public async Task TestServerCanSendDataToMultipleSimultaneousConnections()
    {
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        ArraySegment<byte> buffer1 = WebSocket.CreateClientBuffer(1024, 1024);
        using ClientWebSocket socket1 = new();
        await socket1.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        string connectionId1 = connectionId;
        Task<WebSocketReceiveResult> receiveTask1 = Task.Run(() => socket1.ReceiveAsync(buffer1, CancellationToken.None));

        connectionEvent.Reset();

        ArraySegment<byte> buffer2 = WebSocket.CreateClientBuffer(1024, 1024);
        using ClientWebSocket socket2 = new();
        await socket2.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        string connectionId2 = connectionId;
        Task<WebSocketReceiveResult> receiveTask2 = Task.Run(() => socket2.ReceiveAsync(buffer2, CancellationToken.None));

        await server.SendData(connectionId1, "Sent to client 1");
        await server.SendData(connectionId2, "Sent to client 2");
        Task.WaitAll(receiveTask1, receiveTask2);
        WebSocketReceiveResult result1 = receiveTask1.Result;
        string receivedData1 = Encoding.UTF8.GetString(buffer1.Array!, 0, result1.Count);
        WebSocketReceiveResult result2 = receiveTask1.Result;
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
        int dataLength = 2 * server!.BufferSize;
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        string data = new('a', dataLength);
        await server.SendData(connectionId, data);
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
    public async Task TestServerCanSendDataOnVeryLongMessage()
    {
        int dataLength = 70000;
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);
        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        string data = new('a', dataLength);
        await server.SendData(connectionId, data);
        receiveTask.Wait(TimeSpan.FromSeconds(5));
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
    public async Task TestServerLogsIncomingAndOutgoingDataForHttpTraffic()
    {
        List<string> expectedLog = new()
        {
            "Client connected",
            "RECV 41 bytes",
            "SEND 238 bytes"
        };
        this.server!.RegisterResource("/", WebResource.CreateHtmlResource("hello world"));
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.That(this.server.Log, Is.EquivalentTo(expectedLog));
    }

    [Test]
    public async Task TestServerLogsIncomingAndOutgoingDataForWebSocketTraffic()
    {
        // Expected log includes WebSocket upgrade handshake request.
        List<string> expectedLog = new()
        { 
            "Client connected",
            "RECV 154 bytes",
            "SEND 258 bytes",
            "RECV 26 bytes",
            "SEND 16 bytes"
        };

        ManualResetEvent connectionEvent = new(false);
        string connectionId = string.Empty;
        server!.ClientConnected += (sender, e) =>
        {
            connectionId = e.ConnectionId;
            connectionEvent.Set();
        };

        ManualResetEvent syncEvent = new(false);
        string? receivedData = null;
        this.server!.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        };

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        connectionEvent.WaitOne(TimeSpan.FromSeconds(1));

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await socket.SendAsync(Encoding.UTF8.GetBytes("Received from client"), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.WaitOne(TimeSpan.FromSeconds(1));
        await server.SendData(connectionId, "Sent to client");
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        string sentData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
        Assert.That(this.server.Log, Is.EquivalentTo(expectedLog));
    }

    [Test]
    public void TestSettingIgnoreCloseRequestForInvalidConnectionIdThrows()
    {
        Assert.That(() => this.server!.IgnoreCloseConnectionRequest("invalidConnectionId", true), Throws.InstanceOf<PinchHitterException>());
    }

    [Test]
    public void TestDisconnectingForInvalidConnectionIdThrows()
    {
        Assert.That(async () => await this.server!.Disconnect("invalidConnectionId"), Throws.InstanceOf<PinchHitterException>());
    }

    [Test]
    public void TestSendingDataForInvalidConnectionIdThrows()
    {
        Assert.That(async () => await this.server!.SendData("invalidConnectionId", "Sent to client"), Throws.InstanceOf<PinchHitterException>());
    }

    [Test]
    public void TestCannotSetReceiveBufferSizeOnStartedServer()
    {
        Assert.That(() => this.server!.BufferSize = 2048, Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void TestCanSetReceiveBufferSize()
    {
        Server localServer = new()
        {
            BufferSize = 8192
        };
        Assert.That(localServer.BufferSize, Is.EqualTo(8192));
    }
}