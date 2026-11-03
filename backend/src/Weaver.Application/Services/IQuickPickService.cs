namespace Weaver.Application.Services;

/// <summary>
/// The When quick picks resolved to dates for today in the server's time zone. The app can't
/// work out the server's today itself, so it asks here and then sets the dates through the
/// ordinary schedule write.
/// </summary>
public interface IQuickPickService
{
    Task<IReadOnlyList<ResolvedQuickPick>> ListAsync(CancellationToken ct = default);
}
