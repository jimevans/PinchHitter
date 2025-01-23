namespace PinchHitter;

using System.Net;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class HttpRequestProcessorTests
{
    [Test]
    public async Task TestProcessRequest()
    {
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", new WebResourceRequestHandler("Hello world"));
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task TestProcessingRequestRaisesRequestHandlingEvent()
    {
        ManualResetEventSlim syncEvent = new(false);
        HttpRequestHandler handler = new WebResourceRequestHandler("Hello world");
        handler.OnRequestHandling.AddObserver((e) => {
            Assert.Multiple(() =>
            {
                Assert.That(e.ConnectionId, Is.EqualTo("connectionId"));
                Assert.That(e.RequestId, Is.Not.Empty);
                Assert.That(e.HttpVersion, Is.EqualTo("HTTP/1.1"));
                Assert.That(e.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(e.Uri.ToString(), Is.EqualTo("http://example.com/"));
                Assert.That(e.Headers, Has.Count.EqualTo(1));
                Assert.That(e.Headers, Contains.Key("Host"));
                Assert.That(e.Body, Is.Empty);
            });
            syncEvent.Set();
        });

        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", handler);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        bool eventRaised = syncEvent.Wait(TimeSpan.FromSeconds(1));
        Assert.That(eventRaised, Is.EqualTo(true));
    }

    [Test]
    public async Task TestProcessingRequestRaisesRequestHandledEvent()
    {
        ManualResetEventSlim syncEvent = new(false);
        HttpRequestHandler handler = new WebResourceRequestHandler("Hello world");
        handler.OnRequestHandled.AddObserver((e) => {
            Assert.Multiple(() =>
            {
                Assert.That(e.ConnectionId, Is.EqualTo("connectionId"));
                Assert.That(e.RequestId, Is.Not.Empty);
                Assert.That(e.HttpVersion, Is.EqualTo("HTTP/1.1"));
                Assert.That(e.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(e.ReasonPhrase, Is.EqualTo("OK"));
                Assert.That(e.Headers, Has.Count.EqualTo(5));
                Assert.That(e.Headers, Contains.Key("Connection"));
                Assert.That(e.Headers, Contains.Key("Server"));
                Assert.That(e.Headers, Contains.Key("Date"));
                Assert.That(e.Headers, Contains.Key("Content-Type"));
                Assert.That(e.Headers, Contains.Key("Content-Length"));
                Assert.That(e.Body, Is.EqualTo("Hello world"));
            });
            syncEvent.Set();
        });

        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", handler);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        bool eventRaised = syncEvent.Wait(TimeSpan.FromSeconds(1));
        Assert.That(eventRaised, Is.EqualTo(true));
    }

    [Test]
    public async Task TestProcessNotFoundRequest()
    {
        HttpRequestProcessor processor = new();
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task TestProcessRedirectRequest()
    {
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", new RedirectRequestHandler("/index.html"));
        _  = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(response.Headers, Contains.Key("Location"));
            Assert.That(response.Headers["Location"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Location"][0], Is.EqualTo("/index.html"));
        });
    }

    [Test]
    public async Task TestProcessInvalidMethodRequest()
    {
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", HttpMethod.Post, new WebResourceRequestHandler("hello"));
        processor.RegisterHandler("/", HttpMethod.Delete, new WebResourceRequestHandler("world"));
        _  = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(response.Headers, Contains.Key("Allow"));
            Assert.That(response.Headers["Allow"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Allow"][0], Is.EqualTo("DELETE, POST"));
        });
    }

    [Test]
    public async Task TestProcessRequestNeedingAuthentication()
    {
        AuthenticatedResourceRequestHandler resource = new("hello world");
        resource.AddAuthenticator(new BasicWebAuthenticator("userName", "P@ssw0rd!"));
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", resource);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Headers, Contains.Key("Www-Authenticate"));
            Assert.That(response.Headers["Www-Authenticate"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Www-Authenticate"][0], Is.EqualTo("Basic"));
        });
    }

    [Test]
    public async Task TestProcessRequestWithValidAuthorizationResponse()
    {
        string userName = "userName";
        string password = "P@ssw0rd!";
        string base64AuthHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));

        AuthenticatedResourceRequestHandler resource = new("hello world");
        resource.AddAuthenticator(new BasicWebAuthenticator(userName, password));
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", resource);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        request.Headers["Authorization"] = new List<string>() { $"Basic {base64AuthHeaderValue}" };
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.BodyContent, Is.EqualTo("hello world"));
        });
    }

    [Test]
    public async Task TestProcessRequestWithNonAuthorizingResponse()
    {
        string userName = "userName";
        string password = "P@ssw0rd!";
        string base64AuthHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:invalid{password}"));

        AuthenticatedResourceRequestHandler resource = new("hello world");
        resource.AddAuthenticator(new BasicWebAuthenticator(userName, password));
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", resource);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        request.Headers["Authorization"] = new List<string>() { $"Basic {base64AuthHeaderValue}" };
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task TestProcessRequestWithInvalidAuthorizationResponse()
    {
        string userName = "userName";
        string password = "P@ssw0rd!";
        string base64AuthHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));

        AuthenticatedResourceRequestHandler resource = new("hello world");
        resource.AddAuthenticator(new BasicWebAuthenticator(userName, password));
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", resource);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        request.Headers["Authorization"] = new List<string>();
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task TestProcessMalformedRequestResponse()
    {
        WebResourceRequestHandler resource = new("hello world");
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", resource);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\n\r\n", out HttpRequest request);
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task TestProcessWebSocketUpgradeRequest()
    {
        HttpRequestProcessor processor = new();
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        request.Headers["Connection"] = new List<string>() { "Upgrade" };
        request.Headers["Upgrade"] = new List<string>() { "websocket" };
        request.Headers["Sec-WebSocket-Key"] = new List<string>() { "AWebSocketSecurityKey" };
        HttpResponse response = await processor.ProcessRequestAsync("connectionId", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SwitchingProtocols));
    }
}