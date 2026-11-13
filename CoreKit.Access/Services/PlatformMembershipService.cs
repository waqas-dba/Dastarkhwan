using CoreKit.Access.Abstractions;
using CoreKit.Access.Constants;
using CoreKit.Access.Entities;
using CoreKit.Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Access.Services;

/// <summary>
/// Manages platform membership records. Membership exists independently of role assignments:
/// a platform member with no assignments holds no permissions.
/// </summary>
internal sealed class PlatformMembershipService : IPlatformMembershipService
{
    private readonly AccessDbContext _db;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly IAccessAuditRecorder _audit;
    private readonly IAccessCurrentUser _actor;
    private readonly IAccessCache _cache;
    private readonly TimeProvider _time;
    private readonly AccessGuard _guard;

    public PlatformMembershipService(
        AccessDbContext db,
        IAccessUnitOfWork unitOfWork,
        IAccessAuditRecorder audit,
        IAccessCurrentUser actor,
        IAccessCache cache,
        TimeProvider time,
        AccessGuard guard)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _actor = actor;
        _cache = cache;
        _time = time;
        _guard = guard;
    }

    public async Task<PlatformMemberResult> AddMemberAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return PlatformMemberResult.Fail("validation.failed", "A user id is required.");

        var guardResult = await _guard.RequirePlatformScopePermissionAsync(AccessPermissionNames.PlatformMembersManage, ct);
        if (guardResult.IsFailure)
            return PlatformMemberResult.Fail("access.denied", guardResult.Error!.Message);

        var existing = await _db.PlatformMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId, ct);

        var now = _time.GetUtcNow().UtcDateTime;

        if (existing is not null)
        {
            if (existing.RevokedAt is null)
                return PlatformMemberResult.Ok(); // already an active member

            // Reactivate with a new generation so old assignments stay inactive.
            existing.RevokedAt = null;
            existing.MembershipGeneration++;
            existing.JoinedAt = now;
        }
        else
        {
            var generation = await NextGenerationAsync(ct);
            _db.PlatformMembers.Add(new AccessPlatformMember
            {
                UserId = userId,
                PlatformId = AccessScope.PlatformId,
                MembershipGeneration = generation,
                JoinedAt = now,
                RevokedAt = null
            });
        }

        _audit.Record(AccessScope.ForPlatform(AccessScope.PlatformId),
            AccessAuditActions.MemberAdded, $"user: {userId}");

        try
        {
            if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
                return PlatformMemberResult.Fail(saveError.Code, saveError.Message);
        }
        catch (DbUpdateException)
        {
            // Concurrent AddMember for the same user: another request inserted first.
            // Reread the persisted state and succeed if the member is now active.
            var raced = await _db.PlatformMembers
                .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId, ct);
            if (raced is not null && raced.RevokedAt is null)
            {
                _cache.InvalidateUser(AccessScope.ForPlatform(AccessScope.PlatformId), userId);
                return PlatformMemberResult.Ok();
            }
            throw;
        }

        _cache.InvalidateUser(AccessScope.ForPlatform(AccessScope.PlatformId), userId);
        return PlatformMemberResult.Ok();
    }

    public async Task<PlatformMemberResult> RevokeMemberAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return PlatformMemberResult.Fail("validation.failed", "A user id is required.");

        var guardResult = await _guard.RequirePlatformScopePermissionAsync(AccessPermissionNames.PlatformMembersManage, ct);
        if (guardResult.IsFailure)
            return PlatformMemberResult.Fail("access.denied", guardResult.Error!.Message);

        var existing = await _db.PlatformMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId, ct);

        if (existing is null || existing.RevokedAt is not null)
            return PlatformMemberResult.Ok(); // already not a member

        existing.RevokedAt = _time.GetUtcNow().UtcDateTime;
        existing.MembershipGeneration++;

        _audit.Record(AccessScope.ForPlatform(AccessScope.PlatformId),
            AccessAuditActions.MemberRevoked, $"user: {userId}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return PlatformMemberResult.Fail(saveError.Code, saveError.Message);

        _cache.InvalidateUser(AccessScope.ForPlatform(AccessScope.PlatformId), userId);
        return PlatformMemberResult.Ok();
    }

    public async Task<bool> IsMemberAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return false;

        return await _db.PlatformMembers
            .AnyAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId && m.RevokedAt == null, ct);
    }

    private async Task<long> NextGenerationAsync(CancellationToken ct)
    {
        var max = await _db.PlatformMembers
            .Where(m => m.PlatformId == AccessScope.PlatformId)
            .MaxAsync(m => (long?)m.MembershipGeneration, ct);
        return (max ?? 0L) + 1L;
    }
}
