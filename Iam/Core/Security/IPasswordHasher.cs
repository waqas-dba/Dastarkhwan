namespace Iam.Core.Security;

/// <summary>
/// A "port": Core says what it needs, and a later batch supplies the implementation
/// (BCrypt or Argon2). Core never knows which algorithm is used.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);

    /// <summary>True when the stored hash uses old settings and should be re-hashed after a successful login.</summary>
    bool NeedsRehash(string passwordHash);
}