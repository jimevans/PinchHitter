namespace PinchHitter;

using System.Net;
using System.Text;

[TestFixture]
public class WebResourceTests
{
    [Test]
    public void TestCanCreateWebResource()
    {
        WebResource resource = new("hello world");
        Assert.Multiple(() =>
        {
            Assert.That(resource.IsRedirect, Is.False);
            Assert.That(resource.RequiresAuthentication, Is.False);
            Assert.That(resource.MimeType, Is.EqualTo("text/html;charset=utf-8"));
            Assert.That(resource.Data, Is.EquivalentTo(Encoding.UTF8.GetBytes("hello world")));
        });
    }

    [Test]
    public void TestCanAuthenticateWhenNoAuthenticationRequired()
    {
        WebResource resource = new("hello world");
        Assert.That(resource.TryAuthenticate(string.Empty), Is.True);
    }

    [Test]
    public void TestCanAuthenticate()
    {
        WebResource resource = new("hello world");
        resource.AddAuthenticator(new TestAuthenticator());
        Assert.That(resource.TryAuthenticate("Valid authentication"), Is.True);
    }

    [Test]
    public void TestAuthenticateCanRejectWithUnauthorizedInput()
    {
        WebResource resource = new("hello world");
        resource.AddAuthenticator(new TestAuthenticator());
        Assert.That(resource.TryAuthenticate(string.Empty), Is.False);
    }

    [Test]
    public void TestCanCreateHtmlResource()
    {
        string headContent = "<title>Page Title</title>";
        string bodyContent = "hello world";
        string expected = $"<html><head>{headContent}</head><body>{bodyContent}</body></html>";
        WebResource resource = WebResource.CreateHtmlResource(bodyContent, headContent);
        Assert.That(resource.Data, Is.EquivalentTo(Encoding.UTF8.GetBytes(expected)));
    }

    [Test]
    public void TestCanCreateWebSocketResponseResource()
    {
        WebResource resource = WebResource.CreateWebSocketHandshakeResponse("AWebSocketSecurityKey");
        Assert.That(resource.Data, Is.Empty);
    }

    [Test]
    public void TestCanCreateHttpResponse()
    {
        byte[] content = Encoding.UTF8.GetBytes("hello world");
        WebResource resource = new(content);
        HttpResponse response = resource.CreateHttpResponse();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers, Has.Count.EqualTo(5));
            Assert.That(response.Headers, Contains.Key("Connection"));
            Assert.That(response.Headers["Connection"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Connection"][0], Is.EqualTo("keep-alive"));
            Assert.That(response.Headers, Contains.Key("Server"));
            Assert.That(response.Headers["Server"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Server"][0], Is.EqualTo("PinchHitter/0.1 .NET/6.0"));
            Assert.That(response.Headers, Contains.Key("Date"));
            Assert.That(response.Headers["Date"], Has.Count.EqualTo(1));
            Assert.That(response.Headers, Contains.Key("Content-Type"));
            Assert.That(response.Headers["Content-Type"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Content-Type"][0], Is.EqualTo("text/html;charset=utf-8"));
            Assert.That(response.Headers, Contains.Key("Content-Length"));
            Assert.That(response.Headers["Content-Length"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Content-Length"][0], Is.EqualTo(content.Length.ToString()));
            Assert.That(response.BodyContent, Is.EquivalentTo(content));
        });
    }

    [Test]
    public void TestCanCreateHttpResponseWithStatusCode()
    {
        byte[] content = Encoding.UTF8.GetBytes("hello world");
        WebResource resource = new(content);
        HttpResponse response = resource.CreateHttpResponse(HttpStatusCode.NotFound);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Headers, Has.Count.EqualTo(5));
            Assert.That(response.Headers, Contains.Key("Connection"));
            Assert.That(response.Headers["Connection"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Connection"][0], Is.EqualTo("keep-alive"));
            Assert.That(response.Headers, Contains.Key("Server"));
            Assert.That(response.Headers["Server"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Server"][0], Is.EqualTo("PinchHitter/0.1 .NET/6.0"));
            Assert.That(response.Headers, Contains.Key("Date"));
            Assert.That(response.Headers["Date"], Has.Count.EqualTo(1));
            Assert.That(response.Headers, Contains.Key("Content-Type"));
            Assert.That(response.Headers["Content-Type"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Content-Type"][0], Is.EqualTo("text/html;charset=utf-8"));
            Assert.That(response.Headers, Contains.Key("Content-Length"));
            Assert.That(response.Headers["Content-Length"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Content-Length"][0], Is.EqualTo(content.Length.ToString()));
            Assert.That(response.BodyContent, Is.EquivalentTo(content));
        });
    }

    [Test]
    public void TestCanCreateHttpResponseForWebSocketHandshake()
    {
        WebResource resource = WebResource.CreateWebSocketHandshakeResponse("AWebSocketSecurityKey");
        HttpResponse response = resource.CreateHttpResponse();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SwitchingProtocols));
            Assert.That(response.Headers, Has.Count.EqualTo(7));
            Assert.That(response.Headers, Contains.Key("Connection"));
            Assert.That(response.Headers["Connection"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Connection"][0], Is.EqualTo("Upgrade"));
            Assert.That(response.Headers, Contains.Key("Server"));
            Assert.That(response.Headers["Server"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Server"][0], Is.EqualTo("PinchHitter/0.1 .NET/6.0"));
            Assert.That(response.Headers, Contains.Key("Date"));
            Assert.That(response.Headers["Date"], Has.Count.EqualTo(1));
            Assert.That(response.Headers, Contains.Key("Content-Type"));
            Assert.That(response.Headers["Content-Type"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Content-Type"][0], Is.EqualTo("text/html;charset=utf-8"));
            Assert.That(response.Headers, Contains.Key("Content-Length"));
            Assert.That(response.Headers["Content-Length"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Content-Length"][0], Is.EqualTo("0"));
            Assert.That(response.Headers, Contains.Key("Upgrade"));
            Assert.That(response.Headers["Upgrade"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Upgrade"][0], Is.EqualTo("websocket"));
            Assert.That(response.Headers, Contains.Key("Sec-WebSocket-Accept"));
            Assert.That(response.Headers["Sec-WebSocket-Accept"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Sec-WebSocket-Accept"][0], Is.EqualTo("QsbTE0fhpQ8hqOTV5cBS5qENwmQ="));
            Assert.That(response.BodyContent, Is.Empty);
        });
    }

    private class TestAuthenticator : WebAuthenticator
    {
        public override bool IsAuthenticated(string authCandidate)
        {
            // If anything not null or empty is passed in, assume authenticated.
            return !string.IsNullOrEmpty(authCandidate);
        }
    }
}
