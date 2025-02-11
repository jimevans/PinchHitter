namespace PinchHitter;

using System.Net;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class AuthenticatedResourceRequestHandlerTests
{
    [Test]
    public async Task TestHandlerWithValidAuthorizationValueHeaderReturnsContent()
    {
        string userName = "goodUserName";
        string password = "GoodP@ssw0rd!";
        string authorizationHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));
        AuthenticatedResourceRequestHandler handler = new("content");
        handler.AddAuthenticator(new BasicWebAuthenticator(userName, password));

        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        request.Headers.Add("Authorization", new List<string>() { $"Basic {authorizationHeaderValue}" });
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.BodyContent, Is.EqualTo("content"));
        });
    }

    [Test]
    public async Task TestHandlerWithInvalidAuthorizationValueHeaderReturnsForbiddenResponse()
    {
        string userName = "goodUserName";
        string password = "GoodP@ssw0rd!";
        AuthenticatedResourceRequestHandler handler = new("content");
        handler.AddAuthenticator(new BasicWebAuthenticator(userName, password));

        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        request.Headers.Add("Authorization", new List<string>() { $"Basic NotAValidHeaderValue" });
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task TestHandlerWithoutAuthenticatorsReturnsContent()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        // Disable spell checker for bogus value.
        // cspell: disable-next
        request.Headers.Add("Authorization", new List<string>() { "Basic aninvalidvaluebutusableforthistest" });
        AuthenticatedResourceRequestHandler handler = new("content");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.BodyContent, Is.EqualTo("content"));
        });
    }

    [Test]
    public async Task TestHandlerWithEmptyAuthorizationHeaderReturnsBadRequest()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        request.Headers.Add("Authorization", new List<string>());
        AuthenticatedResourceRequestHandler handler = new("content");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task TestRequestWithoutAuthenticationHeaderReturnsAuthChallengeResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        AuthenticatedResourceRequestHandler handler = new("content");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Headers, Contains.Key("Www-Authenticate"));
            Assert.That(response.Headers["Www-Authenticate"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Www-Authenticate"][0], Is.EqualTo("Basic"));
        });
    }
}