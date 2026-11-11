using System.Text.Json;
using Weaver.Runner.Model;

namespace Weaver.Runner.WhenResolution;

/// <summary>
/// The optional <c>start</c> and <c>end</c> of a model's answer, checked the same way for every
/// task kind: each is a <see cref="IsoDate"/> on or after today, and end isn't before start.
/// Throws <see cref="InvalidAnswerException"/> otherwise.
/// </summary>
internal static class AnswerDates
{
    public static (DateOnly? Start, DateOnly? End) Read(JsonElement root, DateOnly today)
    {
        var start = ReadDate(root, "start", today);
        var end = ReadDate(root, "end", today);
        if (start is not null && end is not null && end < start)
        {
            throw new InvalidAnswerException($"end {IsoDate.Format(end.Value)} is before start {IsoDate.Format(start.Value)}");
        }

        return (start, end);
    }

    private static DateOnly? ReadDate(JsonElement root, string name, DateOnly today)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String || !IsoDate.TryParse(value.GetString(), out var date))
        {
            throw new InvalidAnswerException($"{name} isn't a {IsoDate.Pattern} date: {value.GetRawText()}");
        }

        if (date < today)
        {
            throw new InvalidAnswerException($"{name} {IsoDate.Format(date)} is before today {IsoDate.Format(today)}");
        }

        return date;
    }
}
