namespace PinchHitter;

using System.Net;
using System.Text;

[TestFixture]
public class HttpResponseTests
{
    [Test]
    public void TestCanCreateResponse()
    {
        HttpResponse response = new("requestId");
        response.Headers["Custom-Header"] = new List<string>() { "Custom Header Value" };
        response.TextBodyContent = "hello world";
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.ReasonPhrase, Is.EqualTo("OK"));
            Assert.That(response.HttpVersion, Is.EqualTo("HTTP/1.1"));
            Assert.That(response.Headers, Has.Count.EqualTo(1));
            Assert.That(response.Headers, Contains.Key("Custom-Header"));
            Assert.That(response.BodyContent, Is.Not.Empty);
            Assert.That(response.ToByteArray(), Has.Length.EqualTo(66));
        });
    }

    [Test]
    public void TestResponseWithUnsupportedStatusCode()
    {
        HttpResponse response = new("requestId")
        {
            StatusCode = HttpStatusCode.Unused
        };
        string responseTextContent = Encoding.UTF8.GetString(response.ToByteArray());
        string[] responseLines = responseTextContent.Split("\r\n");
        Assert.Multiple(() =>
        {
            Assert.That(response.ReasonPhrase, Is.Null);
            Assert.That(responseLines, Has.Length.GreaterThan(0));
            Assert.That(responseLines[0], Is.EqualTo("HTTP/1.1 306"));
        });
    }
}