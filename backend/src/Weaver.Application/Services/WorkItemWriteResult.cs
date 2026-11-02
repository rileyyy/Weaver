using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// A work item after a write, and which of its fields the write changed (named as in
/// <see cref="WorkItemField"/>). The item carries its new version.
/// </summary>
public record WorkItemWriteResult(WorkItem Item, IReadOnlyList<string> ChangedFields);
