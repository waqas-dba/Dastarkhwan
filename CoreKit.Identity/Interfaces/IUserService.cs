using CoreKit.Identity.Models;


namespace CoreKit.Identity.Interfaces;

public interface IUserService
{
    Task<IdentityResult<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default);

    Task<IdentityResult<UserDto>> GetAsync(Guid id, CancellationToken ct = default);

    Task<IdentityResult<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<IdentityResult<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    Task<Common.IdentityResult> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Replaces the user's roles with exactly the given set.</summary>
    Task<IdentityResult<UserDto>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct = default);
}
