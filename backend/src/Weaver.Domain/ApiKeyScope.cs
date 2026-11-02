namespace Weaver.Domain;

/// <summary>What an API key may do. Ordered so the zero value is the safer one.</summary>
public enum ApiKeyScope
{
    ReadOnly,
    ReadWrite,
}
