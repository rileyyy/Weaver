using System.Text;
using Weaver.Runner.Model;

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

    /// <summary>
    /// The reply when there's no answer to give, which still clears the mention, with anything
    /// already noted as added (by an earlier attempt at the same mention).
    /// </summary>
    public string RenderCouldNotAnswer(string reason) => Render($"I couldn't answer this request: {reason}");

    public void Added(string what) => _added.Add(what);

    public void NotDone(string what, string why) => _notDone.Add($"{what}: {TextShortener.Shorten(why, MaxReasonLength)}");

    public string Render(string modelReply)
    {
        var notes = new StringBuilder();
        AppendSection(notes, "Added:", _added);
        AppendSection(notes, "Not done:", _notDone);
        // The whole comment is shortened too: a reply Weaver refuses as too long would leave the
        // mention pending, and every later run would be refused the same way.
        var reply = TextShortener.Shorten(modelReply, Math.Max(MinReplyLength, MaxLength - notes.Length));
        return TextShortener.Shorten(reply + notes, MaxLength);
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
}
