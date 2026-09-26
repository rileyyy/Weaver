namespace Weaver.Domain;

/// <summary>
/// The weekdays a weekly or bi-weekly repetition falls on, stored as one bitmask column.
/// Deliberately not <see cref="DayOfWeek"/>'s ordinals: those start at Sunday = 0, which
/// can't be combined as flags.
/// </summary>
[Flags]
public enum RecurrenceDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
}
