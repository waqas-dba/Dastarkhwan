namespace CoreKit.Tenant.Abstractions;

/// <summary>Who is acting. The IAM integration provides this from the signed-in user.</summary>
public interface ITenantActor
{
    Guid? UserId { get; }

    /// <summary>True when the caller may act across all tenants.</summary>
    bool IsPlatformAdmin { get; }
}

/// <summary>
/// Decides whether the current code may do platform-level work (create tenants, change status, ...).
/// Platform administrators have it automatically. Trusted code such as a sign-up flow or a seeder can
/// take it for a while with <see cref="Grant"/>.
/// </summary>
public interface ITenantPlatformAccess
{
    bool IsGranted { get; }

    IDisposable Grant();
}