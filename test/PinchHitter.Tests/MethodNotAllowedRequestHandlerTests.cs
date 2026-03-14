namespace PinchHitter;

using System.Net;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class MethodNotAllowedRequestHandlerTests
{
    [Test]
    public async Task TestHandlerReturnsMethodNotAllowedResponse()
    {
        _ = HttpRequest.TryParse("GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent:Test User Agent\r\n\r\n", out HttpRequest request);
        MethodNotAllowedRequestHandler handler = new("Method Not Allowed", [HttpRequestMethod.Post, HttpRequestMethod.Delete]);
        HttpResponse response = await handler.HandleRequestAsync("connectionId", request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(response.TextBodyContent, Is.EqualTo("Method Not Allowed"));
            Assert.That(response.BodyContentBytes.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("Method Not Allowed")));
            Assert.That(response.Headers, Contains.Key("Allow"));
            Assert.That(response.Headers["Allow"], Has.Count.EqualTo(1));
            Assert.That(response.Headers["Allow"][0], Is.EqualTo("DELETE, POST"));
        });
    }

    [Test]
    public void TestHandlerWithoutValidMethodListThrows()
    {
        Assert.That(() => { MethodNotAllowedRequestHandler handler = new("Method Not Allowed", null!); }, Throws.InstanceOf<ArgumentNullException>().With.Message.Contains("Request handler requires list of valid methods."));
    }

    [Test]
    public void TestHandlerWithEmptyMethodListThrows()
    {
        Assert.That(() => { MethodNotAllowedRequestHandler handler = new("Method Not Allowed", new List<HttpRequestMethod>()); }, Throws.InstanceOf<ArgumentException>().With.Message.Contains("List of HttpMethod values must contain at least one entry."));
    }
}
