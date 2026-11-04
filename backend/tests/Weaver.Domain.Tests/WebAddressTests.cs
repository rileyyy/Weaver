namespace Weaver.Domain.Tests;

[TestFixture]
public class WebAddressTests
{
    [TestCase("https://example.com/pr/7")]
    [TestCase("http://nas.local/plans")]
    [TestCase("http://192.168.1.10:8080/")]
    public void IsHttpOrHttps_WithAHost_IsTrue(string text)
    {
        Assert.That(WebAddress.IsHttpOrHttps(text), Is.True);
    }

    [TestCase("http:///x")]
    [TestCase("https:///")]
    [TestCase("example.com")]
    [TestCase("ftp://example.com/file")]
    [TestCase("javascript:alert(1)")]
    [TestCase("file:///etc/passwd")]
    public void IsHttpOrHttps_WithoutAnHttpSchemeAndHost_IsFalse(string text)
    {
        Assert.That(WebAddress.IsHttpOrHttps(text), Is.False);
    }
}
