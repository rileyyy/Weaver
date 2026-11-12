using System.Globalization;
using System.Text.Json;
using Weaver.Runner.Model;

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
        if (!ModelJson.TryReadObject(rawAnswer, out var root))
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
        var (start, end) = AnswerDates.Read(root, today);
        var blocker = ReadBlocker(root, offered);
        var confidence = ReadConfidence(root);
        if (start is null && end is null && blocker is null)
        {
            throw new InvalidAnswerException("the answer has no dates and no blocker");
        }

        return new WhenAnswer(start, end, blocker, confidence);
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
