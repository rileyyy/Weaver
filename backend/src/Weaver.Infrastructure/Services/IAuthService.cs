using Weaver.Domain;
using Weaver.Infrastructure.Auth;

namespace Weaver.Infrastructure.Services;

public record AuthResult(User User, AccessToken AccessToken, string RefreshToken);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string username, string password, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
