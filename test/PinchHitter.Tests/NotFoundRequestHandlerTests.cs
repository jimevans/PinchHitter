namespace PinchHitter;

using System.Net;

[TestFixture]
public class NotFoundRequestHandlerTests
{
    [Test]
    public void TestHandlerReturnsNotFoundResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        NotFoundRequestHandler handler = new("Not Found");
        HttpResponse response = handler.HandleRequest("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.BodyContent, Is.EqualTo("Not Found"));
        });
    }
}
