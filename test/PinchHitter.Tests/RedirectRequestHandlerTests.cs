namespace PinchHitter;

using System.Net;
using System.Threading.Tasks;

[TestFixture]
public class RedirectRequestHandlerTests
{
    [Test]
    public async Task TestHandlerReturnsNotFoundResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        RedirectRequestHandler handler = new("/redirected");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(response.Headers, Contains.Key("Location"));
            Assert.That(response.Headers["Location"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Location"][0], Is.EqualTo("/redirected"));
            Assert.That(response.BodyContent, Is.Empty);
        });
    }

}
