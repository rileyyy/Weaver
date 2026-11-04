using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// Where a page of the work-item query ends: the <see cref="WorkItem.Number"/> of its last item.
/// The next page starts at the next higher Number still matching, however items move meanwhile.
/// </summary>
/// <remarks>Sent to clients as an <see cref="OpaqueCursor"/>.</remarks>
public readonly record struct WorkItemQueryCursor(int AfterNumber)
{
    private const string Prefix = "q1:";

    public static WorkItemQueryCursor Start { get; } = new(0);

    public string Encode() => OpaqueCursor.Encode(Prefix, AfterNumber);

    /// <summary>No token is the first page.</summary>
    public static WorkItemQueryCursor Parse(string? token)
    {
        var position = OpaqueCursor.Parse(Prefix, token, Start.AfterNumber);
        return position <= int.MaxValue
            ? new WorkItemQueryCursor((int)position)
            : throw new DomainValidationException("The cursor isn't one this server issued.");
    }
}
