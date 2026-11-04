using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>What a <see cref="WorkItemHistoryField.UrlLink"/> entry holds.</summary>
internal sealed record UrlLinkHistoryValue(string Url, string? Label)
{
    public static UrlLinkHistoryValue Of(WorkItemUrlLink link) => new(link.Url, link.Label);
}
