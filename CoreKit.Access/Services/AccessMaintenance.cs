namespace CoreKit.Access.Services;

internal sealed class AccessMaintenance : IAccessMaintenance
{
    private readonly IAccessRoleRepository _roles;
    private readonly IAccessAssignmentRepository _assignments;
    private readonly IAccessAuditRecorder _audit;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly IAccessCache _cache;
    private readonly AccessCatalog _catalog;
    private readonly TimeProvider _time;

    public AccessMaintenance(
        IAccessRoleRepository roles,
        IAccessAssignmentRepository assignments,
        IAccessAuditRecorder audit,
        IAccessUnitOfWork unitOfWork,
        IAccessCache cache,
        AccessCatalog catalog,
        TimeProvider time)
    {
        _roles = roles;
        _assignments = assignments;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _catalog = catalog;
        _time = time;
    }

    public async Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        if (!scope.IsValid || userId == Guid.Empty)
            return;

        await _assignments.RemoveUserAsync(scope, userId, ct);

        _audit.Record(scope, AccessAuditActions.UserRemoved, $"user: {userId}");

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "user removal");

        // Only invalidate the cache now that the removal and its audit entry are durably persisted.
        _cache.InvalidateUser(scope, userId);
    }

    public async Task RemoveScopeAsync(AccessScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid)
            return;

        await _roles.RemoveScopeAsync(scope, ct); // permissions and assignments follow (cascade)

        _audit.Record(scope, AccessAuditActions.ScopeRemoved);

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "scope removal");

        // Only invalidate the cache now that the removal and its audit entry are durably persisted.
        _cache.InvalidateScope(scope);
    }

    public async Task ProvisionScopeAsync(AccessScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid || _catalog.RoleTemplates.Count == 0)
            return;

        // Idempotent: if the scope already has roles, it was provisioned before. Scopes own their
        // roles after creation; template changes affect only newly provisioned scopes.
        var existingCount = await _roles.CountAsync(scope, ct);
        if (existingCount > 0)
            return;

        var templates = _catalog.RoleTemplates
            .Where(t => string.Equals(t.ScopeType, scope.Type, StringComparison.Ordinal))
            .ToList();

        if (templates.Count == 0)
            return;

        var now = _time.GetUtcNow().UtcDateTime;

        foreach (var template in templates)
        {
            var role = new AccessRole
            {
                Id = Guid.NewGuid(),
                ScopeType = scope.Type,
                ScopeId = scope.Id,
                Name = template.Name,
                NormalizedName = AccessValidation.NormalizeName(template.Name),
                Description = template.Description,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };

            foreach (var permission in template.Permissions)
                role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

            _roles.Add(role);
        }

        _audit.Record(scope, AccessAuditActions.ScopeProvisioned, $"roles: {templates.Count}");

        // Re-read after save to verify provisioning actually persisted (concurrent conflicts).
        var outcome = await _unitOfWork.SaveAsync(ct);
        if (outcome != SaveOutcome.Saved)
            throw new InvalidOperationException(
                $"Access scope provisioning was not persisted: the unit of work reported {outcome}.");

        // Verify: another concurrent provisioning may have added roles between our check and save.
        var afterCount = await _roles.CountAsync(scope, ct);
        if (afterCount == 0)
            throw new InvalidOperationException("Access scope provisioning persisted no roles.");
    }

    /// <summary>Throws unless the unit of work reports that the change was durably saved, so a failed save
    /// is never mistaken for a successful no-op.</summary>
    private static void EnsureSaved(SaveOutcome outcome, string operation)
    {
        if (outcome != SaveOutcome.Saved)
            throw new InvalidOperationException(
                $"Access {operation} was not persisted: the unit of work reported {outcome}.");
    }
}