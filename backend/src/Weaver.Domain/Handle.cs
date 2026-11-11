using System.Text.RegularExpressions;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// An account's <c>@</c> name, used to mention it in a comment (e.g. <c>claude-research</c>).
/// Lower-case letters and digits in words joined by single hyphens, so a mention ends cleanly at
/// punctuation: "@claude-research, please" and "ask @claude-research." both name the same handle.
/// </summary>
public static partial class Handle
{
    public const int MaxLength = 50;

    /// <summary>One handle, without its <c>@</c>; <see cref="Mentions"/> builds on it.</summary>
    internal const string Pattern = "[a-z0-9]+(?:-[a-z0-9]+)*";

    /// <summary>The trimmed handle, or null for a blank one, which means "no handle".</summary>
    public static string? Normalize(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var trimmed = handle.Trim();
        TextValidation.RequireMaxLength(trimmed, "Handle", MaxLength);
        if (!WholeHandle().IsMatch(trimmed))
        {
            throw new DomainValidationException(
                "Handle must be lower-case letters and digits, in words joined by single hyphens (e.g. claude-research).");
        }

        return trimmed;
    }

    [GeneratedRegex($"^{Pattern}$")]
    private static partial Regex WholeHandle();
}
