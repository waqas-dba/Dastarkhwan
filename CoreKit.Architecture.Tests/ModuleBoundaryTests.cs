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
        ["CoreKit.Identity"] = new[] { "CoreKit.SharedKernel" },
        ["CoreKit.Access"] = new[] { "CoreKit.SharedKernel" },

        // Temporary exception (decision D-015, roadmap S4.3): Tenant still consumes Identity's
        // token hook, permission seeds, endpoint permission check and global usings. These move
        // behind CoreKit.SharedKernel contracts; removing "CoreKit.Identity" from this row is the
        // completion evidence for S4.3, after which any new direct reference fails the test again.
        // Do not extend this list for other modules.
        ["CoreKit.Tenant"] = new[] { "CoreKit.SharedKernel", "CoreKit.Identity" }
    };

    public static IEnumerable<object[]> Modules() => Allowed.Keys.Select(m => new object[] { m });

    private static List<string> CoreKitReferencesOf(string module)
        => Load(module).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("CoreKit.", StringComparison.Ordinal))
            .Distinct()
            .ToList();

    // A module missing from the test output would otherwise fail with a bare FileNotFoundException
    // and hide the cause, which is a missing ProjectReference in CoreKit.Architecture.Tests.csproj.
    private static Assembly Load(string module)
    {
        try
        {
            return Assembly.Load(module);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"{module} was not copied to the test output. " +
                "Add a ProjectReference for it to CoreKit.Architecture.Tests.csproj.");
        }
    }

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
        var thirdParty = Load("CoreKit.SharedKernel").GetReferencedAssemblies()
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
        var types = Load("CoreKit.SharedKernel").GetExportedTypes();

        // No entities and no DbContexts: those belong to a module.
        Assert.DoesNotContain(types, t => t.Name.EndsWith("DbContext", StringComparison.Ordinal));
        Assert.DoesNotContain(types, t => t.Name.EndsWith("Entity", StringComparison.Ordinal));
    }
}