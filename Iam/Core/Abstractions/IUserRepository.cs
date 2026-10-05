using Iam.Core.Entities;
using Iam.Core.Identity;

namespace Iam.Core.Abstractions;

public interface IUserRepository
{
    /// <summary>Loads a tracked user with their role links.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Finds a user by email or phone (already normalised) and loads their role links.</summary>
    Task<User?> GetByLoginAsync(LoginIdentifier identifier, CancellationToken ct = default);

    Task<bool> LoginExistsAsync(LoginIdentifier identifier, CancellationToken ct = default);

    /// <summary>Every permission code the user holds through any of their roles, without duplicates.</summary>
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default);

    void Add(User user);
}