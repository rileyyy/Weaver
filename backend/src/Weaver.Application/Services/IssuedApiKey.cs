using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>A newly issued key and its plaintext, which is never available again.</summary>
public sealed record IssuedApiKey(ApiKey ApiKey, string Key);
