using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// The query string of <c>GET /api/work-items/search</c>, one field per <see cref="WorkItemQuery"/>
/// input. List inputs repeat their key (<c>?tags=a&amp;tags=b</c>).
/// </summary>
public sealed class WorkItemSearchParameters
{
    public IReadOnlyList<Guid>? SpaceIds { get; init; }

    /// <summary>Every word must start a word of the title, description or When; <c>#123</c> looks up that Number.</summary>
    public string? Text { get; init; }

    public int? Number { get; init; }

    public IReadOnlyList<StatusCategory>? StatusCategories { get; init; }

    public Guid? AssigneeId { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public TagMatch TagMatch { get; init; } = TagMatch.Any;

    public IReadOnlyList<Guid>? TypeIds { get; init; }

    /// <summary>Start of the date window, inclusive (yyyy-MM-dd).</summary>
    public DateOnly? From { get; init; }

    /// <summary>End of the date window, inclusive (yyyy-MM-dd).</summary>
    public DateOnly? To { get; init; }

    public bool BacklogOnly { get; init; }

    public DateTimeOffset? UpdatedSince { get; init; }

    /// <summary>The <c>nextCursor</c> from the previous page; omit for the first.</summary>
    public string? Cursor { get; init; }

    /// <summary>Items per page, 1 to 1000.</summary>
    public int Limit { get; init; } = WorkItemQuery.DefaultLimit;

    public WorkItemQuery ToQuery() => new(
        SpaceIds: SpaceIds,
        Text: Text,
        Number: Number,
        StatusCategories: StatusCategories,
        AssigneeId: AssigneeId,
        Tags: Tags,
        TagMatch: TagMatch,
        TypeIds: TypeIds,
        From: From,
        To: To,
        BacklogOnly: BacklogOnly,
        UpdatedSince: UpdatedSince,
        Cursor: Cursor,
        Limit: Limit);
}
