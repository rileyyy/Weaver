namespace Weaver.Domain.Exceptions;

public class AttachmentTooLargeException(int maxFileSizeMegabytes)
    : DomainException(DomainErrorKind.TooLarge, $"The file is larger than the {maxFileSizeMegabytes} MB limit.")
{
    public int MaxFileSizeMegabytes { get; } = maxFileSizeMegabytes;
}
