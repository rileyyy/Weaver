using System.Globalization;
using System.Text.Json;

namespace Weaver.Runner.WhenResolution;

/// <summary>
/// Checks a model's answer before anything is written (ADR-0010): dates parse and aren't in the
/// past, the end isn't before the start, a blocker is one the runner offered, and the confidence
/// is stated. An answer that fails any check is rejected whole, so nothing of it is written.
/// </summary>
public static class WhenAnswerChecker
{
    public static WhenAnswerCheck Check(string rawAnswer, DateOnly today, IReadOnlyList<BlockerCandidate> offered)
    {
        if (!TryReadObject(rawAnswer, out var root))
        {
            return WhenAnswerCheck.Reject("the answer isn't a JSON object");
        }

        if (SkipReason(root) is { } skip)
        {
            return WhenAnswerCheck.Skip(skip);
        }

        try
        {
            return WhenAnswerCheck.Resolve(ReadResolution(root, today, offered));
        }
        catch (InvalidAnswerException e)
        {
            return WhenAnswerCheck.Reject(e.Message);
        }
    }

    private static WhenAnswer ReadResolution(JsonElement root, DateOnly today, IReadOnlyList<BlockerCandidate> offered)
    {
        var start = ReadDate(root, "start", today);
        var end = ReadDate(root, "end", today);
        if (start is not null && end is not null && end < start)
        {
            throw new InvalidAnswerException($"end {IsoDate.Format(end.Value)} is before start {IsoDate.Format(start.Value)}");
        }

        var blocker = ReadBlocker(root, offered);
        var confidence = ReadConfidence(root);
        if (start is null && end is null && blocker is null)
        {
            throw new InvalidAnswerException("the answer has no dates and no blocker");
        }

        return new WhenAnswer(start, end, blocker, confidence);
    }

    /// <summary>
    /// Small models often wrap JSON in a code fence or a sentence despite being told not to, so the
    /// outermost braces are read rather than the whole text.
    /// </summary>
    private static bool TryReadObject(string raw, out JsonElement root)
    {
        root = default;
        var open = raw.IndexOf('{');
        var close = raw.LastIndexOf('}');
        if (open < 0 || close < open)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw[open..(close + 1)]);
            root = document.RootElement.Clone();
            return root.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? SkipReason(JsonElement root)
    {
        if (!root.TryGetProperty("skip", out var skip))
        {
            return null;
        }

        return skip.ValueKind switch
        {
            JsonValueKind.String when !string.IsNullOrWhiteSpace(skip.GetString()) => skip.GetString()!.Trim(),
            JsonValueKind.True => "no reason given",
            _ => null,
        };
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

    private static BlockerCandidate? ReadBlocker(JsonElement root, IReadOnlyList<BlockerCandidate> offered)
    {
        if (!root.TryGetProperty("blockedBy", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var number = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(value.GetString()!.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out var n) => n,
            _ => (int?)null,
        };

        return offered.FirstOrDefault(c => c.Number == number)
            ?? throw new InvalidAnswerException($"blockedBy {value.GetRawText()} isn't one of the offered items");
    }

    private static WhenConfidence ReadConfidence(JsonElement root)
    {
        var stated = root.TryGetProperty("confidence", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim().ToLowerInvariant()
            : null;

        return stated switch
        {
            "low" => WhenConfidence.Low,
            "medium" => WhenConfidence.Medium,
            "high" => WhenConfidence.High,
            _ => throw new InvalidAnswerException("confidence must be low, medium or high"),
        };
    }
}
