using Microsoft.Extensions.DependencyInjection;

namespace Weaver.Infrastructure.Attachments;

public static class AttachmentStorageStartup
{
    /// <summary>
    /// Creates the attachments directory if it's missing. Run at startup rather than on the
    /// first upload, so a path the API can't write to stops it starting instead of failing
    /// later in front of a user.
    /// </summary>
    public static void EnsureAttachmentsDirectory(this IServiceProvider services) =>
        services.GetRequiredService<FileSystemAttachmentStore>().EnsureRootExists();
}
