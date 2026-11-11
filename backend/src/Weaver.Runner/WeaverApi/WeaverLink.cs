namespace Weaver.Runner.WeaverApi;

/// <summary>The part of a link the runner reads: the item on its other side.</summary>
internal sealed record WeaverLink(Guid LinkedWorkItemId);
