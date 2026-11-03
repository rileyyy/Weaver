using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemChangesCursorTests
{
    [Test]
    public void ACursor_ReadsBackAsThePositionItWasMadeFrom()
    {
        var token = new WorkItemChangesCursor(12345).Encode();

        Assert.That(WorkItemChangesCursor.Parse(token), Is.EqualTo(new WorkItemChangesCursor(12345)));
    }

    [Test]
    public void ACursor_IsOpaque_NotTheBareEntryId()
    {
        Assert.That(new WorkItemChangesCursor(12345).Encode(), Does.Not.Contain("12345"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void NoCursor_StartsAtTheBeginning(string? token)
    {
        Assert.That(WorkItemChangesCursor.Parse(token), Is.EqualTo(WorkItemChangesCursor.Start));
    }

    [TestCase("not a cursor")]
    [TestCase("bm9wZQ")]
    [TestCase("djE6LTE")]
    public void AMalformedCursor_IsRejected(string token)
    {
        Assert.That(() => WorkItemChangesCursor.Parse(token), Throws.TypeOf<DomainValidationException>());
    }
}
