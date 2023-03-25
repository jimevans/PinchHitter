namespace PinchHitter;

[TestFixture]
public class WebContentTests
{
    [Test]
    public void TestWebContentCreatesHtmlDocument()
    {
        string content = WebContent.AsHtmlDocument("hello world", "header value");
        Assert.That(content, Is.EqualTo("<!DOCTYPE html><html><head>header value</head><body>hello world</body></html>"));
    }

    [Test]
    public void TestWebContentCreatesHtmlDocumentWhenHeadContentOmitted()
    {
        string content = WebContent.AsHtmlDocument("hello world");
        Assert.That(content, Is.EqualTo("<!DOCTYPE html><html><head></head><body>hello world</body></html>"));
    }
}
