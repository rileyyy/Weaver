namespace Weaver.Application.Services;

/// <summary>One run in the run reports: who made it, when its first and last changes were, and how many there were.</summary>
public sealed record RunReport(
    Guid AccountId,
    string AccountName,
    Guid? ApiKeyId,
    string? ApiKeyLabel,
    string RunId,
    DateTimeOffset FirstChangeAtUtc,
    DateTimeOffset LastChangeAtUtc,
    int ChangeCount);
