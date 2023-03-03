namespace PinchHitter;

[TestFixture]
public class HttpRequestTests
{
    [Test]
    public void TestCanParseHttpRequest()
    {
        HttpRequest request = HttpRequest.Parse("GET / HTTP/1.1\r\nUser-Agent:Test User Agent\r\n\r\nHello world");
        Assert.Multiple(() =>
        {
            Assert.That(request.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(request.Verb, Is.EqualTo("GET"));
            Assert.That(request.Url, Is.EqualTo("/"));
            Assert.That(request.Headers, Has.Count.EqualTo(1));
            Assert.That(request.Headers, Contains.Key("User-Agent"));
            Assert.That(request.Headers["User-Agent"], Has.Count.EqualTo(1));
            Assert.That(request.Headers["User-Agent"][0], Is.EqualTo("Test User Agent"));
            Assert.That(request.Body, Is.EqualTo("Hello world"));
        });
    }

    [Test]
    public void TestCanParseHttpRequestWithRepeatedHeaders()
    {
        HttpRequest request = HttpRequest.Parse("GET / HTTP/1.1\r\nCookie:name1;value1\r\nCookie:name2;value2\r\n\r\nHello world");
        Assert.Multiple(() =>
        {
            Assert.That(request.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(request.Verb, Is.EqualTo("GET"));
            Assert.That(request.Url, Is.EqualTo("/"));
            Assert.That(request.Headers, Has.Count.EqualTo(1));
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
        HttpRequest request = HttpRequest.Parse("GET / HTTP/1.1\r\n\r\nHello world\r\nAnd good day");
        Assert.Multiple(() =>
        {
            Assert.That(request.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(request.Verb, Is.EqualTo("GET"));
            Assert.That(request.Url, Is.EqualTo("/"));
            Assert.That(request.Body, Is.EqualTo("Hello world\nAnd good day"));
        });
    }
}
