using System.Reflection;
using Xunit;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Keeps the modules independent. A module may reference the shared kernel, but another module
/// only where this list says so. Every row you delete from the exceptions makes the design stricter,
/// and a new, unlisted reference fails the build.
/// </summary>
public sealed class ModuleBoundaryTests
{
    // module -> the CoreKit assemblies it may reference
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["CoreKit.SharedKernel"] = Array.Empty<string>(),
        ["CoreKit.IAM"] = new[] { "CoreKit.SharedKernel" },

        // Temporary: Tenant still uses IAM's token hook, permission seeds and endpoint permission check.
        // They move behind the shared kernel when Access provides the permission policies.
        ["CoreKit.Tenant"] = new[] { "CoreKit.SharedKernel", "CoreKit.IAM" }
    };

    public static IEnumerable<object[]> Modules() => Allowed.Keys.Select(m => new object[] { m });

    private static List<string> CoreKitReferencesOf(string module)
        => Assembly.Load(module).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("CoreKit.", StringComparison.Ordinal))
            .Distinct()
            .ToList();

    [Theory]
    [MemberData(nameof(Modules))]
    public void A_module_references_only_the_modules_it_is_allowed_to(string module)
    {
        var illegal = CoreKitReferencesOf(module).Except(Allowed[module]).ToList();

        Assert.True(
            illegal.Count == 0,
            $"{module} must not reference: {string.Join(", ", illegal)}. " +
            "Use a contract from CoreKit.SharedKernel instead of another module.");
    }

    [Fact]
    public void The_shared_kernel_has_no_third_party_dependencies()
    {
        var thirdParty = Assembly.Load("CoreKit.SharedKernel").GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                        n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                        n.StartsWith("Npgsql", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            thirdParty.Count == 0,
            $"The shared kernel must stay free of frameworks. Found: {string.Join(", ", thirdParty)}.");
    }

    [Fact]
    public void The_shared_kernel_contains_only_contracts_and_events()
    {
        var types = Assembly.Load("CoreKit.SharedKernel").GetExportedTypes();

        // No entities and no DbContexts: those belong to a module.
        Assert.DoesNotContain(types, t => t.Name.EndsWith("DbContext", StringComparison.Ordinal));
        Assert.DoesNotContain(types, t => t.Name.EndsWith("Entity", StringComparison.Ordinal));
    }
}