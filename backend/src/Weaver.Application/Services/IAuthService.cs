namespace Weaver.Application.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string username, string password, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
