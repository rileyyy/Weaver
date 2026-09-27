using Weaver.Application.Auth;
using Weaver.Domain;

namespace Weaver.Application.Services;

public record AuthResult(User User, AccessToken AccessToken, string RefreshToken);
