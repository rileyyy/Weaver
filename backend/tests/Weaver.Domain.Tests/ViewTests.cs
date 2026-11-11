using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class ViewTests
{
    [TestCase("")]
    [TestCase("  ")]
    public void Create_WithABlankName_IsRejected(string name)
    {
        Assert.Throws<DomainValidationException>(() => View.Create(Definition() with { Name = name }));
    }

    [Test]
    public void Create_WithATooLongName_IsRejected()
    {
        Assert.Throws<DomainValidationException>(
            () => View.Create(Definition() with { Name = new string('x', View.NameMaxLength + 1) }));
    }

    [Test]
    public void Create_TrimsTheNameAndText_AndDropsBlankText()
    {
        var named = View.Create(Definition() with { Name = "  Not software ", Text = " paint " });
        var blank = View.Create(Definition() with { Text = "   " });

        Assert.Multiple(() =>
        {
            Assert.That(named.Name, Is.EqualTo("Not software"));
            Assert.That(named.Text, Is.EqualTo("paint"));
            Assert.That(blank.Text, Is.Null);
        });
    }

    [Test]
    public void Create_TrimsTagsAndDropsDuplicatesIgnoringCase_KeepingTheFirstSpelling()
    {
        var view = View.Create(Definition() with { Tags = [" Outside", "outside", "Urgent "] });

        Assert.That(view.Tags, Is.EqualTo(new[] { "Outside", "Urgent" }));
    }

    [Test]
    public void Create_WithABlankTag_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => View.Create(Definition() with { Tags = ["ok", " "] }));
    }

    [Test]
    public void Create_DropsRepeatedSpaces()
    {
        var space = Guid.NewGuid();

        var view = View.Create(Definition() with { SpaceIds = [space, space] });

        Assert.That(view.SpaceIds, Is.EqualTo(new[] { space }));
    }

    [Test]
    public void Create_KeepsEveryQueryInputAndTheLayout()
    {
        var space = Guid.NewGuid();

        var view = View.Create(new ViewDefinition(
            "Outside work",
            [space],
            "fence",
            ["outside"],
            TagMatch.All,
            ApplyDoneHiding: false,
            ScheduledOrInProgressOnly: true,
            ViewLayout.Roadmap));

        Assert.Multiple(() =>
        {
            Assert.That(view.SpaceIds, Is.EqualTo(new[] { space }));
            Assert.That(view.TagMatch, Is.EqualTo(TagMatch.All));
            Assert.That(view.ApplyDoneHiding, Is.False);
            Assert.That(view.ScheduledOrInProgressOnly, Is.True);
            Assert.That(view.Layout, Is.EqualTo(ViewLayout.Roadmap));
            Assert.That(view.IsBuiltIn, Is.False);
        });
    }

    [Test]
    public void Redefine_ReplacesTheWholeDefinition()
    {
        var view = View.Create(Definition() with { Text = "paint", Tags = ["outside"] });

        view.Redefine(Definition() with { Name = "Renamed" });

        Assert.Multiple(() =>
        {
            Assert.That(view.Name, Is.EqualTo("Renamed"));
            Assert.That(view.Text, Is.Null);
            Assert.That(view.Tags, Is.Empty);
        });
    }

    [Test]
    public void Overview_IsBuiltIn_AndListsScheduledOrInProgressWorkInEverySpace()
    {
        var overview = View.Overview();

        Assert.Multiple(() =>
        {
            Assert.That(overview.Id, Is.EqualTo(View.OverviewId));
            Assert.That(overview.IsBuiltIn, Is.True);
            Assert.That(overview.Name, Is.EqualTo("Overview"));
            Assert.That(overview.SpaceIds, Is.Empty);
            Assert.That(overview.ScheduledOrInProgressOnly, Is.True);
            Assert.That(overview.ApplyDoneHiding, Is.True);
        });
    }

    [Test]
    public void Overview_CannotBeRedefined()
    {
        Assert.Throws<BuiltInViewException>(() => View.Overview().Redefine(Definition()));
    }

    private static ViewDefinition Definition() => new(
        "Home and village",
        [],
        null,
        [],
        TagMatch.Any,
        ApplyDoneHiding: true,
        ScheduledOrInProgressOnly: false,
        ViewLayout.Board);
}
