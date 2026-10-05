using System.Security.Cryptography;
using System.Text;

namespace Iam.Core.Security;

public static class ConstantTime
{
    /// <summary>
    /// Compares two secrets in time that does not depend on where they first differ.
    /// A normal == stops at the first different character, which can leak information to an attacker.
    /// </summary>
    public static bool AreEqual(string? a, string? b)
    {
        if (a is null || b is null) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }
}