using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Constants;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Events;
using CoreKit.Tenant.Persistence;
using CoreKit.Tenant.Services;

namespace CoreKit.Tenant.Services;

public sealed class TenantService : ITenantService
{
    private readonly ITenantRepository _tenants;
    private readonly ITenantMemberRepository _members;
    private readonly ITenantAuditRecorder _audit;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ITenantEventPublisher _events;
    private readonly ITenantInfoCache _cache;
    private readonly TenantAccessGuard _guard;
    private readonly ITenantPermissionChecker _permissions;
    private readonly TenantOptions _options;
    private readonly TimeProvider _time;
    private readonly ITenantOutbox _outbox;

    public TenantService(
        ITenantRepository tenants,
        ITenantMemberRepository members,
        ITenantAuditRecorder audit,
        ITenantUnitOfWork unitOfWork,
        ITenantEventPublisher events,
        ITenantInfoCache cache,
        TenantAccessGuard guard,
        ITenantPermissionChecker permissions,
        IOptions<TenantOptions> options,
        TimeProvider time,
        ITenantOutbox outbox)
    {
        _tenants = tenants;
        _members = members;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _events = events;
        _cache = cache;
        _guard = guard;
        _permissions = permissions;
        _options = options.Value;
        _time = time;
        _outbox = outbox;
    }

    public async Task<TenantResult<TenantPagedResult<TenantDto>>> ListAsync(
        TenantListQuery query, CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        if (!await _permissions.HasPlatformPermissionAsync(TenantPermissionNames.Platform, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.Platform);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _tenants.ListAsync(query.Search, query.Status, page, pageSize, ct);

        return TenantResult.Success(new TenantPagedResult<TenantDto>
        {
            Items = items.Select(t => TenantMapper.ToDto(t)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<TenantResult<TenantDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (await _guard.EnsureCanAccessAsync(id, ct) is { } denied)
            return denied;

        if (!await _permissions.HasPermissionAsync(TenantPermissionNames.Read, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.Read);

        var tenant = await _tenants.FindByIdAsync(id, ct);

        return tenant is null
            ? TenantErrors.NotFound("Tenant")
            : TenantResult.Success(TenantMapper.ToDto(tenant));
    }

    public async Task<TenantResult<TenantDto>> GetCurrentAsync(CancellationToken ct = default)
    {
        if (_guard.CurrentTenantId is not { } id)
            return TenantErrors.TenantRequired;

        return await GetAsync(id, ct);
    }

    public async Task<TenantResult<TenantDto>> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        if (!await _permissions.HasPlatformPermissionAsync(TenantPermissionNames.Create, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.Create);

        var nameError = TenantValidation.ValidateName(request.Name, out var name);

        if (nameError is not null)
            return nameError;

        if (request.OwnerUserId == Guid.Empty)
            return TenantErrors.Validation("The owner user id is not valid.");

        string slug;

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            var generated = await GenerateUniqueSlugAsync(name!, ct);

            if (generated is null)
                return TenantErrors.Conflict("A free slug could not be generated. Please choose one.");

            slug = generated;
        }
        else
        {
            slug = TenantSlug.Normalize(request.Slug);

            var slugError = TenantSlug.Validate(slug, _options.GetReservedSlugs());

            if (slugError is not null)
                return TenantErrors.Validation(slugError);

            if (await _tenants.SlugExistsAsync(slug, null, ct))
                return TenantErrors.Conflict("A tenant with this slug already exists.");
        }

        var now = Now();

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Name = name!,
            Slug = slug,
            Status = request.ActivateImmediately ? TenantStatus.Active : TenantStatus.Pending,
            CreatedAt = now,
            StatusChangedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };

        _tenants.Add(tenant);
        _audit.Record(tenant.Id, TenantAuditActions.Created, $"slug: {slug}; status: {tenant.Status}");

        // The tenant row and its first two events are one transaction. Without the creation event a tenant's
        // initial state would never reach the Access projection.
        _outbox.StageLifecycleEvent(
            tenant, TenantOutboxEventTypes.TenantCreated, null, tenant.Status.ToString(), now);

        TenantMember? owner = null;

        if (request.OwnerUserId is { } ownerId)
        {
            owner = new TenantMember
            {
                TenantId = tenant.Id,
                UserId = ownerId,
                IsOwner = true,
                IsDefault = !await _members.HasDefaultAsync(ownerId, ct),
                JoinedAt = now
            };

            _members.Add(owner);
            _audit.Record(tenant.Id, TenantAuditActions.MemberAdded, $"user: {ownerId}; owner");
            _outbox.StageMembershipEvent(tenant, TenantOutboxEventTypes.MemberAdded, owner, now);
        }

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        await _events.PublishAsync(new TenantCreatedEvent(tenant.Id, tenant.Slug, tenant.Name, tenant.Status, now), ct);

        if (owner is not null)
            await _events.PublishAsync(new TenantMemberAddedEvent(tenant.Id, owner.UserId, true, now), ct);

        return TenantResult.Success(TenantMapper.ToDto(tenant));
    }

    public async Task<TenantResult<TenantDto>> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        if (await _guard.EnsureCanAccessAsync(id, ct) is { } denied)
            return denied;

        if (!await _permissions.HasPermissionAsync(TenantPermissionNames.Update, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.Update);

        // Validate everything first, then change anything, so a refused request leaves nothing half-done.
        string? newName = null;

        if (request.Name is not null)
        {
            var nameError = TenantValidation.ValidateName(request.Name, out newName);

            if (nameError is not null)
                return nameError;
        }

        string? newSlug = null;

        if (request.Slug is not null)
        {
            if (await _guard.EnsurePlatformScopeAsync(ct) is { } slugDenied)
                return slugDenied;

            if (!await _permissions.HasPlatformPermissionAsync(TenantPermissionNames.Update, ct))
                return TenantErrors.Forbidden(TenantPermissionNames.Update);

            newSlug = TenantSlug.Normalize(request.Slug);

            var slugError = TenantSlug.Validate(newSlug, _options.GetReservedSlugs());

            if (slugError is not null)
                return TenantErrors.Validation(slugError);
        }

        var tenant = await _tenants.FindForUpdateAsync(id, ct);

        if (tenant is null)
            return TenantErrors.NotFound("Tenant");

        if (tenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        var changes = new List<string>();
        var oldSlug = tenant.Slug;

        if (newName is not null && newName != tenant.Name)
            changes.Add($"name: {tenant.Name} -> {newName}");

        if (newSlug is not null && newSlug != tenant.Slug)
        {
            if (await _tenants.SlugExistsAsync(newSlug, id, ct))
                return TenantErrors.Conflict("A tenant with this slug already exists.");

            changes.Add($"slug: {tenant.Slug} -> {newSlug}");
        }

        if (changes.Count == 0)
            return TenantResult.Success(TenantMapper.ToDto(tenant));

        var now = Now();

        if (newName is not null)
            tenant.Name = newName;

        if (newSlug is not null)
            tenant.Slug = newSlug;

        tenant.UpdatedAt = now;
        tenant.ConcurrencyStamp = Guid.NewGuid();

        _audit.Record(id, TenantAuditActions.Updated, string.Join("; ", changes));

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _cache.Invalidate(id, oldSlug, tenant.Slug);

        await _events.PublishAsync(new TenantUpdatedEvent(id, tenant.Slug, tenant.Name, now), ct);

        return TenantResult.Success(TenantMapper.ToDto(tenant));
    }

    public Task<TenantResult<TenantDto>> ActivateAsync(Guid id, CancellationToken ct = default)
        => ChangeStatusAsync(id, TenantStatus.Active, null, TenantAuditActions.Activated, ct);

    public Task<TenantResult<TenantDto>> SuspendAsync(Guid id, ChangeTenantStatusRequest request, CancellationToken ct = default)
        => ChangeStatusAsync(id, TenantStatus.Suspended, request.Reason, TenantAuditActions.Suspended, ct);

    public Task<TenantResult<TenantDto>> ArchiveAsync(Guid id, ChangeTenantStatusRequest request, CancellationToken ct = default)
        => ChangeStatusAsync(id, TenantStatus.Archived, request.Reason, TenantAuditActions.Archived, ct);

    public Task<TenantResult<TenantDto>> RestoreAsync(Guid id, CancellationToken ct = default)
        => ChangeStatusAsync(id, TenantStatus.Suspended, null, TenantAuditActions.Restored, ct, requireArchived: true);

    public async Task<TenantResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        if (!await _permissions.HasPlatformPermissionAsync(TenantPermissionNames.Delete, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.Delete);

        var tenant = await _tenants.FindForUpdateAsync(id, ct);

        if (tenant is null)
            return TenantErrors.NotFound("Tenant");

        if (tenant.Status != TenantStatus.Archived)
            return TenantErrors.Conflict("Archive the tenant before deleting it.");

        var now = Now();
        var slug = tenant.Slug;

        _tenants.Remove(tenant); // settings and members go with it (cascade); the audit trail stays
        _audit.Record(id, TenantAuditActions.Deleted, $"slug: {slug}");

        // The tenant row is gone after this SaveChanges, so this is the only event that can tell Access the
        // tenant no longer exists. The outbox has no foreign key to the tenant, so it survives the delete.
        _outbox.StageLifecycleEvent(
            tenant, TenantOutboxEventTypes.TenantDeleted, tenant.Status.ToString(),
            TenantLifecycleStatuses.Deleted, now);

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _cache.Invalidate(id, slug);

        await _events.PublishAsync(new TenantDeletedEvent(id, slug, now), ct);

        return TenantResult.Success();
    }

    private async Task<TenantResult<TenantDto>> ChangeStatusAsync(
        Guid id, TenantStatus target, string? reason, string action, CancellationToken ct, bool requireArchived = false)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        if (!await _permissions.HasPlatformPermissionAsync(TenantPermissionNames.ManageStatus, ct))
            return TenantErrors.Forbidden(TenantPermissionNames.ManageStatus);

        var reasonError = TenantValidation.ValidateReason(reason, out var cleanReason);

        if (reasonError is not null)
            return reasonError;

        var tenant = await _tenants.FindForUpdateAsync(id, ct);

        if (tenant is null)
            return TenantErrors.NotFound("Tenant");

        var from = tenant.Status;

        var restoring = requireArchived;

        if (!TenantLifecycle.CanTransition(from, target)
            || (restoring && from != TenantStatus.Archived)
            || (!restoring && from == TenantStatus.Archived))
            return TenantErrors.InvalidTransition(from, target);

        var now = Now();

        tenant.Status = target;
        tenant.StatusReason = target == TenantStatus.Active ? null : cleanReason;
        tenant.StatusChangedAt = now;
        tenant.UpdatedAt = now;
        tenant.ConcurrencyStamp = Guid.NewGuid();

        _audit.Record(id, action, cleanReason is null ? $"{from} -> {target}" : $"{from} -> {target}; reason: {cleanReason}");
        _outbox.StageLifecycleEvent(
            tenant, TenantOutboxEventTypes.TenantStatusChanged, from.ToString(), target.ToString(), now);

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _cache.Invalidate(id, tenant.Slug);

        await _events.PublishAsync(new TenantStatusChangedEvent(id, from, target, tenant.StatusReason, now), ct);

        return TenantResult.Success(TenantMapper.ToDto(tenant));
    }

    /// <summary>"acme-foods", then "acme-foods-2", "acme-foods-3"... Reserved and invalid candidates are skipped.</summary>
    private async Task<string?> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = TenantSlug.FromName(name);

        if (baseSlug.Length == 0)
            baseSlug = "tenant";
        else if (baseSlug.Length < TenantSlug.MinLength)
            baseSlug = "tenant-" + baseSlug;

        var reserved = _options.GetReservedSlugs();

        for (var attempt = 1; attempt <= 1000; attempt++)
        {
            var suffix = attempt == 1 ? string.Empty : "-" + attempt;

            var stem = baseSlug.Length + suffix.Length > TenantSlug.MaxLength
                ? baseSlug[..(TenantSlug.MaxLength - suffix.Length)].TrimEnd('-')
                : baseSlug;

            var candidate = stem + suffix;

            if (TenantSlug.Validate(candidate, reserved) is not null)
                continue;

            if (!await _tenants.SlugExistsAsync(candidate, null, ct))
                return candidate;
        }

        return null;
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}