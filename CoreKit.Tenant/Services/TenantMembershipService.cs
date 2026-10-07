namespace CoreKit.Tenant.Services;

internal sealed class TenantMembershipService : ITenantMembershipService
{
    private readonly ITenantRepository _tenants;
    private readonly ITenantMemberRepository _members;
    private readonly ITenantAuditRecorder _audit;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ITenantEventPublisher _events;
    private readonly TenantAccessGuard _guard;
    private readonly TimeProvider _time;

    public TenantMembershipService(
        ITenantRepository tenants,
        ITenantMemberRepository members,
        ITenantAuditRecorder audit,
        ITenantUnitOfWork unitOfWork,
        ITenantEventPublisher events,
        TenantAccessGuard guard,
        TimeProvider time)
    {
        _tenants = tenants;
        _members = members;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _events = events;
        _guard = guard;
        _time = time;
    }

    public async Task<TenantResult<IReadOnlyList<TenantMemberDto>>> ListMembersAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        if (await _tenants.FindByIdAsync(tenantId, ct) is null)
            return TenantErrors.NotFound("Tenant");

        var members = await _members.ListAsync(tenantId, ct);

        return TenantResult.Success<IReadOnlyList<TenantMemberDto>>(members.Select(m => TenantMapper.ToDto(m)).ToList());
    }

    public async Task<TenantResult<TenantMemberDto>> AddMemberAsync(
        Guid tenantId, AddTenantMemberRequest request, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        if (request.UserId == Guid.Empty)
            return TenantErrors.Validation("A user id is required.");

        var tenant = await _tenants.FindByIdAsync(tenantId, ct);

        if (tenant is null)
            return TenantErrors.NotFound("Tenant");

        if (tenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        if (await _members.FindAsync(tenantId, request.UserId, ct) is not null)
            return TenantErrors.Conflict("The user is already a member of this tenant.");

        var now = Now();

        var member = new TenantMember
        {
            TenantId = tenantId,
            UserId = request.UserId,
            IsOwner = request.IsOwner,
            IsDefault = !await _members.HasDefaultAsync(request.UserId, ct),
            JoinedAt = now
        };

        _members.Add(member);
        _audit.Record(tenantId, TenantAuditActions.MemberAdded, request.IsOwner ? $"user: {request.UserId}; owner" : $"user: {request.UserId}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        await _events.PublishAsync(new TenantMemberAddedEvent(tenantId, member.UserId, member.IsOwner, now), ct);

        return TenantResult.Success(TenantMapper.ToDto(member));
    }

    public async Task<TenantResult> RemoveMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        var member = await _members.FindForUpdateAsync(tenantId, userId, ct);

        if (member is null)
            return TenantErrors.NotFound("Member");

        if (member.IsOwner && await _members.CountOwnersAsync(tenantId, ct) <= 1)
            return TenantErrors.Conflict("The last owner of a tenant cannot be removed.");

        var wasDefault = member.IsDefault;

        _members.Remove(member);

        if (wasDefault)
        {
            // Never leave a user with memberships but no default.
            var next = (await _members.ListForUserForUpdateAsync(userId, ct)).FirstOrDefault(m => m.TenantId != tenantId);

            if (next is not null)
                next.IsDefault = true;
        }

        _audit.Record(tenantId, TenantAuditActions.MemberRemoved, $"user: {userId}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        await _events.PublishAsync(new TenantMemberRemovedEvent(tenantId, userId, Now()), ct);

        return TenantResult.Success();
    }

    public async Task<TenantResult<TenantMemberDto>> SetOwnerAsync(
        Guid tenantId, Guid userId, SetTenantOwnerRequest request, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        var tenant = await _tenants.FindByIdAsync(tenantId, ct);

        if (tenant is null)
            return TenantErrors.NotFound("Tenant");

        if (tenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        var member = await _members.FindForUpdateAsync(tenantId, userId, ct);

        if (member is null)
            return TenantErrors.NotFound("Member");

        if (member.IsOwner == request.IsOwner)
            return TenantResult.Success(TenantMapper.ToDto(member));

        if (!request.IsOwner && await _members.CountOwnersAsync(tenantId, ct) <= 1)
            return TenantErrors.Conflict("The last owner of a tenant cannot lose ownership.");

        member.IsOwner = request.IsOwner;
        _audit.Record(tenantId, TenantAuditActions.OwnerChanged, $"user: {userId}; owner: {request.IsOwner}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        return TenantResult.Success(TenantMapper.ToDto(member));
    }

    public async Task<TenantResult<IReadOnlyList<TenantMembershipDto>>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (_guard.EnsureSelfOrPlatform(userId) is { } denied)
            return denied;

        var memberships = await _members.ListForUserAsync(userId, ct);

        return TenantResult.Success<IReadOnlyList<TenantMembershipDto>>(
            memberships.Select(m => TenantMapper.ToMembershipDto(m)).ToList());
    }

    public async Task<TenantResult> SetDefaultTenantAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        if (_guard.EnsureSelfOrPlatform(userId) is { } denied)
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

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}