namespace Weaver.Application.Services;

/// <summary>
/// Run reports (ADR-0010): the writes agents made with a run id, grouped into runs so an admin
/// can review what each run did. Built on history alone, so a run that changed nothing never
/// appears. Every operation is refused unless the caller is an active admin.
/// </summary>
public interface IRunReportService
{
    /// <summary>
    /// Runs newest first, by their last change. <paramref name="limit"/> is 1 to
    /// <see cref="RunReportService.MaxPageSize"/>.
    /// </summary>
    Task<RunReportPage> ListAsync(int offset, int limit, CancellationToken ct = default);

    /// <summary>
    /// The items one run changed, in the order it first changed each, with only that run's
    /// entries. Throws <see cref="Weaver.Domain.Exceptions.RunNotFoundException"/> when no entry
    /// belongs to the run.
    /// </summary>
    Task<IReadOnlyList<ChangedWorkItem>> ListChangesAsync(RunKey run, CancellationToken ct = default);
}
