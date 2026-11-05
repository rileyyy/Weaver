namespace Weaver.Application.Attachments;

/// <summary>
/// Bound from the "Attachments" configuration section. The path is separate from Postgres so
/// it can live on other storage; backups must cover it as well as the database (ADR-0008).
/// </summary>
public class AttachmentOptions
{
    public const string SectionName = "Attachments";

    private const long BytesPerMegabyte = 1024 * 1024;

    public string Path { get; set; } = "/data/attachments";

    public int MaxFileSizeMegabytes { get; set; } = 25;

    public long MaxFileSizeBytes => MaxFileSizeMegabytes * BytesPerMegabyte;

    /// <summary>Fails startup on a setting that would make every upload fail or be refused.</summary>
    public void EnsureUsable()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            throw new InvalidOperationException(
                "Attachments:Path is not configured. Set it via the Attachments__Path environment variable.");
        }

        if (MaxFileSizeMegabytes <= 0)
        {
            throw new InvalidOperationException(
                "Attachments:MaxFileSizeMegabytes must be a whole number of megabytes greater than zero.");
        }
    }
}
