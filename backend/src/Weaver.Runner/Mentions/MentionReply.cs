using System.Text;

namespace Weaver.Runner.Mentions;

/// <summary>
/// The comment that answers a mention: the model's reply, then what the runner added and what it
/// didn't, so the person who asked can see exactly what changed without opening history.
/// </summary>
public sealed class MentionReply
{
    /// <summary>Weaver's comment limit; the model's reply is shortened to make the notes fit.</summary>
    public const int MaxLength = 4000;

    private const int MaxReasonLength = 300;
    private const int MinReplyLength = 200;

    private readonly List<string> _added = [];
    private readonly List<string> _notDone = [];

    /// <summary>The reply when there's no answer to give, which still clears the mention.</summary>
    public static string CouldNotAnswer(string reason) =>
        Shorten($"I couldn't answer this request: {reason}", MaxLength);

    public void Added(string what) => _added.Add(what);

    public void NotDone(string what, string why) => _notDone.Add($"{what}: {Shorten(why, MaxReasonLength)}");

    public string Render(string modelReply)
    {
        var notes = new StringBuilder();
        AppendSection(notes, "Added:", _added);
        AppendSection(notes, "Not done:", _notDone);
        // The whole comment is shortened too: a reply Weaver refuses as too long would leave the
        // mention pending, and the next run would add everything again.
        return Shorten(Shorten(modelReply, Math.Max(MinReplyLength, MaxLength - notes.Length)) + notes, MaxLength);
    }

    private static void AppendSection(StringBuilder notes, string heading, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        notes.Append("\n\n").Append(heading);
        foreach (var line in lines)
        {
            notes.Append("\n- ").Append(line);
        }
    }

    private static string Shorten(string text, int max) =>
        text.Length <= max ? text : text[..Math.Max(0, max - 1)] + "…";
}
