using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// One agent run: the account and key that made it, its run id, its first and last changes and
/// how many there were. The account, key and run id together name the run for its changes.
/// </summary>
public record RunReportDto(
    Guid AccountId,
    string AccountName,
    Guid? ApiKeyId,
    string? ApiKeyLabel,
    string RunId,
    DateTimeOffset FirstChangeAtUtc,
    DateTimeOffset LastChangeAtUtc,
    int ChangeCount)
{
    public static RunReportDto FromReport(RunReport run) => new(
        run.AccountId,
        run.AccountName,
        run.ApiKeyId,
        run.ApiKeyLabel,
        run.RunId,
        run.FirstChangeAtUtc,
        run.LastChangeAtUtc,
        run.ChangeCount);
}
