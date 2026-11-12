using System.Globalization;

namespace Weaver.Runner.Model;

/// <summary>
/// The yyyy-MM-dd dates the model is shown and answers in. Always in the invariant culture: in a
/// culture with another calendar (e.g. Thai Buddhist) "yyyy" would give a different year.
/// </summary>
public static class IsoDate
{
    public const string Pattern = "yyyy-MM-dd";

    public static string Format(DateOnly date) => date.ToString(Pattern, CultureInfo.InvariantCulture);

    /// <summary>A date, or "?" for the open end of a range that has only the other one.</summary>
    public static string FormatOrOpen(DateOnly? date) => date is { } d ? Format(d) : "?";

    public static bool TryParse(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, Pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
