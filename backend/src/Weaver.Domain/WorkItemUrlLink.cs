using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A link from one work item to something outside Weaver (a PR, a quote, a product page),
/// with an optional label to show instead of the address.
/// </summary>
public class WorkItemUrlLink : IHasCreatedAt
{
    public const int UrlMaxLength = 2048;
    public const int LabelMaxLength = 200;

    /// <summary>For EF Core.</summary>
    private WorkItemUrlLink()
    {
        Url = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid WorkItemId { get; private set; }

    public string Url { get; private set; }

    public string? Label { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public WorkItem? WorkItem { get; private set; }

    public static WorkItemUrlLink Create(Guid workItemId, string url, string? label) => new()
    {
        Id = Guid.NewGuid(),
        WorkItemId = workItemId,
        Url = ValidUrl(url),
        Label = ValidLabel(label),
    };

    private static string ValidUrl(string? url)
    {
        var trimmed = url?.Trim();
        TextValidation.RequireText(trimmed, "URL", UrlMaxLength);
        if (!WebAddress.IsHttpOrHttps(trimmed!))
        {
            throw new DomainValidationException("URL must be an absolute http or https address.");
        }

        return trimmed!;
    }

    private static string? ValidLabel(string? label)
    {
        var trimmed = label?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        TextValidation.RequireMaxLength(trimmed, "Label", LabelMaxLength);
        return trimmed;
    }
}
