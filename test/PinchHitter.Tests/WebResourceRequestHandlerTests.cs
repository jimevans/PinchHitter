namespace PinchHitter;

using System.Net;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class WebResourceRequestHandlerTests
{
    [Test]
    public async Task TestHandlerReturnsOKResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        WebResourceRequestHandler handler = new("content");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(handler.Data, Is.EqualTo(Encoding.UTF8.GetBytes("content")));
            Assert.That(handler.MimeType, Is.EqualTo("text/html;charset=utf-8"));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.BodyContent, Is.EqualTo("content"));
        });
    }
}
