using System.Globalization;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>A work item's <see cref="WorkItem.Number"/> as people type it: <c>#123</c>.</summary>
public static class WorkItemNumberText
{
    public static bool TryParse(string? text, out int number)
    {
        number = 0;
        var trimmed = text?.Trim();
        return trimmed is ['#', _, ..]
            && int.TryParse(trimmed.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
