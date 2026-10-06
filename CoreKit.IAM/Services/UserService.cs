
using CoreKit.IAM.Common;
using CoreKit.IAM.Constants;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Normalization;
using CoreKit.IAM.Persistence;
using CoreKit.IAM.Security;
using CoreKit.IAM.Validation;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CoreKit.IAM.Services;

public sealed class UserService : IUserService
{
    private const int MaxDisplayNameLength = 200;

    private readonly IamDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;

    public UserService(
        IamDbContext db,
        IPasswordHasher hasher,
        ICurrentUserService currentUser,
        TimeProvider time)
    {
        _db = db;
        _hasher = hasher;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<IamResult<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var users = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();

            users = users.Where(u =>
                u.NormalizedEmail.Contains(term) ||
                (u.DisplayName != null && u.DisplayName.ToUpper().Contains(term)));
        }

        if (query.IsActive.HasValue)
            users = users.Where(u => u.IsActive == query.IsActive.Value);

        var totalCount = await users.CountAsync(ct);

        var items = await users
            .OrderBy(u => u.NormalizedEmail)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync(ct);

        return IamResult.Success(new PagedResult<UserDto>
        {
            Items = items.Select(u => IamMapper.ToDto(u)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<IamResult<UserDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        var (_, permissions) = await IamMapper.ResolveAccessAsync(_db, user, ct);

        return IamResult.Success(IamMapper.ToDto(user, permissions));
    }

    public async Task<IamResult<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;

        if (!EmailValidator.IsValid(email))
            return IamErrors.Validation("A valid email address is required.");

        var passwordError = PasswordPolicy.Validate(request.Password, email);

        if (passwordError is not null)
            return IamErrors.Validation(passwordError);

        var nameError = ValidateDisplayName(request.DisplayName, out var displayName);

        if (nameError is not null)
            return nameError;

        var roleIds = (request.RoleIds ?? new List<Guid>()).Distinct().ToList();

        var roles = roleIds.Count == 0
            ? new List<Role>()
            : await _db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(ct);

        if (roles.Count != roleIds.Count)
            return IamErrors.Validation("One or more roles do not exist.");

        var normalizedEmail = IamNormalizer.NormalizeEmail(email);

        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct))
            return IamErrors.Conflict("A user with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = _hasher.Hash(request.Password),
            DisplayName = displayName,
            IsActive = request.IsActive,
            EmailConfirmed = false,
            CreatedAt = Now()
        };

        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        _db.Users.Add(user);

        var saveError = await TrySaveAsync(normalizedEmail, ct);

        if (saveError is not null)
            return saveError;

        return await GetAsync(user.Id, ct);
    }

    public async Task<IamResult<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        var now = Now();
        string? changedEmail = null;
        var endSessions = false;

        if (request.Email is not null)
        {
            var email = request.Email.Trim();

            if (!EmailValidator.IsValid(email))
                return IamErrors.Validation("A valid email address is required.");

            var normalized = IamNormalizer.NormalizeEmail(email);

            if (normalized != user.NormalizedEmail)
            {
                if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalized && u.Id != id, ct))
                    return IamErrors.Conflict("A user with this email already exists.");

                user.NormalizedEmail = normalized;
                user.EmailConfirmed = false; // a new address has not been confirmed yet
                changedEmail = normalized;
            }

            user.Email = email;
        }

        if (request.DisplayName is not null)
        {
            var nameError = ValidateDisplayName(request.DisplayName, out var displayName);

            if (nameError is not null)
                return nameError;

            user.DisplayName = displayName;
        }

        if (request.IsActive is { } isActive && isActive != user.IsActive)
        {
            if (!isActive)
            {
                if (_currentUser.UserId == id)
                    return IamErrors.Forbidden("You cannot deactivate your own account.");

                if (await AdministratorGuard.IsLastActiveAdministratorAsync(_db, id, ct))
                    return IamErrors.Conflict("The last active administrator cannot be deactivated.");

                endSessions = true;
            }

            user.IsActive = isActive;
        }

        if (request.Password is not null)
        {
            var passwordError = PasswordPolicy.Validate(request.Password, user.Email);

            if (passwordError is not null)
                return IamErrors.Validation(passwordError);

            user.PasswordHash = _hasher.Hash(request.Password);
            endSessions = true;
        }

        user.UpdatedAt = now;

        var saveError = await TrySaveAsync(changedEmail, ct);

        if (saveError is not null)
            return saveError;

        if (endSessions)
            await RefreshTokenRevocation.RevokeAllForUserAsync(_db, id, now, ct);

        return await GetAsync(id, ct);
    }

    public async Task<IamResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId == id)
            return IamErrors.Forbidden("You cannot delete your own account.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        if (await AdministratorGuard.IsLastActiveAdministratorAsync(_db, id, ct))
            return IamErrors.Conflict("The last active administrator cannot be deleted.");

        _db.Users.Remove(user); // role links and refresh tokens are removed by the database (cascade)
        await _db.SaveChangesAsync(ct);

        return IamResult.Success();
    }

    public async Task<IamResult<UserDto>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        var wanted = (request.RoleIds ?? new List<Guid>()).Distinct().ToList();

        var roles = wanted.Count == 0
            ? new List<Role>()
            : await _db.Roles.Where(r => wanted.Contains(r.Id)).ToListAsync(ct);

        if (roles.Count != wanted.Count)
            return IamErrors.Validation("One or more roles do not exist.");

        var administrator = IamNormalizer.NormalizeName(IamRoleNames.Administrator);

        var losesAdministrator =
            user.UserRoles.Any(ur => ur.Role.NormalizedName == administrator) &&
            roles.All(r => r.NormalizedName != administrator);

        if (losesAdministrator && await AdministratorGuard.IsLastActiveAdministratorAsync(_db, id, ct))
            return IamErrors.Conflict("The last active administrator cannot lose the Administrator role.");

        foreach (var link in user.UserRoles.Where(ur => !wanted.Contains(ur.RoleId)).ToList())
            _db.UserRoles.Remove(link);

        var current = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();

        foreach (var role in roles.Where(r => !current.Contains(r.Id)))
            _db.UserRoles.Add(new UserRole { UserId = id, RoleId = role.Id });

        user.UpdatedAt = Now();
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private static IamError? ValidateDisplayName(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxDisplayNameLength }
            ? IamErrors.Validation($"The display name is too long (max {MaxDisplayNameLength} characters).")
            : null;
    }

    /// <summary>
    /// Two requests can both pass the "email is free" check. The unique index then rejects the second
    /// one, and this turns that database error into a clear Conflict.
    /// </summary>
    private async Task<IamError?> TrySaveAsync(string? emailToCheck, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException) when (emailToCheck is not null)
        {
            _db.ChangeTracker.Clear();

            if (await _db.Users.AnyAsync(u => u.NormalizedEmail == emailToCheck, ct))
                return IamErrors.Conflict("A user with this email already exists.");

            throw;
        }
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}