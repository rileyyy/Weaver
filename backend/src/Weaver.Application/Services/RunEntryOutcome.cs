namespace Weaver.Application.Services;

/// <summary>What an undo did with one of the run's history entries.</summary>
internal sealed record RunEntryOutcome(long EntryId, Guid WorkItemId, string Field, RunUndoOutcome Result);
