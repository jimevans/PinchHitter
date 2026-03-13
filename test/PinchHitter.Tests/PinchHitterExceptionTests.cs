namespace PinchHitter;

[TestFixture]
public class PinchHitterExceptionTests
{
    [Test]
    public void TestCanCreateException()
    {
        PinchHitterException exception = new("Test message");
        Assert.That(exception.Message, Is.EqualTo("Test message"));
    }

    [Test]
    public void TestCanCreateExceptionWithInnerException()
    {
        InvalidOperationException innerException = new("Inner exception message");
        PinchHitterException  exception = new("Test message", innerException);
        Assert.That(exception.Message, Is.EqualTo("Test message"));
        Assert.That(exception.InnerException, Is.EqualTo(innerException));
    }
}
