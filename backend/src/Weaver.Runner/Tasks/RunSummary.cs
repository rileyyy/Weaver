namespace Weaver.Runner.Tasks;

/// <summary>
/// What one run did with the items it looked at. <see cref="Rejected"/> counts answers that
/// failed a check; <see cref="WriteFailed"/> counts checked answers Weaver turned down.
/// </summary>
public sealed record RunSummary(int Considered, int Written, int Skipped, int Rejected, int WriteFailed);
