using Weaver.Domain;

namespace Weaver.Application.Auth;

public record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user);
}
