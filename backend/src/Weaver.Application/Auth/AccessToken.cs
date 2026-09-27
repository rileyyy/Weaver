namespace Weaver.Application.Auth;

public record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);
