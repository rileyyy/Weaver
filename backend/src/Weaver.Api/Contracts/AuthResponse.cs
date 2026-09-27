using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

public record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, UserDto User)
{
    public static AuthResponse FromResult(AuthResult result) => new(
        result.AccessToken.Value,
        result.AccessToken.ExpiresAtUtc,
        result.RefreshToken,
        UserDto.FromEntity(result.User));
}
