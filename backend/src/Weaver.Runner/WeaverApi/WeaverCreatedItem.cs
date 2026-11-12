namespace Weaver.Runner.WeaverApi;

/// <summary>The part of a newly created item the runner reads: the Number its reply names it by.</summary>
internal sealed record WeaverCreatedItem(Guid Id, int Number);
