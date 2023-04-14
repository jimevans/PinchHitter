namespace PinchHitter;

using System.Net;

[TestFixture]
public class MethodNotAllowedRequestHandlerTests
{
    [Test]
    public void TestHandlerReturnsMethodNotAllowedResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        MethodNotAllowedRequestHandler handler = new("Method Not Allowed");
        HttpResponse response = handler.HandleRequest("connectionId", request, new List<HttpMethod>() { HttpMethod.Post, HttpMethod.Delete });
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(response.BodyContent, Is.EqualTo("Method Not Allowed"));
            Assert.That(response.Headers, Contains.Key("Allow"));
            Assert.That(response.Headers["Allow"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Allow"][0], Is.EqualTo("DELETE, POST"));
        });
    }

    [Test]
    public void TestHandlerWithoutValidMethodListThrows()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        MethodNotAllowedRequestHandler handler = new("Method Not Allowed");
        Assert.That(() => handler.HandleRequest("connectionId", request), Throws.InstanceOf<ArgumentException>().With.Message.Contains("Request handler requires list of valid methods."));
    }

    [Test]
    public void TestHandlerWithInvalidParameterTypeThrows()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        MethodNotAllowedRequestHandler handler = new("Method Not Allowed");
        Assert.That(() => handler.HandleRequest("connectionId", request, "foo"), Throws.InstanceOf<ArgumentException>().With.Message.Contains("Additional data must be a list of HttpMethod values."));
    }

    [Test]
    public void TestHandlerWithEmptyMethodListThrows()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        MethodNotAllowedRequestHandler handler = new("Method Not Allowed");
        Assert.That(() => handler.HandleRequest("connectionId", request, new List<HttpMethod>()), Throws.InstanceOf<ArgumentException>().With.Message.Contains("List of HttpMethod values most contain at least one entry."));
    }
}