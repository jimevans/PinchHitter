namespace PinchHitter;

using System.Net;
using System.Text;

[TestFixture]
public class HttpRequestProcessorTests
{
    [Test]
    public void TestProcessRequest()
    {
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", new WebResourceRequestHandler("Hello world"));
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void TestProcessingRequestRaisesRequestHandlingEvent()
    {
        ManualResetEventSlim syncEvent = new(false);
        HttpRequestHandler handler = new WebResourceRequestHandler("Hello world");
        handler.RequestHandling += (sender, e) => {
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
        };

        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", handler);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        bool eventRaised = syncEvent.Wait(TimeSpan.FromSeconds(1));
        Assert.That(eventRaised, Is.EqualTo(true));
    }

    [Test]
    public void TestProcessingRequestRaisesRequestHandledEvent()
    {
        ManualResetEventSlim syncEvent = new(false);
        HttpRequestHandler handler = new WebResourceRequestHandler("Hello world");
        handler.RequestHandled += (sender, e) => {
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
        };

        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", handler);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        bool eventRaised = syncEvent.Wait(TimeSpan.FromSeconds(1));
        Assert.That(eventRaised, Is.EqualTo(true));
    }

    [Test]
    public void TestProcessNotFoundRequest()
    {
        HttpRequestProcessor processor = new();
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public void TestProcessRedirectRequest()
    {
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", new RedirectRequestHandler("/index.html"));
        _  = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(response.Headers, Contains.Key("Location"));
            Assert.That(response.Headers["Location"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Location"][0], Is.EqualTo("/index.html"));
        });
    }

    [Test]
    public void TestProcessInvalidMethodRequest()
    {
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", HttpMethod.Post, new WebResourceRequestHandler("hello"));
        processor.RegisterHandler("/", HttpMethod.Delete, new WebResourceRequestHandler("world"));
        _  = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(response.Headers, Contains.Key("Allow"));
            Assert.That(response.Headers["Allow"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Allow"][0], Is.EqualTo("DELETE, POST"));
        });
    }

    [Test]
    public void TestProcessRequestNeedingAuthentication()
    {
        AuthenticatedResourceRequestHandler resource = new("hello world");
        resource.AddAuthenticator(new BasicWebAuthenticator("userName", "P@ssw0rd!"));
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", resource);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Headers, Contains.Key("Www-Authenticate"));
            Assert.That(response.Headers["Www-Authenticate"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Www-Authenticate"][0], Is.EqualTo("Basic"));
        });
    }

    [Test]
    public void TestProcessRequestWithValidAuthorizationResponse()
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
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.BodyContent, Is.EqualTo("hello world"));
        });
    }

    [Test]
    public void TestProcessRequestWithNonAuthorizingResponse()
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
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public void TestProcessRequestWithInvalidAuthorizationResponse()
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
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public void TestProcessMalformedRequestResponse()
    {
        WebResourceRequestHandler resource = new("hello world");
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", resource);
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public void TestProcessWebSocketUpgradeRequest()
    {
        HttpRequestProcessor processor = new();
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        request.Headers["Connection"] = new List<string>() { "Upgrade" };
        request.Headers["Upgrade"] = new List<string>() { "websocket" };
        request.Headers["Sec-WebSocket-Key"] = new List<string>() { "AWebSocketSecurityKey" };
        HttpResponse response = processor.ProcessRequest("connectionId", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SwitchingProtocols));
    }
}