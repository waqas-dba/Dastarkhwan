using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Keeps the modules independent. A module may reference another module only where the allowlist
/// says so. Every row you delete from the exceptions makes the design stricter, and a new,
/// unlisted reference fails the build. The compiled-assembly allowlist is shared with
/// ProjectReferenceBoundaryTests via ModuleBoundaryMatrix.
/// </summary>
public sealed class ModuleBoundaryTests
{
    public static IEnumerable<object[]> Modules() =>
        ModuleBoundaryMatrix.CompiledAssemblyAllowlist.Keys.Select(m => new object[] { m });

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
        var allowed = ModuleBoundaryMatrix.CompiledAssemblyAllowlist[module];
        var illegal = CoreKitReferencesOf(module).Except(allowed).ToList();

        Assert.True(
            illegal.Count == 0,
            $"{module} must not reference: {string.Join(", ", illegal)}. " +
            "Keep a module's contracts inside the module that owns them.");
    }
}
