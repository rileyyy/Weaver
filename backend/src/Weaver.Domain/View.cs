using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A saved, shared work-item query (ADR-0006): which Spaces, which filters and which layout.
/// Every user sees the same Views, and anyone may save, rename or delete one. Overview is built
/// in rather than stored (<see cref="Overview"/>).
/// </summary>
public class View : IHasUpdatedAt
{
    public const int NameMaxLength = 100;
    public const int TextMaxLength = 500;
    public const int MaxTags = WorkItem.MaxTags;
    public const string OverviewName = "Overview";

    /// <summary>Overview's id. It's never stored, so it can't collide with a saved View's.</summary>
    public static readonly Guid OverviewId = new("00000000-0000-0000-0000-000000000001");

    private List<Guid> _spaceIds = [];
    private List<string> _tags = [];

    /// <summary>For EF Core.</summary>
    private View()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Trimmed, and unique across Views ignoring case.</summary>
    public string Name { get; private set; }

    /// <summary>Empty for every Space, including ones created later.</summary>
    public IReadOnlyList<Guid> SpaceIds
    {
        get => _spaceIds;
        private set => _spaceIds = [.. value];
    }

    /// <summary>Trimmed; null for no text filter.</summary>
    public string? Text { get; private set; }

    public IReadOnlyList<string> Tags
    {
        get => _tags;
        private set => _tags = [.. value];
    }

    public TagMatch TagMatch { get; private set; }

    public bool ApplyDoneHiding { get; private set; }

    public bool ScheduledOrInProgressOnly { get; private set; }

    public ViewLayout Layout { get; private set; }

    public bool IsBuiltIn => Id == OverviewId;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static View Create(ViewDefinition definition)
    {
        var view = new View { Id = Guid.NewGuid() };
        view.Apply(definition);
        return view;
    }

    /// <summary>
    /// Overview: everything in a Doing status or dated in the window, across every Space, with
    /// old Done items hidden. A new instance each time, never stored.
    /// </summary>
    public static View Overview() => new()
    {
        Id = OverviewId,
        Name = OverviewName,
        TagMatch = TagMatch.Any,
        ApplyDoneHiding = true,
        ScheduledOrInProgressOnly = true,
        Layout = ViewLayout.Board,
    };

    /// <summary>Replaces the whole definition, the way a <c>PUT</c> does.</summary>
    public void Redefine(ViewDefinition definition)
    {
        if (IsBuiltIn)
        {
            throw new BuiltInViewException(Name);
        }

        Apply(definition);
    }

    private void Apply(ViewDefinition definition)
    {
        TextValidation.RequireText(definition.Name, "View name", NameMaxLength);
        var text = string.IsNullOrWhiteSpace(definition.Text) ? null : definition.Text.Trim();
        if (text is not null)
        {
            TextValidation.RequireMaxLength(text, "Search text", TextMaxLength);
        }

        var tags = TagList.Normalize(definition.Tags, () => new DomainValidationException("A View's tags must not be blank."));
        if (tags.Count > MaxTags)
        {
            throw new DomainValidationException($"A View can filter on at most {MaxTags} tags.");
        }

        Name = definition.Name.Trim();
        _spaceIds = definition.SpaceIds.Distinct().ToList();
        Text = text;
        _tags = tags;
        TagMatch = definition.TagMatch;
        ApplyDoneHiding = definition.ApplyDoneHiding;
        ScheduledOrInProgressOnly = definition.ScheduledOrInProgressOnly;
        Layout = definition.Layout;
    }
}
