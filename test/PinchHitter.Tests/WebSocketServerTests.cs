namespace PinchHitter;

using System.Net;
using System.Net.WebSockets;
using System.Text;

[TestFixture]
public class WebSocketServerTests
{
    private WebSocketServer? server;

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
    public async Task TestServerCanRespondToCloseRequest()
    {
        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }

    [Test]
    public async Task TestServerCanSimulateIgnoringCloseRequest()
    {
        server!.IgnoreCloseRequest = true;
        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server.Port}"), CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(async () => await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None), Throws.InstanceOf<WebSocketException>().With.Property("WebSocketErrorCode").EqualTo(WebSocketError.ConnectionClosedPrematurely));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Aborted));
        });
    }

    [Test]
    public async Task TestServerCanInitiateCloseRequest()
    {
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));
        await server.Disconnect();
        await receiveTask;
        Assert.Multiple(() =>
        {
            Assert.That(receiveTask.Result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.CloseReceived));
        });
    }

    [Test]
    public async Task TestServerCanReceiveDataFromClient()
    {
        ManualResetEvent syncEvent = new(false);
        string? receivedData = null;
        this.server!.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        };
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes("Received from client"), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.WaitOne(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo("Received from client"));
        });
    }

    [Test]
    public async Task TestServerSendDataToClient()
    {
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await server.SendData("Sent to client");
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
    public async Task TestServerDisconnectIsNoOpWithoutConnectedWebSocket()
    {
        this.server!.RegisterResource("/", WebResource.CreateHtmlResource("hello world"));
        await this.server!.Disconnect();
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
    public async Task TestServerCanReceiveDataOnBufferBoundary()
    {
        int dataLength = 2 * server!.BufferSize;
        ManualResetEvent syncEvent = new(false);
        string? receivedData = null;
        this.server!.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        };
        string data = new('a', dataLength);
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
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
        this.server!.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        };
        string data = new('a', dataLength);
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.WaitOne(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(receivedData, Is.EqualTo(data));
        });
    }

    [Test]
    public async Task TestServerCanSendDataOnBufferBoundary()
    {
        int dataLength = 2 * server!.BufferSize;
        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(dataLength, dataLength);

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server.Port}"), CancellationToken.None);
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        string data = new('a', dataLength);
        await server.SendData(data);
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

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        string data = new('a', dataLength);
        await server.SendData(data);
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
    public async Task TestWebSocketServerLogsIncomingAndOutgoingData()
    {
        // Expected log includes WebSocket upgrade handshake request.
        List<string> expectedLog = new()
        { 
            "Socket connected",
            "RECV 154 bytes",
            "SEND 258 bytes",
            "RECV 26 bytes",
            "SEND 16 bytes"
        };

        using ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri($"ws://localhost:{this.server!.Port}"), CancellationToken.None);

        ManualResetEvent syncEvent = new(false);
        string? receivedData = null;
        this.server!.DataReceived += (sender, e) =>
        {
            receivedData = e.Data;
            syncEvent.Set();
        };

        ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
        Task<WebSocketReceiveResult> receiveTask = Task.Run(() => socket.ReceiveAsync(buffer, CancellationToken.None));

        await socket.SendAsync(Encoding.UTF8.GetBytes("Received from client"), WebSocketMessageType.Text, true, CancellationToken.None);
        bool eventReceived = syncEvent.WaitOne(TimeSpan.FromSeconds(1));
        await server.SendData("Sent to client");
        await receiveTask;
        WebSocketReceiveResult result = receiveTask.Result;
        string sentData = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
        Assert.That(this.server.Log, Is.EquivalentTo(expectedLog));
    }

    [Test]
    public void TestSendingDataWithClosedConnectionThrows()
    {
        Assert.That(async () => await this.server!.SendData("Sent to client"), Throws.InstanceOf<PinchHitterException>());
    }

    [Test]
    public void TestCannotSetReceiveBufferSizeOnStartedServer()
    {
        Assert.That(() => this.server!.BufferSize = 2048, Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void TestCanSetReceiveBufferSize()
    {
        WebSocketServer localServer = new()
        {
            BufferSize = 8192
        };
        Assert.That(localServer.BufferSize, Is.EqualTo(8192));
    }
}