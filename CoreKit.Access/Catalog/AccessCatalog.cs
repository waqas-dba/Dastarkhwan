using System.Text.RegularExpressions;

namespace CoreKit.Access.Catalog;

public sealed record AccessPermissionDefinition(string Name, string Description);

/// <summary>A role every new scope starts with. After that the scope owns its copy and can change it.</summary>
public sealed record AccessRoleTemplate(string Name, string? Description, IReadOnlyList<string> Permissions);

/// <summary>Where an application (or any module) declares the permissions it uses.</summary>
public sealed class AccessCatalogBuilder
{
    internal static readonly Regex PermissionPattern = new(
        "^[a-z0-9]+([._-][a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, AccessPermissionDefinition> _permissions = new(StringComparer.Ordinal);
    private readonly List<AccessRoleTemplate> _templates = new();

    internal IReadOnlyCollection<AccessPermissionDefinition> Permissions => _permissions.Values;

    internal IReadOnlyList<AccessRoleTemplate> Templates => _templates;

    /// <summary>Declares a permission. Names are lower-case and dotted, for example "sales.refund".</summary>
    public AccessCatalogBuilder Add(string name, string description)
    {
        var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0 || normalized.Length > 100 || !PermissionPattern.IsMatch(normalized))
            throw new ArgumentException(
                $"'{name}' is not a valid permission name. Use lower-case letters, digits and . _ - (for example 'sales.refund').",
                nameof(name));

        _permissions.TryAdd(normalized, new AccessPermissionDefinition(normalized, description ?? string.Empty));
        return this;
    }

    /// <summary>Declares a role that every new scope receives. Its permissions must be declared too.</summary>
    public AccessCatalogBuilder AddRoleTemplate(string name, string? description, params string[] permissions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A role template needs a name.", nameof(name));

        _templates.Add(new AccessRoleTemplate(
            name.Trim(), description, permissions.Select(p => p.Trim().ToLowerInvariant()).Distinct().ToList()));

        return this;
    }
}

internal sealed record AccessCatalogContribution(Action<AccessCatalogBuilder> Configure);

/// <summary>Every permission and role template declared by the application and its modules.</summary>
public sealed class AccessCatalog
{
    private readonly HashSet<string> _names;

    private AccessCatalog(IReadOnlyList<AccessPermissionDefinition> permissions, IReadOnlyList<AccessRoleTemplate> templates)
    {
        Permissions = permissions;
        RoleTemplates = templates;
        _names = permissions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<AccessPermissionDefinition> Permissions { get; }

    public IReadOnlyList<AccessRoleTemplate> RoleTemplates { get; }

    public IReadOnlySet<string> Names => _names;

    public bool Contains(string permission) => _names.Contains(permission);

    internal static AccessCatalog Build(IEnumerable<AccessCatalogContribution> contributions)
    {
        var builder = new AccessCatalogBuilder();

        foreach (var permission in AccessPermissionNames.All)
            builder.Add(permission.Name, permission.Description);

        foreach (var contribution in contributions)
            contribution.Configure(builder);

        var names = builder.Permissions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var template in builder.Templates)
        {
            var unknown = template.Permissions.FirstOrDefault(p => !names.Contains(p));

            if (unknown is not null)
                throw new InvalidOperationException(
                    $"The role template '{template.Name}' uses the permission '{unknown}', which was never declared.");
        }

        return new AccessCatalog(
            builder.Permissions.OrderBy(p => p.Name, StringComparer.Ordinal).ToList(),
            builder.Templates.ToList());
    }
}