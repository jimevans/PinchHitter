namespace PinchHitter;

using System.Text;

[TestFixture]
public class BasicWebAuthenticatorTests
{
    [Test]
    public void TestCanAuthenticate()
    {
        string userName = "user";
        string password = "P@ssw0rd!";
        string base64EncodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));

        BasicWebAuthenticator authenticator = new(userName, password);
        Assert.That(authenticator.IsAuthenticated($"Basic {base64EncodedString}"), Is.True);
    }

    [Test]
    public void TestInvalidValueDoesNotAuthenticate()
    {
        string userName = "user";
        string password = "P@ssw0rd!";
        string base64EncodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:incorrectPassword"));

        BasicWebAuthenticator authenticator = new(userName, password);
        Assert.That(authenticator.IsAuthenticated($"Basic {base64EncodedString}"), Is.False);
    }

    [Test]
    public void TestInvalidHeaderFormatDoesNotAuthenticate()
    {
        string userName = "user";
        string password = "P@ssw0rd!";
        string base64EncodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));

        BasicWebAuthenticator authenticator = new(userName, password);
        Assert.That(authenticator.IsAuthenticated($"{base64EncodedString}"), Is.False);
    }
}