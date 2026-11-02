using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>Who the caller is signed in as. The key fields are null when it signed in with a password.</summary>
public record WhoAmIDto(string AccountName, UserKind AccountKind, string? KeyLabel, ApiKeyScope? Scope)
{
    public static WhoAmIDto FromIdentity(CallerIdentity identity) => new(
        identity.Account.Username,
        identity.Account.Kind,
        identity.ApiKey?.Label,
        identity.ApiKey?.Scope);
}
