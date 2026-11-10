using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            ["CoreKit.Access"] = Array.Empty<string>(),
            ["CoreKit.Tenant"] = new[] { "CoreKit.SharedKernel" },
        };

    /// <summary>
    /// Project file path (forward slashes, solution-relative) -> the project paths it may reference
    /// (declared/evaluated project-reference check).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ProjectReferenceAllowlist =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["CoreKit.Access/CoreKit.Access.csproj"] = Array.Empty<string>(),
            ["CoreKit.Identity/CoreKit.Identity.csproj"] = new[] { "CoreKit.SharedKernel/CoreKit.SharedKernel.csproj" },
            ["CoreKit.Tenant/CoreKit.Tenant.csproj"] = new[]
            {
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
    /// <remarks>Sourced from SolutionInventory (canonical classification) to prevent drift.</remarks>
    public static readonly IReadOnlyList<string> ProductionModuleProjectPaths = SolutionInventory.ProductionModules;

    /// <summary>Composition root project paths, solution-relative forward-slash.</summary>
    /// <remarks>Sourced from SolutionInventory (canonical classification) to prevent drift.</remarks>
    public static readonly IReadOnlyList<string> CompositionRoots = SolutionInventory.CompositionRoots;

    /// <summary>All test project names (evaluated assembly names), sourced from
    /// SolutionInventory.ExpectedTestAssemblyNames to prevent drift.</summary>
    public static readonly IReadOnlyList<string> TestProjects =
        SolutionInventory.ExpectedTestAssemblyNames.Values.ToList();

}
