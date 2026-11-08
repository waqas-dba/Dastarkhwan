using System.Collections.Generic;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Shared architecture boundary matrix consumed by both ModuleBoundaryTests and
/// ProjectReferenceBoundaryTests. It is the single source of truth for which module
/// may reference which, at both the compiled-assembly level and the declared
/// project-reference level.
///
/// Keys in CompiledAssemblyAllowlist and InternalsVisibleToAllowlist are evaluated
/// assembly names (e.g. "CoreKit.Access"). Keys in ProjectReferenceAllowlist are
/// solution-relative project paths (e.g. "CoreKit.Access/CoreKit.Access.csproj").
/// </summary>
internal static class ModuleBoundaryMatrix
{
    /// <summary>
    /// Production module -> the CoreKit assemblies it may reference (compiled-assembly check).
    /// Keys are evaluated assembly names.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CompiledAssemblyAllowlist =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["CoreKit.SharedKernel"] = Array.Empty<string>(),
            ["CoreKit.Identity"] = new[] { "CoreKit.SharedKernel" },
            ["CoreKit.Access"] = new[] { "CoreKit.SharedKernel" },
            ["CoreKit.Tenant"] = new[] { "CoreKit.SharedKernel", "CoreKit.Identity" },
        };

    /// <summary>
    /// Project file path (forward slashes, solution-relative) -> the project paths it may reference
    /// (declared/evaluated project-reference check).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ProjectReferenceAllowlist =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["CoreKit.Access/CoreKit.Access.csproj"] = new[] { "CoreKit.SharedKernel/CoreKit.SharedKernel.csproj" },
            ["CoreKit.Identity/CoreKit.Identity.csproj"] = new[] { "CoreKit.SharedKernel/CoreKit.SharedKernel.csproj" },
            ["CoreKit.Tenant/CoreKit.Tenant.csproj"] = new[]
            {
                "CoreKit.Identity/CoreKit.Identity.csproj",
                "CoreKit.SharedKernel/CoreKit.SharedKernel.csproj",
            },
            ["CoreKit.SharedKernel/CoreKit.SharedKernel.csproj"] = Array.Empty<string>(),
            ["CoreKit.SampleHost/CoreKit.SampleHost.csproj"] = new[]
            {
                "CoreKit.Access/CoreKit.Access.csproj",
                "CoreKit.Identity/CoreKit.Identity.csproj",
                "CoreKit.Tenant/CoreKit.Tenant.csproj",
            },
        };

    /// <summary>
    /// Assembly name -> friend assembly names allowed to see its internals.
    /// Keys are evaluated assembly names; values are friend assembly names.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> InternalsVisibleToAllowlist =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["CoreKit.Access"] = new[] { "CoreKit.Access.Tests" },
        };

    /// <summary>All production module names (evaluated assembly names).</summary>
    public static readonly IReadOnlyList<string> ProductionModules =
        new[] { "CoreKit.SharedKernel", "CoreKit.Identity", "CoreKit.Access", "CoreKit.Tenant" };

    /// <summary>All production module project paths, solution-relative forward-slash.</summary>
    public static readonly IReadOnlyList<string> ProductionModuleProjectPaths = new[]
    {
        "CoreKit.SharedKernel/CoreKit.SharedKernel.csproj",
        "CoreKit.Identity/CoreKit.Identity.csproj",
        "CoreKit.Access/CoreKit.Access.csproj",
        "CoreKit.Tenant/CoreKit.Tenant.csproj",
    };

    /// <summary>Composition root project paths, solution-relative forward-slash.</summary>
    public static readonly IReadOnlyList<string> CompositionRoots = new[]
    {
        "CoreKit.SampleHost/CoreKit.SampleHost.csproj",
    };

    /// <summary>All test project names (evaluated assembly names or project paths).</summary>
    public static readonly IReadOnlyList<string> TestProjects = new[]
    {
        "CoreKit.Architecture.Tests",
        "CoreKit.Access.Tests",
        "CoreKit.Tenant.Tests",
    };

    /// <summary>
    /// D-015 temporary exception: Tenant -> Identity is the only documented temporary
    /// production-module exception. It must be removed during S4.3 (Tenant decoupling).
    /// This sentinel test confirms the exception remains until S4.3 is implemented.
    /// Do not implement S4.3 here.
    /// </summary>
    public const string D015_TenantIdentity_TemporaryException =
        "Tenant -> Identity direct dependency is temporary under D-015; remove during S4.3";

    /// <summary>
    /// Asserts that the D-015 Tenant -> Identity exception is still present in both
    /// allowlists. This is a sentinel: if S4.3 removes the exception, this test will
    /// fail and the matrix should be updated to remove Tenant -> Identity.
    /// </summary>
    public static void AssertD015TenantIdentityExceptionPresent()
    {
        // Compiled-assembly allowlist: Tenant references Identity
        var tenantAllowed = CompiledAssemblyAllowlist["CoreKit.Tenant"];
        if (!tenantAllowed.Contains("CoreKit.Identity"))
            throw new InvalidOperationException(
                "D-015 sentinel failed: CoreKit.Tenant no longer references CoreKit.Identity " +
                "in CompiledAssemblyAllowlist. If S4.3 has been implemented, remove this sentinel. " +
                "Otherwise, restore the exception.");

        // Project-reference allowlist: Tenant.csproj references Identity.csproj
        var tenantProjectAllowed = ProjectReferenceAllowlist["CoreKit.Tenant/CoreKit.Tenant.csproj"];
        if (!tenantProjectAllowed.Contains("CoreKit.Identity/CoreKit.Identity.csproj"))
            throw new InvalidOperationException(
                "D-015 sentinel failed: CoreKit.Tenant.csproj no longer references " +
                "CoreKit.Identity.csproj in ProjectReferenceAllowlist. If S4.3 has been " +
                "implemented, remove this sentinel. Otherwise, restore the exception.");
    }
}
