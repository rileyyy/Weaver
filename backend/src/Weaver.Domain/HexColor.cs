using System.Text.RegularExpressions;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>The <c>#RRGGBB</c> colors that statuses and layers render with.</summary>
public static partial class HexColor
{
    public const int Length = 7;

    /// <summary>Trims and upper-cases <paramref name="color"/>, so equal colors compare equal.</summary>
    /// <exception cref="DomainValidationException">It isn't a <c>#RRGGBB</c> value.</exception>
    public static string Normalize(string? color)
    {
        var trimmed = color?.Trim() ?? string.Empty;
        if (!Pattern().IsMatch(trimmed))
        {
            throw new DomainValidationException("Color must be a #RRGGBB hex value.");
        }

        return trimmed.ToUpperInvariant();
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex Pattern();
}
