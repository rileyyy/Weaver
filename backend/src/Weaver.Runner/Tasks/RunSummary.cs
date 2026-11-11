namespace Weaver.Runner.Tasks;

/// <summary>
/// What one run did with the items it looked at. <see cref="Rejected"/> counts answers that
/// failed a check; <see cref="WriteFailed"/> counts checked answers Weaver turned down;
/// <see cref="Failed"/> counts items no answer was had for, because Weaver turned down a read
/// for the item or the model turned down its prompt.
/// </summary>
public sealed record RunSummary(int Considered, int Written, int Skipped, int Rejected, int WriteFailed, int Failed);
