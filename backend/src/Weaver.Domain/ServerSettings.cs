using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// Settings that apply to the whole server, set by an admin. There is exactly one row, so
/// each setting is a typed, validated column rather than a key/value pair.
/// </summary>
public class ServerSettings
{
    public const int SingletonId = 1;
    public const string DefaultTimeZoneId = "UTC";
    public const int TimeZoneIdMaxLength = 64;
    public const int DefaultTrashRetentionDays = 30;
    public const int MinTrashRetentionDays = 1;
    public const int MaxTrashRetentionDays = 3650;

    /// <summary>For EF Core.</summary>
    private ServerSettings()
    {
    }

    public int Id { get; private set; }

    /// <summary>
    /// The IANA time zone that defines "today" wherever the server reasons about calendar
    /// dates, such as when a repeating item's next occurrence is due.
    /// </summary>
    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    /// <summary>
    /// How long a delete stays in the Trash before it's purged (ADR-0007), counted as elapsed
    /// time since the delete rather than calendar days in <see cref="TimeZoneId"/>.
    /// </summary>
    public int TrashRetentionDays { get; private set; } = DefaultTrashRetentionDays;

    public static ServerSettings CreateDefault() => new() { Id = SingletonId };

    /// <summary>The calendar date in the server's time zone at <paramref name="instant"/>.</summary>
    public DateOnly DateAt(DateTimeOffset instant) =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);

    /// <summary>
    /// Accepts only IANA ids (e.g. "Europe/Berlin"). Windows ids such as "W. Europe Standard
    /// Time" resolve on a Windows host but not in the Linux container, so they're refused
    /// everywhere. Stores the id as the system spells it. The lookup is case-insensitive
    /// only on Windows, so "europe/berlin" is refused in the Linux container.
    /// </summary>
    public void SetTimeZone(string timeZoneId)
    {
        var trimmed = timeZoneId?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > TimeZoneIdMaxLength ||
            !TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out var zone) ||
            !zone.HasIanaId)
        {
            throw new DomainValidationException(
                $"'{timeZoneId}' is not a known IANA time zone, such as 'Europe/Berlin' or 'UTC'.");
        }

        TimeZoneId = zone.Id;
    }

    /// <summary>A delete made at or before this instant has been in the Trash past its retention.</summary>
    public DateTimeOffset TrashExpiryCutoff(DateTimeOffset now) => now - TimeSpan.FromDays(TrashRetentionDays);

    public void SetTrashRetention(int days)
    {
        if (days is < MinTrashRetentionDays or > MaxTrashRetentionDays)
        {
            throw new DomainValidationException(
                $"Trash retention must be between {MinTrashRetentionDays} and {MaxTrashRetentionDays} days.");
        }

        TrashRetentionDays = days;
    }
}
