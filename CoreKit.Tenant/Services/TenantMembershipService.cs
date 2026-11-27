using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Constants;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Events;
using CoreKit.Tenant.Persistence;

namespace CoreKit.Tenant.Services;

public sealed class TenantMembershipService : ITenantMembershipService
{
    private readonly ITenantRepository _tenants;
    private readonly ITenantMemberRepository _members;
    private readonly ITenantAuditRecorder _audit;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ITenantEventPublisher _events;
    private readonly TenantAccessGuard _guard;
    private readonly ITenantPlatformAccess _platform;
    private readonly ITenantActor _actor;
    private readonly ITenantPermissionChecker _permissions;
    private readonly TimeProvider _time;
    private readonly ITenantMembershipCache _membershipCache;
    private readonly ITenantOutbox _outbox;

    public TenantMembershipService(
        ITenantRepository tenants,
        ITenantMemberRepository members,
        ITenantAuditRecorder audit,
        ITenantUnitOfWork unitOfWork,
        ITenantEventPublisher events,
        TenantAccessGuard guard,
        ITenantPlatformAccess platform,
        ITenantActor actor,
        ITenantPermissionChecker permissions,
        TimeProvider time,
        ITenantMembershipCache membershipCache,
        ITenantOutbox outbox)
    {
        _tenants = tenants;
        _members = members;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _events = events;
        _guard = guard;
        _platform = platform;
        _actor = actor;
        _permissions = permissions;
        _time = time;
        _membershipCache = membershipCache;
        _outbox = outbox;
    }

    public async Task<TenantResult<IReadOnlyList<TenantMemberDto>>> ListMembersAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (await _guard.EnsureCanAccessAsync(tenantId, ct) is { } denied)
            return denied;

        if (!await _permissions.HasPermissionAsync(TenantPermissionNames.MembersRead, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.MembersRead);

        if (await _tenants.FindByIdAsync(tenantId, ct) is null)
            return TenantErrors.NotFound("Tenant");

        var members = await _members.ListAsync(tenantId, ct);

        return TenantResult.Success<IReadOnlyList<TenantMemberDto>>(members.Select(m => TenantMapper.ToDto(m)).ToList());
    }

    public async Task<TenantResult<TenantMemberDto>> AddMemberAsync(
        Guid tenantId, AddTenantMemberRequest request, CancellationToken ct = default)
    {
        if (await _guard.EnsureCanAccessAsync(tenantId, ct) is { } denied)
            return denied;

        if (!await _permissions.HasFreshPermissionAsync(TenantPermissionNames.MembersManage, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.MembersManage);

        // F1: Require an identified actor for ownership assignment.
        // F2: Prevent self-promotion to owner.
        if (request.IsOwner)
        {
            if (_actor.UserId is null || _actor.UserId == Guid.Empty)
                return TenantErrors.Forbidden("An identified actor is required to assign ownership.");
            if (_actor.UserId == request.UserId)
                return TenantErrors.Forbidden("You cannot promote yourself to owner.");
            if (!await _platform.IsGrantedAsync(ct))
            {
                var caller = await _members.FindAsync(tenantId, _actor.UserId.Value, ct);
                if (caller is null || !caller.IsOwner)
                    return TenantErrors.Forbidden("Only a tenant owner or platform administrator can assign ownership.");
            }
        }

        if (request.UserId == Guid.Empty)
            return TenantErrors.Validation("A user id is required.");

        // The tenant is always loaded tracked: the outbox sequence lives on the tenant row and its
        // concurrency stamp is what makes two concurrent membership writers fail instead of colliding.
        var tenant = await _tenants.FindForUpdateAsync(tenantId, ct);
        if (tenant is null)
            return TenantErrors.NotFound("Tenant");
        if (tenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        var now = Now();
        TenantMember member;
        string eventType;

        if (await _members.FindIncludingRemovedAsync(tenantId, request.UserId, ct) is { } existing)
        {
            if (existing.RemovedAt is null)
                return TenantErrors.Conflict("The user is already a member of this tenant.");

            // Reactivate a previously removed member with a new generation so old Access role
            // assignments stay inactive until explicitly re-assigned.
            existing.RemovedAt = null;
            existing.MembershipGeneration++;
            existing.JoinedAt = now;
            existing.IsOwner = request.IsOwner;
            existing.IsDefault = !await _members.HasDefaultAsync(request.UserId, ct);

            member = existing;
            eventType = TenantOutboxEventTypes.MemberReactivated;
        }
        else
        {
            var memberEntity = new TenantMember
            {
                TenantId = tenantId,
                UserId = request.UserId,
                IsOwner = request.IsOwner,
                IsDefault = !await _members.HasDefaultAsync(request.UserId, ct),
                JoinedAt = now,
                MembershipGeneration = 1
            };
            _members.Add(memberEntity);
            member = memberEntity;
            eventType = TenantOutboxEventTypes.MemberAdded;
        }

        _audit.Record(tenantId, TenantAuditActions.MemberAdded, request.IsOwner ? $"user: {request.UserId}; owner" : $"user: {request.UserId}");

        // Same SaveChanges as the membership change. StageMembershipEvent also rotates the tenant's
        // concurrency stamp (F3), so no separate stamp update is needed here.
        _outbox.StageMembershipEvent(tenant, eventType, member, now);

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _membershipCache.Invalidate(tenantId, member.UserId);

        // In-process notification only. It is NOT durable delivery: durable delivery is the outbox row above.
        await _events.PublishAsync(new TenantMemberAddedEvent(tenantId, member.UserId, member.IsOwner, now), ct);

        return TenantResult.Success(TenantMapper.ToDto(member));
    }

    public async Task<TenantResult> RemoveMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        if (await _guard.EnsureCanAccessAsync(tenantId, ct) is { } denied)
            return denied;

        if (!await _permissions.HasFreshPermissionAsync(TenantPermissionNames.MembersManage, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.MembersManage);

        // F3: Load and track the tenant BEFORE reading the membership state, so the
        // concurrency stamp is captured before any ownership decision is made.
        var trackedTenant = await _tenants.FindForUpdateAsync(tenantId, ct);
        if (trackedTenant is null)
            return TenantErrors.NotFound("Tenant");

        if (trackedTenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        var member = await _members.FindForUpdateAsync(tenantId, userId, ct);

        if (member is null)
            return TenantErrors.NotFound("Member");

        // F1: Require an identified actor for owner removal.
        // Only tenant owners or platform administrators can remove an owner.
        if (member.IsOwner)
        {
            if (_actor.UserId is null || _actor.UserId == Guid.Empty)
                return TenantErrors.Forbidden("An identified actor is required to remove an owner.");

            if (!await _platform.IsGrantedAsync(ct))
            {
                var caller = await _members.FindAsync(tenantId, _actor.UserId.Value, ct);
                if (caller is null || !caller.IsOwner)
                    return TenantErrors.Forbidden("Only a tenant owner or platform administrator can remove an owner.");
            }
        }

        if (member.IsOwner && await _members.CountOwnersAsync(tenantId, ct) <= 1)
            return TenantErrors.Conflict("The last owner of a tenant cannot be removed.");

        var wasDefault = member.IsDefault;
        var removedAt = _time.GetUtcNow().UtcDateTime;

        // Soft-delete: keep the row (and its generation) so Access can enforce that old
        // assignments stay inactive after the member rejoins with a new generation.
        member.RemovedAt = removedAt;
        member.MembershipGeneration++;

        if (wasDefault)
        {
            // Never leave a user with memberships but no default.
            var next = (await _members.ListForUserForUpdateAsync(userId, ct)).FirstOrDefault(m => m.TenantId != tenantId);

            if (next is not null)
                next.IsDefault = true;
        }

        _audit.Record(tenantId, TenantAuditActions.MemberRemoved, $"user: {userId}");

        // F3: StageMembershipEvent rotates the concurrency stamp for every successful removal,
        // including removal of a member initially read as a non-owner.
        _outbox.StageMembershipEvent(trackedTenant, TenantOutboxEventTypes.MemberRemoved, member, removedAt);

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _membershipCache.Invalidate(tenantId, userId);

        // In-process notification only. Durable delivery is the outbox row staged above.
        await _events.PublishAsync(new TenantMemberRemovedEvent(tenantId, userId, Now()), ct);

        return TenantResult.Success();
    }

    public async Task<TenantResult<TenantMemberDto>> SetOwnerAsync(
        Guid tenantId, Guid userId, SetTenantOwnerRequest request, CancellationToken ct = default)
    {
        if (await _guard.EnsureCanAccessAsync(tenantId, ct) is { } denied)
            return denied;

        if (!await _permissions.HasFreshPermissionAsync(TenantPermissionNames.MembersManage, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.MembersManage);

        // F3: Load and track the tenant BEFORE the owner-count decision, so the
        // concurrency stamp is captured before any ownership decision is made.
        var trackedTenant = await _tenants.FindForUpdateAsync(tenantId, ct);
        if (trackedTenant is null)
            return TenantErrors.NotFound("Tenant");

        if (trackedTenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        var member = await _members.FindForUpdateAsync(tenantId, userId, ct);
        if (member is null)
            return TenantErrors.NotFound("Member");

        if (member.IsOwner == request.IsOwner)
            return TenantResult.Success(TenantMapper.ToDto(member));

        // F1: Require an identified actor for all ownership changes, even for platform admins.
        if (_actor.UserId is null || _actor.UserId == Guid.Empty)
            return TenantErrors.Forbidden("An identified actor is required to change ownership.");

        // F2: Prevent self-promotion to owner.
        if (request.IsOwner && _actor.UserId == userId)
            return TenantErrors.Forbidden("You cannot promote yourself to owner.");

        // F1: Only tenant owners or platform administrators can change ownership.
        if (!await _platform.IsGrantedAsync(ct))
        {
            var caller = await _members.FindAsync(tenantId, _actor.UserId.Value, ct);
            if (caller is null || !caller.IsOwner)
                return TenantErrors.Forbidden("Only a tenant owner or platform administrator can change ownership.");
        }

        if (!request.IsOwner && await _members.CountOwnersAsync(tenantId, ct) <= 1)
            return TenantErrors.Conflict("The last owner of a tenant cannot lose ownership.");

        member.IsOwner = request.IsOwner;
        _audit.Record(tenantId, TenantAuditActions.OwnerChanged, $"user: {userId}; owner: {request.IsOwner}");

        // Ownership does not change the generation. StageMembershipEvent rotates the concurrency stamp (F3).
        _outbox.StageMembershipEvent(trackedTenant, TenantOutboxEventTypes.MemberOwnershipChanged, member, Now());

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _membershipCache.Invalidate(tenantId, userId);
        return TenantResult.Success(TenantMapper.ToDto(member));
    }

    public async Task<TenantResult<IReadOnlyList<TenantMembershipDto>>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (await _guard.EnsureSelfOrPlatformAsync(userId, ct) is { } denied)
            return denied;

        var memberships = await _members.ListForUserAsync(userId, ct);

        return TenantResult.Success<IReadOnlyList<TenantMembershipDto>>(
            memberships.Select(m => TenantMapper.ToMembershipDto(m)).ToList());
    }

    public async Task<TenantResult> SetDefaultTenantAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        if (await _guard.EnsureSelfOrPlatformAsync(userId, ct) is { } denied)
            return denied;

        var memberships = await _members.ListForUserForUpdateAsync(userId, ct);
        var target = memberships.FirstOrDefault(m => m.TenantId == tenantId);

        if (target is null)
            return TenantErrors.NotFound("Membership");

        foreach (var membership in memberships)
            membership.IsDefault = membership.TenantId == tenantId;

        _audit.Record(tenantId, TenantAuditActions.DefaultChanged, $"user: {userId}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        return TenantResult.Success();
    }

    public async Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _members.FindAsync(tenantId, userId, ct) is not null;

    public async Task<int> RemoveAllMembershipsForDeletedUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Find all membership rows for this user (including soft-deleted ones)
        var memberships = await _members.ListForUserIncludingRemovedAsync(userId, ct);

        if (memberships.Count == 0)
            return 0;

        // Hard-delete all membership rows since the user no longer exists in Identity
        foreach (var membership in memberships)
        {
            _members.Remove(membership);
        }

        await _unitOfWork.SaveAsync(ct);
        return memberships.Count;
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}