using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class BoardTests
{
    [TestCase("")]
    [TestCase("  ")]
    public void Create_WithABlankName_IsRejected(string name)
    {
        Assert.Throws<DomainValidationException>(() => Board.Create(name, null));
    }
}
