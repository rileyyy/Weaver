namespace Weaver.Application.Services;

/// <summary>Every change a run made, in the order it made them, each with what the undo did.</summary>
public sealed record RunUndoResult(IReadOnlyList<RunUndoChange> Changes);
