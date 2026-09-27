using Weaver.Application.Auth;
using Weaver.Domain;

namespace Weaver.Application.Services;

public record AuthResult(User User, AccessToken AccessToken, string RefreshToken);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string username, string password, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
