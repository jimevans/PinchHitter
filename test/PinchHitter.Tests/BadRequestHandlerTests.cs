namespace PinchHitter;

using System.Net;
using System.Text;

[TestFixture]
public class BadRequestHandlerTests
{
    [Test]
    public void TestHandlerReturnsBadRequestResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        BadRequestHandler handler = new("Bad Request");
        HttpResponse response = handler.HandleRequest(request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(Encoding.UTF8.GetString(response.BodyContent), Is.EqualTo("Bad Request"));
        });
    }

}
