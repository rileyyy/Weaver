namespace Weaver.Runner.WeaverApi;

/// <summary>The parts of Weaver's <c>StatusDto</c> the runner reads; the category is ToDo, Doing or Done.</summary>
internal sealed record WeaverStatus(Guid Id, int Order, string Category);
