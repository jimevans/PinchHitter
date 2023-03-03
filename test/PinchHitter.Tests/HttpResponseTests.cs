namespace PinchHitter;

using System.Net;
using System.Text;

[TestFixture]
public class HttpResponseTests
{
    [Test]
    public void TestCanCreateResponse()
    {
        HttpResponse response = new();
        response.Headers["Custom-Header"] = new List<string>() { "Custom Header Value" };
        response.BodyContent = Encoding.UTF8.GetBytes("hello world");
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
        HttpResponse response = new();
        response.StatusCode = HttpStatusCode.Unused;
        Assert.Multiple(() =>
        {
            Assert.That(response.ReasonPhrase, Is.Null);
        });
    }
}