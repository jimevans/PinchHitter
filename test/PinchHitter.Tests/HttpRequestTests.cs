namespace PinchHitter;

[TestFixture]
public class HttpRequestTests
{
    [Test]
    public void TestCanParseHttpRequest()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\nHello world", out HttpRequest request);
        Assert.Multiple(() =>
        {
            Assert.That(request.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/"));
            Assert.That(request.Headers, Has.Count.EqualTo(2));
            Assert.That(request.Headers, Contains.Key("Host"));
            Assert.That(request.Headers["Host"], Has.Count.EqualTo(1));
            Assert.That(request.Headers["Host"][0], Is.EqualTo("example.com"));
            Assert.That(request.Headers, Contains.Key("User-Agent"));
            Assert.That(request.Headers["User-Agent"], Has.Count.EqualTo(1));
            Assert.That(request.Headers["User-Agent"][0], Is.EqualTo("Test User Agent"));
            Assert.That(request.Body, Is.EqualTo("Hello world"));
            Assert.That(request.IsWebSocketHandshakeRequest, Is.False);
        });
    }

    [Test]
    public void TestCanParseHttpRequestWithRepeatedHeaders()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nCookie:name1;value1\r\nCookie:name2;value2\r\n\r\nHello world", out HttpRequest request);
        Assert.Multiple(() =>
        {
            Assert.That(request.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/"));
            Assert.That(request.Headers, Has.Count.EqualTo(2));
            Assert.That(request.Headers, Contains.Key("Host"));
            Assert.That(request.Headers["Host"], Has.Count.EqualTo(1));
            Assert.That(request.Headers["Host"][0], Is.EqualTo("example.com"));
            Assert.That(request.Headers, Contains.Key("Cookie"));
            Assert.That(request.Headers["Cookie"], Has.Count.EqualTo(2));
            Assert.That(request.Headers["Cookie"][0], Is.EqualTo("name1;value1"));
            Assert.That(request.Headers["Cookie"][1], Is.EqualTo("name2;value2"));
            Assert.That(request.Body, Is.EqualTo("Hello world"));
        });
    }

    [Test]
    public void TestCanParseHttpRequestWithMultipleBodySegments()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\nHello world\r\nAnd good day", out HttpRequest request);
        Assert.Multiple(() =>
        {
            Assert.That(request.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/"));
            Assert.That(request.Body, Is.EqualTo("Hello world\nAnd good day"));
        });
    }

    [Test]
    public void TestCanDetectWebSocketHandshakeRequest()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nConnection:Upgrade\r\nUpgrade:websocket\r\nSec-WebSocket-Key:AWebSocketKey\r\n\r\nHello world", out HttpRequest request);
        Assert.That(request.IsWebSocketHandshakeRequest, Is.True);
    }

    [Test]
    public void TestNoHostHeaderFailsToParse()
    {
        bool parsed = HttpRequest.TryParse("GET / HTTP/1.1\r\n\r\nHello world\r\nAnd good day", out HttpRequest _);
        Assert.That(parsed, Is.False);
    }

    [Test]
    public void TestMultipleHostHeadersFailsToParse()
    {
        bool parsed = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nHost: busted.example.com\r\n\r\nHello world\r\nAnd good day", out HttpRequest _);
        Assert.That(parsed, Is.False);
    }

    [Test]
    public void TestMalformedInitialLineFailsToParse()
    {
        bool parsed = HttpRequest.TryParse("GET HTTP/1.1\r\nHost: example.com\r\n\r\nHello world\r\nAnd good day", out HttpRequest _);
        Assert.That(parsed, Is.False);
    }

    [Test]
    public void TestInvalidMethodNameFailsToParse()
    {
        bool parsed = HttpRequest.TryParse("INVALID / HTTP/1.1\r\nHost: example.com\r\n\r\nHello world\r\nAnd good day", out HttpRequest _);
        Assert.That(parsed, Is.False);
    }

    [Test]
    public void TestInvalidUrlFailsToParse()
    {
        bool parsed = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: \r\n\r\nHello world\r\nAnd good day", out HttpRequest _);
        Assert.That(parsed, Is.False);
    }
}
