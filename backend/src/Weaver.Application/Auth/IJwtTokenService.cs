using Weaver.Domain;

namespace Weaver.Application.Auth;

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user);
}
