namespace CoreKit.IAM.Security;

/// <summary>
/// Wraps ASP.NET Core's built-in hasher (PBKDF2 with HMAC-SHA512, random salt per hash,
/// settings stored inside the hash). We do not invent any cryptography.
/// </summary>
public sealed class PasswordHasher : Interfaces.IPasswordHasher
{
    /// <summary>OWASP's recommended minimum for PBKDF2 with HMAC-SHA512.</summary>
    public const int DefaultIterationCount = 210_000;

    private sealed class HashSubject { }

    private static readonly HashSubject Subject = new();

    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<HashSubject> _inner;

    public PasswordHasher() : this(DefaultIterationCount) { }

    public PasswordHasher(int iterationCount)
    {
        _inner = new Microsoft.AspNetCore.Identity.PasswordHasher<HashSubject>(
            Options.Create(new PasswordHasherOptions { IterationCount = iterationCount }));
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _inner.HashPassword(Subject, password);
    }

    public PasswordCheckResult Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(password))
            return PasswordCheckResult.Failed;

        return _inner.VerifyHashedPassword(Subject, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheckResult.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
            _ => PasswordCheckResult.Failed
        };
    }
}