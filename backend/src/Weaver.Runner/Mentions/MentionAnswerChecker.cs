using System.Text.Json;
using Weaver.Runner.Model;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Mentions;

/// <summary>
/// Checks a model's answer to a mention before anything is written (ADR-0010): there is a reply,
/// every sub-item has a title, every link is an http or https URL, dates are as for When
/// resolution, and there aren't more additions than a person would want to review. An answer that
/// fails any check is rejected whole, so nothing of it is written.
/// </summary>
public static class MentionAnswerChecker
{
    public const int MaxSubItems = 10;
    public const int MaxLinks = 10;

    /// <summary>Weaver's own limits, checked here so one bad addition can't fail half an answer.</summary>
    private const int TitleMaxLength = 500;
    private const int UrlMaxLength = 2048;
    private const int LabelMaxLength = 200;

    public static MentionAnswerCheck Check(string rawAnswer, DateOnly today)
    {
        if (!ModelJson.TryReadObject(rawAnswer, out var root))
        {
            return MentionAnswerCheck.Reject("the answer isn't a JSON object");
        }

        try
        {
            var reply = ReadReply(root);
            var subItems = ReadSubItems(root);
            var links = ReadLinks(root);
            var (start, end) = AnswerDates.Read(root, today);
            return MentionAnswerCheck.Accept(new MentionAnswer(reply, subItems, links, start, end));
        }
        catch (InvalidAnswerException e)
        {
            return MentionAnswerCheck.Reject(e.Message);
        }
    }

    private static string ReadReply(JsonElement root) =>
        root.TryGetProperty("reply", out var reply)
        && reply.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(reply.GetString())
            ? reply.GetString()!.Trim()
            : throw new InvalidAnswerException("the answer has no reply");

    private static IReadOnlyList<string> ReadSubItems(JsonElement root)
    {
        var titles = ReadArray(root, "subItems", MaxSubItems)
            .Select(title => title.ValueKind == JsonValueKind.String ? title.GetString()!.Trim() : string.Empty)
            .ToList();
        if (titles.Any(string.IsNullOrEmpty))
        {
            throw new InvalidAnswerException("every sub-item must be a title");
        }

        return titles.Any(t => t.Length > TitleMaxLength)
            ? throw new InvalidAnswerException($"a sub-item's title is longer than {TitleMaxLength} characters")
            : titles;
    }

    private static IReadOnlyList<MentionLink> ReadLinks(JsonElement root) =>
        ReadArray(root, "links", MaxLinks).Select(ReadLink).ToList();

    private static MentionLink ReadLink(JsonElement link)
    {
        var url = link.ValueKind == JsonValueKind.Object && link.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
            ? u.GetString()!.Trim()
            : throw new InvalidAnswerException($"a link has no url: {link.GetRawText()}");
        if (url.Length > UrlMaxLength || !IsHttpOrHttps(url))
        {
            throw new InvalidAnswerException($"a link isn't an http or https URL: {url}");
        }

        var label = link.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(l.GetString())
            ? l.GetString()!.Trim()
            : null;
        return label is { Length: > LabelMaxLength }
            ? throw new InvalidAnswerException($"a link's label is longer than {LabelMaxLength} characters")
            : new MentionLink(url, label);
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement root, string name, int max)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidAnswerException($"{name} isn't a list");
        }

        return value.GetArrayLength() > max
            ? throw new InvalidAnswerException($"{name} has more than {max} entries")
            : value.EnumerateArray().ToList();
    }

    /// <summary>The same rule Weaver applies to URL links, so the write isn't refused afterwards.</summary>
    private static bool IsHttpOrHttps(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.Host.Length > 0;
}
