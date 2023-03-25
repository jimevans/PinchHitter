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
        HttpResponse response = processor.ProcessRequest(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void TestProcessNotFoundRequest()
    {
        HttpRequestProcessor processor = new();
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public void TestProcessRedirectRequest()
    {
        HttpRequestProcessor processor = new();
        processor.RegisterHandler("/", new RedirectRequestHandler("/index.html"));
        _  = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        HttpResponse response = processor.ProcessRequest(request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(response.Headers, Contains.Key("Location"));
            Assert.That(response.Headers["Location"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Location"][0], Is.EqualTo("/index.html"));
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
        HttpResponse response = processor.ProcessRequest(request);
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
        HttpResponse response = processor.ProcessRequest(request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(Encoding.UTF8.GetString(response.BodyContent), Is.EqualTo("hello world"));
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
        HttpResponse response = processor.ProcessRequest(request);
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
        HttpResponse response = processor.ProcessRequest(request);
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
        HttpResponse response = processor.ProcessRequest(request);
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
        HttpResponse response = processor.ProcessRequest(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SwitchingProtocols));
    }
}