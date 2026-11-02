using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>A new key, with <see cref="Key"/> in plaintext. This is the only response that ever contains it.</summary>
public record IssuedApiKeyDto(ApiKeyDto ApiKey, string Key)
{
    public static IssuedApiKeyDto FromIssued(IssuedApiKey issued) => new(ApiKeyDto.FromEntity(issued.ApiKey), issued.Key);
}
