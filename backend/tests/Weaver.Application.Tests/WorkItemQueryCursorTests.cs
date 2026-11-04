using System.Buffers.Text;
using System.Text;
using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemQueryCursorTests
{
    [Test]
    public void ACursor_ReadsBackAsTheNumberItWasMadeFrom()
    {
        var token = new WorkItemQueryCursor(4321).Encode();

        Assert.Multiple(() =>
        {
            Assert.That(WorkItemQueryCursor.Parse(token), Is.EqualTo(new WorkItemQueryCursor(4321)));
            Assert.That(token, Does.Not.Contain("4321"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void NoCursor_StartsAtTheBeginning(string? token)
    {
        Assert.That(WorkItemQueryCursor.Parse(token), Is.EqualTo(WorkItemQueryCursor.Start));
    }

    [Test]
    public void APositionNoNumberCouldHave_IsRejected()
    {
        var tooBig = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"q1:{(long)int.MaxValue + 1}"));

        Assert.That(() => WorkItemQueryCursor.Parse(tooBig), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void AChangesSinceCursor_IsRejected()
    {
        var changes = new WorkItemChangesCursor(3).Encode();

        Assert.That(() => WorkItemQueryCursor.Parse(changes), Throws.TypeOf<DomainValidationException>());
    }
}
