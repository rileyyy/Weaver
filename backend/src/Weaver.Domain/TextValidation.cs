using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

public static class TextValidation
{
    public static void RequireText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{fieldName} is required.");
        }

        RequireMaxLength(value, fieldName, maxLength);
    }

    public static void RequireMaxLength(string value, string fieldName, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw new DomainValidationException($"{fieldName} must be at most {maxLength} characters.");
        }
    }
}
