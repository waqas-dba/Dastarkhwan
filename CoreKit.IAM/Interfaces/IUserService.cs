using CoreKit.IAM.Models;


namespace CoreKit.IAM.Interfaces;

public interface IUserService
{
    Task<IamResult<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default);

    Task<IamResult<UserDto>> GetAsync(Guid id, CancellationToken ct = default);

    Task<IamResult<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<IamResult<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    Task<IamResult> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Replaces the user's roles with exactly the given set.</summary>
    Task<IamResult<UserDto>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct = default);
}
