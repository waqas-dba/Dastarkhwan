using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Discovers and classifies every project in Dastarkhwan.slnx into production modules,
/// composition roots, and test projects. Validates classification consistency with
/// ModuleBoundaryMatrix.
///
/// Solution directory discovery order:
///   1. COREKIT_SOLUTION_DIR environment override
///   2. Upward search from AppContext.BaseDirectory
///   3. Upward search from Directory.GetCurrentDirectory()
/// </summary>
internal static class SolutionInventory
{
    private const string SolutionFileName = "Dastarkhwan.slnx";

    /// <summary>Production module project paths, solution-relative, forward-slash.</summary>
    public static readonly IReadOnlyList<string> ProductionModules = new[]
    {
        "CoreKit.SharedKernel/CoreKit.SharedKernel.csproj",
        "CoreKit.Identity/CoreKit.Identity.csproj",
        "CoreKit.Access/CoreKit.Access.csproj",
        "CoreKit.Tenant/CoreKit.Tenant.csproj",
    };

    /// <summary>Composition root project paths.</summary>
    public static readonly IReadOnlyList<string> CompositionRoots = new[]
    {
        "CoreKit.SampleHost/CoreKit.SampleHost.csproj",
    };

    /// <summary>Test project paths.</summary>
    public static readonly IReadOnlyList<string> TestProjects = new[]
    {
        "CoreKit.Architecture.Tests/CoreKit.Architecture.Tests.csproj",
        "CoreKit.Access.Tests/CoreKit.Access.Tests.csproj",
        "CoreKit.Tenant.Tests/CoreKit.Tenant.Tests.csproj",
    };

    /// <summary>
    /// Expected evaluated assembly name for each test project, keyed by solution-relative
    /// project path. Every test project must have exactly one entry.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ExpectedTestAssemblyNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CoreKit.Architecture.Tests/CoreKit.Architecture.Tests.csproj"] = "CoreKit.Architecture.Tests",
            ["CoreKit.Access.Tests/CoreKit.Access.Tests.csproj"] = "CoreKit.Access.Tests",
            ["CoreKit.Tenant.Tests/CoreKit.Tenant.Tests.csproj"] = "CoreKit.Tenant.Tests",
        };

    /// <summary>
    /// Expected evaluated assembly name for each production project, keyed by solution-relative
    /// project path. Every production project must have exactly one entry.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ExpectedAssemblyNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CoreKit.SharedKernel/CoreKit.SharedKernel.csproj"] = "CoreKit.SharedKernel",
            ["CoreKit.Identity/CoreKit.Identity.csproj"] = "CoreKit.Identity",
            ["CoreKit.Access/CoreKit.Access.csproj"] = "CoreKit.Access",
            ["CoreKit.Tenant/CoreKit.Tenant.csproj"] = "CoreKit.Tenant",
        };

    /// <summary>Duplicate normalized project paths found in Dastarkhwan.slnx.</summary>
    public static readonly IReadOnlyList<string> DuplicateSolutionPaths;

    /// <summary>Solution directory containing Dastarkhwan.slnx.</summary>
    public static readonly string SolutionDir;

    /// <summary>Absolute path to Dastarkhwan.slnx.</summary>
    public static readonly string SolutionPath;

    /// <summary>All project paths found in Dastarkhwan.slnx, solution-relative forward-slash.</summary>
    public static readonly IReadOnlyList<string> AllSolutionProjects;

    /// <summary>Union of all classified categories, de-duplicated.</summary>
    public static readonly IReadOnlyList<string> AllClassifiedProjects;

    /// <summary>Projects in slnx that belong to no category.</summary>
    public static readonly IReadOnlyList<string> UnclassifiedProjects;

    /// <summary>Projects that appear in more than one category.</summary>
    public static readonly IReadOnlyList<string> MultiplyClassifiedProjects;

    /// <summary>Matrix category entries that correspond to no slnx project.</summary>
    public static readonly IReadOnlyList<string> StaleMatrixEntries;

    /// <summary>Slnx projects that belong to no matrix category.</summary>
    public static readonly IReadOnlyList<string> MissingMatrixEntries;

    static SolutionInventory()
    {
        SolutionDir = FindSolutionDirectory(out var attemptedPaths);
        SolutionPath = Path.Combine(SolutionDir, SolutionFileName);
        AllSolutionProjects = GetAllSolutionProjects();

        // Detect duplicate normalized project paths before constructing classification dictionaries.
        var normalizedPaths = AllSolutionProjects
            .Select(NormalizeSolutionPath)
            .ToList();
        DuplicateSolutionPaths = normalizedPaths
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        var projectCategories = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in AllSolutionProjects)
            projectCategories[p] = new List<string>();

        foreach (var p in ProductionModules)
            if (projectCategories.ContainsKey(p))
                projectCategories[p].Add(nameof(ProductionModules));

        foreach (var p in CompositionRoots)
            if (projectCategories.ContainsKey(p))
                projectCategories[p].Add(nameof(CompositionRoots));

        foreach (var p in TestProjects)
            if (projectCategories.ContainsKey(p))
                projectCategories[p].Add(nameof(TestProjects));

        UnclassifiedProjects = AllSolutionProjects
            .Where(p => projectCategories[p].Count == 0)
            .ToList();

        MultiplyClassifiedProjects = AllSolutionProjects
            .Where(p => projectCategories[p].Count > 1)
            .ToList();

        AllClassifiedProjects = ProductionModules
            .Concat(CompositionRoots)
            .Concat(TestProjects)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allMatrixKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ProductionModules) allMatrixKeys.Add(p);
        foreach (var p in CompositionRoots) allMatrixKeys.Add(p);
        foreach (var p in TestProjects) allMatrixKeys.Add(p);

        StaleMatrixEntries = allMatrixKeys
            .Where(k => !AllSolutionProjects.Contains(k, StringComparer.OrdinalIgnoreCase))
            .ToList();

        MissingMatrixEntries = AllSolutionProjects
            .Where(p => !allMatrixKeys.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Validates inventory consistency. Throws InvalidOperationException with
    /// actionable diagnostics if any classification problem is found.
    /// </summary>
    public static void ValidateConsistency()
    {
        var errors = new List<string>();

        if (UnclassifiedProjects.Count > 0)
            errors.Add(
                $"Unclassified projects in Dastarkhwan.slnx (belong to no matrix category): " +
                $"{string.Join(", ", UnclassifiedProjects)}");

        if (MultiplyClassifiedProjects.Count > 0)
            errors.Add(
                $"Multiply classified projects (appear in multiple matrix categories): " +
                $"{string.Join(", ", MultiplyClassifiedProjects)}");

        if (StaleMatrixEntries.Count > 0)
            errors.Add(
                $"Stale matrix entries (no corresponding project in Dastarkhwan.slnx): " +
                $"{string.Join(", ", StaleMatrixEntries)}");

        if (MissingMatrixEntries.Count > 0)
            errors.Add(
                $"Missing matrix entries (project in Dastarkhwan.slnx but not in any matrix category): " +
                $"{string.Join(", ", MissingMatrixEntries)}");

        if (DuplicateSolutionPaths.Count > 0)
            errors.Add(
                $"Duplicate normalized project paths in Dastarkhwan.slnx: " +
                $"{string.Join(", ", DuplicateSolutionPaths)}");

        // Validate test assembly name mappings.
        foreach (var testProject in TestProjects)
        {
            if (!ExpectedTestAssemblyNames.ContainsKey(testProject))
            {
                errors.Add(
                    $"Test project '{testProject}' has no expected assembly name mapping in " +
                    $"SolutionInventory.ExpectedTestAssemblyNames.");
            }
        }

        foreach (var testProject in ExpectedTestAssemblyNames.Keys)
        {
            if (!TestProjects.Contains(testProject, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"ExpectedTestAssemblyNames contains '{testProject}' which is not a " +
                    $"test project in SolutionInventory.TestProjects.");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Solution inventory is inconsistent:\n" + string.Join(Environment.NewLine, errors));
    }

    private static List<string> GetAllSolutionProjects()
    {
        var doc = XDocument.Load(SolutionPath);
        var result = new List<string>();
        foreach (var p in doc.Root!.Descendants("Project"))
        {
            var path = p.Attribute("Path")?.Value;
            if (path is not null)
                result.Add(path.Replace('\\', '/'));
        }
        return result;
    }

    /// <summary>
    /// Normalizes a solution-relative project path for duplicate detection:
    /// forward slashes, no trailing slash.
    /// </summary>
    private static string NormalizeSolutionPath(string path)
        => path.Replace('\\', '/').TrimEnd('/');

    private static string FindSolutionDirectory(out List<string> attemptedPaths)
    {
        attemptedPaths = new List<string>();

        // 1. Environment override
        var envOverride = Environment.GetEnvironmentVariable("COREKIT_SOLUTION_DIR");
        if (!string.IsNullOrEmpty(envOverride))
        {
            attemptedPaths.Add($"COREKIT_SOLUTION_DIR={envOverride}");
            var overrideDir = Path.GetFullPath(envOverride);
            if (File.Exists(Path.Combine(overrideDir, SolutionFileName)))
                return overrideDir;
            throw new InvalidOperationException(
                $"COREKIT_SOLUTION_DIR points to '{envOverride}' but " +
                $"'{Path.Combine(overrideDir, SolutionFileName)}' does not exist.");
        }

        // 2. Upward search from AppContext.BaseDirectory
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            attemptedPaths.Add(dir.FullName);
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                return dir.FullName;
            dir = dir.Parent;
        }

        // 3. Upward search from Directory.GetCurrentDirectory()
        var cwd = Directory.GetCurrentDirectory();
        dir = new DirectoryInfo(cwd);
        while (dir != null)
        {
            attemptedPaths.Add(dir.FullName);
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Dastarkhwan.slnx not found. Attempted paths: {string.Join(", ", attemptedPaths)}. " +
            "Set COREKIT_SOLUTION_DIR or run from within the repository directory tree.");
    }
}
