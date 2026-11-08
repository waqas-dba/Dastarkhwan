using System.Collections.Generic;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Shared architecture boundary matrix consumed by both ModuleBoundaryTests and
/// ProjectReferenceBoundaryTests. It is the single source of truth for which module
/// may reference which, at both the compiled-assembly level and the declared
/// project-reference level.
/// </summary>
internal static class ModuleBoundaryMatrix
{
    /// <summary>
    /// Production module -> the CoreKit assemblies it may reference (compiled-assembly check).
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
    /// (declared project-reference check).
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
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> InternalsVisibleToAllowlist =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["CoreKit.Access"] = new[] { "CoreKit.Access.Tests" },
        };

    /// <summary>All production module names.</summary>
    public static readonly IReadOnlyList<string> ProductionModules =
        new[] { "CoreKit.SharedKernel", "CoreKit.Identity", "CoreKit.Access", "CoreKit.Tenant" };

    /// <summary>The composition root (SampleHost).</summary>
    public static readonly IReadOnlyList<string> CompositionRoots =
        new[] { "CoreKit.SampleHost" };

    /// <summary>All test project names.</summary>
    public static readonly IReadOnlyList<string> TestProjects =
        new[] { "CoreKit.Architecture.Tests", "CoreKit.Access.Tests", "CoreKit.Tenant.Tests" };
}
