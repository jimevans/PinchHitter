namespace PinchHitter;

using System.Net;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class BadRequestHandlerTests
{
    [Test]
    public async Task TestHandlerReturnsBadRequestResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        BadRequestHandler handler = new("Bad Request");
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.TextBodyContent, Is.EqualTo("Bad Request"));
            Assert.That(response.BodyContentBytes.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("Bad Request")));});
    }

}
