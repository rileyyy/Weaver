using Microsoft.AspNetCore.Identity;
using Weaver.Domain;

namespace Weaver.Application.Tests;

/// <summary>The real hasher, counting verifications so tests can see work was done.</summary>
public class CountingPasswordHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> _inner = new();

    public int Verifications { get; private set; }

    public string HashPassword(User user, string password) => _inner.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        Verifications++;
        return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
