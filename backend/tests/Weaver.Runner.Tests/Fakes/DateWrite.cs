namespace Weaver.Runner.Tests.Fakes;

internal sealed record DateWrite(Guid WorkItemId, DateOnly? Start, DateOnly? End, uint ExpectedVersion, string ActorLabel, bool Replaces = false);
