namespace PinchHitter;

using System.Net;

[TestFixture]
public class WebServerTests
{
    private WebServer? server;

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
    public async Task TestCanServeKnownResources()
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
    public async Task TestCanInterceptIncomingRequests()
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
    public void TestShutdownWithoutReceivingRequest()
    {
        this.server!.Stop();
    }

    [Test]
    public async Task TestWebServerLogsIncomingAndOutgoingData()
    {
        List<string> expectedLog = new()
        { 
            "Socket connected",
            "RECV 41 bytes",
            "SEND 223 bytes"
        };
        this.server!.RegisterResource("/", WebResource.CreateHtmlResource("hello world"));
        using HttpClient client = new();
        HttpResponseMessage responseMessage = await client.GetAsync($"http://localhost:{server.Port}/");
        string responseContent = await responseMessage.Content.ReadAsStringAsync();
        Assert.That(this.server.Log, Is.EquivalentTo(expectedLog));
    }

    [Test]
    public void TestCannotSetReceiveBufferSizeOnStartedServer()
    {
        Assert.That(() => this.server!.BufferSize = 2048, Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void TestCanSetReceiveBufferSize()
    {
        WebServer localServer = new()
        {
            BufferSize = 8192
        };
        Assert.That(localServer.BufferSize, Is.EqualTo(8192));
    }
}