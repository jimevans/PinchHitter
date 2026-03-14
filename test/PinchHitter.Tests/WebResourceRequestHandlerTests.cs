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
            Assert.That(handler.Data.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("content")));
            Assert.That(handler.MimeType, Is.EqualTo("text/html;charset=utf-8"));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.TextBodyContent, Is.EqualTo("content"));
            Assert.That(response.BodyContentBytes.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("content")));
        });
    }

    [Test]
    public async Task TestHandlerResponseContainsCorrectlyFormattedDateHeader()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", out HttpRequest request);
        WebResourceRequestHandler handler = new("content");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers, Contains.Key("Date"));
            Assert.That(response.Headers["Date"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Date"][0], Does.Match(@"^\w{3}, \d{2} \w{3} \d{4} \d{2}:\d{2}:\d{2} GMT$"));
        });
    }
}
