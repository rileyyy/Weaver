using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// A paging position sent to clients as an opaque token. The prefix names which listing issued it
/// and its version, so a token from one listing is refused by another, and a position can change
/// shape later without clients having built anything on the raw number.
/// </summary>
internal static class OpaqueCursor
{
    public static string Encode(string prefix, long position) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(prefix + position.ToString(CultureInfo.InvariantCulture)));

    /// <summary>No token is the first page, at <paramref name="start"/>. A position above <paramref name="max"/> was never issued.</summary>
    public static long Parse(string prefix, string? token, long start, long max = long.MaxValue)
    {
        if (string.IsNullOrEmpty(token))
        {
            return start;
        }

        return TryDecode(prefix, token, out var position) && position <= max
            ? position
            : throw new DomainValidationException("The cursor isn't one this server issued.");
    }

    private static bool TryDecode(string prefix, string token, out long position)
    {
        position = 0;
        string text;
        try
        {
            text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException)
        {
            return false;
        }

        return text.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(text.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out position);
    }
}
