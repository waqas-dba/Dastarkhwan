using System.Text.RegularExpressions;

namespace CoreKit.Tenant.Services;

public sealed class TenantSettingsService : ITenantSettingsService
{
    private static readonly Regex KeyPattern = new(
        "^[a-z0-9]+([._-][a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ITenantRepository _tenants;
    private readonly ITenantSettingRepository _settings;
    private readonly ITenantAuditRecorder _audit;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ITenantEventPublisher _events;
    private readonly TenantAccessGuard _guard;
    private readonly TenantOptions _options;
    private readonly TimeProvider _time;

    public TenantSettingsService(
        ITenantRepository tenants,
        ITenantSettingRepository settings,
        ITenantAuditRecorder audit,
        ITenantUnitOfWork unitOfWork,
        ITenantEventPublisher events,
        TenantAccessGuard guard,
        IOptions<TenantOptions> options,
        TimeProvider time)
    {
        _tenants = tenants;
        _settings = settings;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _events = events;
        _guard = guard;
        _options = options.Value;
        _time = time;
    }

    public async Task<TenantResult<IReadOnlyList<TenantSettingDto>>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        if (await _tenants.FindByIdAsync(tenantId, ct) is null)
            return TenantErrors.NotFound("Tenant");

        var settings = await _settings.ListAsync(tenantId, ct);

        return TenantResult.Success<IReadOnlyList<TenantSettingDto>>(
            settings.Select(s => TenantMapper.ToDto(s)).ToList());
    }

    public async Task<TenantResult<TenantSettingDto>> GetAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        var keyError = ValidateKey(key, out var normalizedKey);

        if (keyError is not null)
            return keyError;

        if (await _tenants.FindByIdAsync(tenantId, ct) is null)
            return TenantErrors.NotFound("Tenant");

        var setting = await _settings.FindAsync(tenantId, normalizedKey!, ct);

        return setting is null
            ? TenantErrors.NotFound("Setting")
            : TenantResult.Success(TenantMapper.ToDto(setting));
    }

    public async Task<TenantResult<TenantSettingDto>> SetAsync(
        Guid tenantId, string key, SetTenantSettingRequest request, CancellationToken ct = default)
    {
        var result = await SetManyAsync(
            tenantId, new SetTenantSettingsRequest { Values = new Dictionary<string, string> { [key] = request.Value } }, ct);

        return result.IsSuccess
            ? TenantResult.Success(result.Value[0])
            : TenantResult.Failure<TenantSettingDto>(result.Error!);
    }

    public async Task<TenantResult<IReadOnlyList<TenantSettingDto>>> SetManyAsync(
        Guid tenantId, SetTenantSettingsRequest request, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        if (request.Values is null || request.Values.Count == 0)
            return TenantErrors.Validation("At least one setting is required.");

        var wanted = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (rawKey, value) in request.Values)
        {
            var keyError = ValidateKey(rawKey, out var key);

            if (keyError is not null)
                return keyError;

            if (value is null)
                return TenantErrors.Validation($"The setting '{key}' needs a value.");

            if (value.Length > TenantValidation.MaxSettingValueLength)
                return TenantErrors.Validation(
                    $"The value of '{key}' is too long (max {TenantValidation.MaxSettingValueLength} characters).");

            if (!wanted.TryAdd(key!, value))
                return TenantErrors.Validation($"The setting '{key}' appears more than once.");
        }

        var tenant = await _tenants.FindByIdAsync(tenantId, ct);

        if (tenant is null)
            return TenantErrors.NotFound("Tenant");

        if (tenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        var existing = await _settings.FindManyForUpdateAsync(tenantId, wanted.Keys.ToList(), ct);
        var existingKeys = existing.Select(s => s.Key).ToHashSet();
        var newCount = wanted.Keys.Count(k => !existingKeys.Contains(k));

        if (await _settings.CountAsync(tenantId, ct) + newCount > _options.MaxSettingsPerTenant)
            return TenantErrors.Conflict($"A tenant can have at most {_options.MaxSettingsPerTenant} settings.");

        var now = Now();
        var saved = new List<TenantSetting>();

        foreach (var setting in existing)
        {
            setting.Value = wanted[setting.Key];
            setting.UpdatedAt = now;
            saved.Add(setting);
        }

        foreach (var (key, value) in wanted.Where(p => !existingKeys.Contains(p.Key)))
        {
            var setting = new TenantSetting { TenantId = tenantId, Key = key, Value = value, UpdatedAt = now };
            _settings.Add(setting);
            saved.Add(setting);
        }

        // Keys only: values may be secrets, and the audit trail is readable.
        _audit.Record(tenantId, TenantAuditActions.SettingsUpdated, "keys: " + string.Join(", ", wanted.Keys.Order()));

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        foreach (var key in wanted.Keys.Order())
            await _events.PublishAsync(new TenantSettingChangedEvent(tenantId, key, false, now), ct);

        return TenantResult.Success<IReadOnlyList<TenantSettingDto>>(
            saved.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => TenantMapper.ToDto(s)).ToList());
    }

    public async Task<TenantResult> RemoveAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        var keyError = ValidateKey(key, out var normalizedKey);

        if (keyError is not null)
            return keyError;

        var tenant = await _tenants.FindByIdAsync(tenantId, ct);

        if (tenant is null)
            return TenantErrors.NotFound("Tenant");

        if (tenant.Status == TenantStatus.Archived)
            return TenantErrors.ArchivedReadOnly;

        var setting = await _settings.FindForUpdateAsync(tenantId, normalizedKey!, ct);

        if (setting is null)
            return TenantErrors.NotFound("Setting");

        _settings.Remove(setting);
        _audit.Record(tenantId, TenantAuditActions.SettingRemoved, $"key: {normalizedKey}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        await _events.PublishAsync(new TenantSettingChangedEvent(tenantId, normalizedKey!, true, Now()), ct);

        return TenantResult.Success();
    }

    private static TenantError? ValidateKey(string? input, out string? key)
    {
        key = input?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(key))
            return TenantErrors.Validation("A setting key is required.");

        if (key.Length > TenantValidation.MaxSettingKeyLength)
            return TenantErrors.Validation(
                $"The setting key is too long (max {TenantValidation.MaxSettingKeyLength} characters).");

        return KeyPattern.IsMatch(key)
            ? null
            : TenantErrors.Validation(
                "A setting key may only contain lowercase letters, digits and the separators . _ -, for example 'general.timezone'.");
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}