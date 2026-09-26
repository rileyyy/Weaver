using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Auth;

namespace Weaver.Infrastructure.Services;

public partial class AuthService : IAuthService
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 200;
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Until the client stops firing parallel refreshes with the same token
    // (F-H1), a rotated token legitimately comes back within milliseconds.
    // Inside this window reuse is still rejected, just not treated as theft.
    private static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    private readonly WeaverDbContext _db;
    private readonly IJwtTokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        WeaverDbContext db,
        IJwtTokenService tokenService,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResult> RegisterAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = NormalizeUsername(username);
        ValidatePassword(password);

        if (await _db.Users.AnyAsync(u => u.NormalizedUsername == normalized, ct))
        {
            throw new UsernameTakenException(username);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = normalized,
            PasswordHash = string.Empty,
            Kind = UserKind.Human,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = NormalizeUsername(username);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);

        // Same generic failure for "no such user", "wrong password", and
        // "locked out" — a distinguishable response would let a caller
        // enumerate valid usernames or learn an account's lockout state.
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        if (user.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
        {
            throw new InvalidCredentialsException();
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RegisterFailedLoginAsync(user, ct);
            throw new InvalidCredentialsException();
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (stored is null || stored.User is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (IsReuseOfRotatedToken(stored))
        {
            await RevokeTokensIssuedFromAsync(stored, ct);
            throw new InvalidRefreshTokenException();
        }

        if (!stored.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        stored.RevokedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            return await IssueTokensAsync(stored.User, ct, replacing: stored);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A parallel request rotated this same token first and already
            // received the new pair.
            throw new InvalidRefreshTokenException();
        }
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        if (stored is null || stored.RevokedAtUtc is not null)
        {
            return;
        }

        stored.RevokedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A rotated token presented again after the grace period means two parties hold the same
    /// token, so one of them stole it. We can't tell which, so every token issued from it is
    /// revoked and both have to log in again. Logged-out tokens have no replacement and are
    /// simply rejected.
    /// </summary>
    private static bool IsReuseOfRotatedToken(RefreshToken token) =>
        token.ReplacedByTokenHash is not null
        && token.RevokedAtUtc is { } revokedAt
        && DateTimeOffset.UtcNow - revokedAt > RotationGracePeriod;

    private async Task RevokeTokensIssuedFromAsync(RefreshToken token, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var visited = new HashSet<string>();
        var nextHash = token.ReplacedByTokenHash;

        while (nextHash is not null && visited.Add(nextHash))
        {
            var next = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == nextHash, ct);
            if (next is null)
            {
                break;
            }

            next.RevokedAtUtc ??= now;
            nextHash = next.ReplacedByTokenHash;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The chain's newest token was rotated at the same moment. The new token is
            // still reachable from the reused one, so the next reuse revokes it.
        }
    }

    private async Task RegisterFailedLoginAsync(User user, CancellationToken ct)
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            user.LockedUntilUtc = DateTimeOffset.UtcNow.Add(LockoutDuration);
        }
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AuthResult> IssueTokensAsync(User user, CancellationToken ct, RefreshToken? replacing = null)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshTokenValue = GenerateRefreshTokenValue();
        var now = DateTimeOffset.UtcNow;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshTokenValue),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(_jwtOptions.RefreshTokenLifetime),
        };

        await RemoveExpiredTokensAsync(user.Id, now, ct);

        if (replacing is not null)
        {
            replacing.ReplacedByTokenHash = refreshToken.TokenHash;
        }

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new AuthResult(user, accessToken, refreshTokenValue);
    }

    /// <summary>
    /// Keeps the table from growing forever without a background job. Revoked tokens that
    /// haven't expired are kept, because reuse detection needs them.
    /// </summary>
    private async Task RemoveExpiredTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var expired = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAtUtc < now)
            .ToListAsync(ct);

        _db.RefreshTokens.RemoveRange(expired);
    }

    private static string GenerateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string NormalizeUsername(string username)
    {
        var trimmed = username.Trim();
        if (!UsernamePattern().IsMatch(trimmed))
        {
            throw new InvalidUsernameException(
                "Username must be 3-50 characters and contain only letters, digits, '.', '_', or '-'.");
        }
        return trimmed.ToLowerInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < MinPasswordLength)
        {
            throw new InvalidPasswordException($"Password must be at least {MinPasswordLength} characters long.");
        }
        if (password.Length > MaxPasswordLength)
        {
            throw new InvalidPasswordException($"Password must be at most {MaxPasswordLength} characters long.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,50}$")]
    private static partial Regex UsernamePattern();
}
