using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>What a <see cref="WorkItemHistoryField.Link"/> entry holds on one of the two items.</summary>
internal sealed record LinkHistoryValue(Guid WorkItemId, WorkItemLinkRelation Type);
