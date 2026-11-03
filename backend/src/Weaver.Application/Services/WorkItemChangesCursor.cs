using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// Where a page of "changes since T" ends: the id of the first history entry (since T) of the
/// last item on the page. Entry ids only grow and an item's first entry since T never changes,
/// so the next page starts exactly after it, however many changes land in between.
/// </summary>
/// <remarks>
/// Sent to clients as an opaque token, versioned, so the position can change shape later
/// without clients having built anything on the raw id.
/// </remarks>
public readonly record struct WorkItemChangesCursor(long AfterEntryId)
{
    private const string Prefix = "v1:";

    public static WorkItemChangesCursor Start { get; } = new(0);

    public string Encode() => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(Prefix + AfterEntryId.ToString(CultureInfo.InvariantCulture)));

    /// <summary>No token is the first page.</summary>
    public static WorkItemChangesCursor Parse(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Start;
        }

        return TryDecode(token, out var afterEntryId)
            ? new WorkItemChangesCursor(afterEntryId)
            : throw new DomainValidationException("The cursor isn't one this server issued.");
    }

    private static bool TryDecode(string token, out long afterEntryId)
    {
        afterEntryId = 0;
        string text;
        try
        {
            text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException)
        {
            return false;
        }

        return text.StartsWith(Prefix, StringComparison.Ordinal)
            && long.TryParse(text.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out afterEntryId);
    }
}
