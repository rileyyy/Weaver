using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record CreateApiKeyRequest(string Label, ApiKeyScope Scope);
