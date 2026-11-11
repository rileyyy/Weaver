using System.Globalization;

namespace Weaver.Runner.WhenResolution;

/// <summary>
/// The yyyy-MM-dd dates the model is shown and answers in. Always in the invariant culture: in a
/// culture with another calendar (e.g. Thai Buddhist) "yyyy" would give a different year.
/// </summary>
public static class IsoDate
{
    public const string Pattern = "yyyy-MM-dd";

    public static string Format(DateOnly date) => date.ToString(Pattern, CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, Pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
