namespace PinchHitter;

using System.Net;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class NotFoundRequestHandlerTests
{
    [Test]
    public async Task TestHandlerReturnsNotFoundResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        NotFoundRequestHandler handler = new("Not Found");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.TextBodyContent, Is.EqualTo("Not Found"));
            Assert.That(response.BodyContentBytes.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("Not Found")));
        });
    }
}
