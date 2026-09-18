using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record UserDto(Guid Id, string Username, UserKind Kind)
{
    public static UserDto FromEntity(User user) => new(user.Id, user.Username, user.Kind);
}

public record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, UserDto User)
{
    public static AuthResponse FromResult(Weaver.Infrastructure.Services.AuthResult result) => new(
        result.AccessToken.Value,
        result.AccessToken.ExpiresAtUtc,
        result.RefreshToken,
        UserDto.FromEntity(result.User));
}

public record RegisterRequest(string Username, string Password);

public record LoginRequest(string Username, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);
