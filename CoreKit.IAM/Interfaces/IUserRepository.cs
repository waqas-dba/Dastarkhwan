using CoreKit.IAM.Entities;

namespace CoreKit.IAM.Interfaces;

/// <summary>Persistence for users. Nothing outside the repositories should touch the DbContext.</summary>
public interface IUserRepository
{
    /// <summary>Read-only, without roles.</summary>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Read-only, with roles.</summary>
    Task<User?> FindByIdWithRolesAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tracked, with roles. Use it when the caller will change the user and call UpdateAsync.</summary>
    Task<User?> FindForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Read-only, with roles.</summary>
    Task<User?> FindByNormalizedEmailWithRolesAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>The id of an active user with this email whose address is not confirmed yet.</summary>
    Task<Guid?> FindUnconfirmedActiveIdByEmailAsync(string normalizedEmail, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId = null, CancellationToken ct = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Ids of active users holding the given active role.</summary>
    Task<IReadOnlyList<Guid>> GetActiveUserIdsInRoleAsync(string normalizedRoleName, CancellationToken ct = default);

    /// <summary>Returns false when another user already has the same normalized email.</summary>
    Task<bool> CreateAsync(User user, CancellationToken ct = default);

    /// <summary>Saves changes made to a tracked user. Returns false on a duplicate email.</summary>
    Task<bool> UpdateAsync(User user, CancellationToken ct = default);

    Task SetPasswordHashAsync(Guid userId, string passwordHash, DateTime now, CancellationToken ct = default);

    /// <summary>Makes the user's role links match exactly the given role ids.</summary>
    Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default);

    Task DeleteAsync(User user, CancellationToken ct = default);
}