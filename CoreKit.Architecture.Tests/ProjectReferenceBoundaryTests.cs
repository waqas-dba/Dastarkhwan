using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Validates the effective MSBuild project-reference graph against ModuleBoundaryMatrix.
///
/// Uses dotnet msbuild -getProperty/-getItem through a subprocess to evaluate each
/// project in Debug and Release, then enforces the boundary matrix both directions.
/// Test-project dependency graphs are intentionally left unconstrained in this task.
/// </summary>
public sealed class ProjectReferenceBoundaryTests
{
    private static readonly MsBuildProjectEvaluator Evaluator = new(SolutionInventory.SolutionDir);

    // Architecture-test implementation files excluded from alternative-mechanism scanning.
    private static readonly HashSet<string> ExcludedImplementationFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SolutionInventory.cs",
        "MsBuildProjectEvaluator.cs",
        "ProjectReferenceBoundaryTests.cs",
        "ModuleBoundaryMatrix.cs",
        "ModuleBoundaryTests.cs",
    };

    [Fact]
    public void Solution_inventory_is_consistent()
    {
        SolutionInventory.ValidateConsistency();
    }

    [Fact]
    public void Every_solution_project_belongs_to_exactly_one_category()
    {
        Assert.True(SolutionInventory.UnclassifiedProjects.Count == 0,
            $"Unclassified projects: {string.Join(", ", SolutionInventory.UnclassifiedProjects)}");
        Assert.True(SolutionInventory.MultiplyClassifiedProjects.Count == 0,
            $"Multiply classified projects: {string.Join(", ", SolutionInventory.MultiplyClassifiedProjects)}");
    }

    [Fact]
    public void Duplicate_normalized_solution_paths_cause_actionable_failure()
    {
        Assert.True(SolutionInventory.DuplicateSolutionPaths.Count == 0,
            $"Duplicate normalized project paths in Dastarkhwan.slnx: " +
            $"{string.Join(", ", SolutionInventory.DuplicateSolutionPaths)}");
    }

    [Fact]
    public void Every_production_project_has_expected_assembly_mapping()
    {
        // Every production project must have exactly one expected assembly name.
        foreach (var projectPath in SolutionInventory.ProductionModules)
        {
            Assert.True(
                SolutionInventory.ExpectedAssemblyNames.ContainsKey(projectPath),
                $"Production project '{projectPath}' has no expected assembly name mapping in " +
                $"SolutionInventory.ExpectedAssemblyNames.");
        }

        // Every expected assembly mapping must correspond to a production project.
        foreach (var projectPath in SolutionInventory.ExpectedAssemblyNames.Keys)
        {
            Assert.True(
                SolutionInventory.ProductionModules.Contains(projectPath, StringComparer.OrdinalIgnoreCase),
                $"ExpectedAssemblyNames contains '{projectPath}' which is not a production module project path.");
        }
    }

    [Theory]
    [MemberData(nameof(ProductionModuleProjectPathsOnly))]
    public void Evaluated_assembly_name_matches_expected_mapping(string projectPath)
    {
        var expectedName = SolutionInventory.ExpectedAssemblyNames[projectPath];

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var result = Evaluator.Evaluate(projectPath, configuration);
            Assert.True(
                string.Equals(result.AssemblyName, expectedName, StringComparison.OrdinalIgnoreCase),
                $"Project '{projectPath}' ({configuration}) evaluates to assembly '{result.AssemblyName}', " +
                $"but expected '{expectedName}'.");
        }
    }

    [Fact]
    public void Classification_lists_do_not_drift_between_inventory_and_matrix()
    {
        // Production module project paths must match exactly.
        var inventoryProduction = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in SolutionInventory.ProductionModules) inventoryProduction.Add(p);
        var matrixProduction = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ModuleBoundaryMatrix.ProductionModuleProjectPaths) matrixProduction.Add(p);
        Assert.True(inventoryProduction.SetEquals(matrixProduction),
            $"Production module project paths drift between SolutionInventory and ModuleBoundaryMatrix." +
            $" Inventory: {string.Join(", ", inventoryProduction)}. Matrix: {string.Join(", ", matrixProduction)}.");

        // Composition root project paths must match exactly.
        var inventoryRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in SolutionInventory.CompositionRoots) inventoryRoots.Add(p);
        var matrixRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ModuleBoundaryMatrix.CompositionRoots) matrixRoots.Add(p);
        Assert.True(inventoryRoots.SetEquals(matrixRoots),
            $"Composition root project paths drift between SolutionInventory and ModuleBoundaryMatrix.");
    }

    [Fact]
    public void Project_reference_allowlist_keys_match_production_and_composition_root_projects()
    {
        var expectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ModuleBoundaryMatrix.ProductionModuleProjectPaths) expectedKeys.Add(p);
        foreach (var p in ModuleBoundaryMatrix.CompositionRoots) expectedKeys.Add(p);

        var actualKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in ModuleBoundaryMatrix.ProjectReferenceAllowlist.Keys) actualKeys.Add(k);

        Assert.True(expectedKeys.SetEquals(actualKeys),
            $"ProjectReferenceAllowlist keys do not match production + composition root projects." +
            $" Expected: {string.Join(", ", expectedKeys)}. Actual: {string.Join(", ", actualKeys)}.");
    }

    [Fact]
    public void Every_allowed_reference_target_exists_in_solution()
    {
        var allSolutionProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in SolutionInventory.AllSolutionProjects) allSolutionProjects.Add(p);

        foreach (var kvp in ModuleBoundaryMatrix.ProjectReferenceAllowlist)
        {
            Assert.True(allSolutionProjects.Contains(kvp.Key),
                $"ProjectReferenceAllowlist key '{kvp.Key}' is not a project in Dastarkhwan.slnx.");

            foreach (var target in kvp.Value)
            {
                Assert.True(allSolutionProjects.Contains(target),
                    $"ProjectReferenceAllowlist target '{target}' (referenced by '{kvp.Key}') " +
                    "is not a project in Dastarkhwan.slnx.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ProductionAndCompositionRootProjects))]
    public void Every_evaluated_reference_is_allowed(string projectPath)
    {
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var result = Evaluator.Evaluate(projectPath, configuration);
            var allowedRefs = ModuleBoundaryMatrix.ProjectReferenceAllowlist[projectPath];
            var allowedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in allowedRefs) allowedSet.Add(a);

            foreach (var reference in result.ProjectReferences)
            {
                Assert.True(allowedSet.Contains(reference),
                    $"Project '{projectPath}' ({configuration}) declares ProjectReference to '{reference}' " +
                    $"which is not allowed. Allowed references: {string.Join(", ", allowedRefs)}.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ProductionAndCompositionRootProjects))]
    public void Production_modules_and_composition_roots_cannot_reference_test_projects(string projectPath)
    {
        var testProjectSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tp in ModuleBoundaryMatrix.TestProjects) testProjectSet.Add(tp);

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var result = Evaluator.Evaluate(projectPath, configuration);

            foreach (var reference in result.ProjectReferences)
            {
                var referenceProjectName = Path.GetFileNameWithoutExtension(reference);
                Assert.False(testProjectSet.Contains(referenceProjectName),
                    $"Project '{projectPath}' ({configuration}) references test project '{reference}'. " +
                    "Production modules and composition roots must not reference test projects.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ProductionModuleProjectPathsOnly))]
    public void Evaluated_assembly_names_match_compiled_assembly_allowlist(string projectPath)
    {
        var evaluatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var result = Evaluator.Evaluate(projectPath, configuration);
            evaluatedNames.Add(result.AssemblyName);
        }

        var allowlistKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in ModuleBoundaryMatrix.CompiledAssemblyAllowlist.Keys) allowlistKeys.Add(k);

        Assert.True(allowlistKeys.Contains(evaluatedNames.First()),
            $"Evaluated assembly name '{evaluatedNames.First()}' for project '{projectPath}' " +
            $"is not in CompiledAssemblyAllowlist keys: {string.Join(", ", allowlistKeys)}.");

        // Both Debug and Release must evaluate to the same assembly name.
        Assert.True(evaluatedNames.Count == 1,
            $"Project '{projectPath}' evaluates to different assembly names across configurations: " +
            $"{string.Join(", ", evaluatedNames)}.");
    }

    [Theory]
    [MemberData(nameof(ProductionModuleProjectPathsOnly))]
    public void InternalsVisibleTo_declarations_match_allowlist(string projectPath)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        var allowlist = ModuleBoundaryMatrix.InternalsVisibleToAllowlist;

        // An absent allowlist key means no IVT declarations are expected.
        var allowedFriends = allowlist.TryGetValue(assemblyName, out var friends)
            ? friends
            : Array.Empty<string>();

        var allowedFriendSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in allowedFriends) allowedFriendSet.Add(f);

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var result = Evaluator.Evaluate(projectPath, configuration);
            foreach (var friend in result.InternalsVisibleTo)
            {
                Assert.True(allowedFriendSet.Contains(friend),
                    $"Project '{projectPath}' ({configuration}) declares InternalsVisibleTo to '{friend}' " +
                    $"which is not allowed. Allowed friends: {string.Join(", ", allowedFriendSet)}.");
            }
        }
    }

    [Fact]
    public void InternalsVisibleTo_allowlist_is_complete_both_directions()
    {
        // Forward: every allowlist entry must be used by at least one project.
        foreach (var kvp in ModuleBoundaryMatrix.InternalsVisibleToAllowlist)
        {
            var assemblyName = kvp.Key;
            var friends = kvp.Value;

            // Find the project that produces this assembly.
            var producingProject = ModuleBoundaryMatrix.ProductionModuleProjectPaths
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == assemblyName);

            if (producingProject == null)
            {
                // Not a production module - skip (IVT allowlist should only contain production modules)
                continue;
            }

            foreach (var configuration in new[] { "Debug", "Release" })
            {
                var result = Evaluator.Evaluate(producingProject, configuration);
                foreach (var friend in friends)
                {
                    Assert.True(result.InternalsVisibleTo.Contains(friend, StringComparer.OrdinalIgnoreCase),
                        $"InternalsVisibleToAllowlist friend '{friend}' for assembly '{assemblyName}' " +
                        $"is not declared by project '{producingProject}' ({configuration}). " +
                        $"Declared: {string.Join(", ", result.InternalsVisibleTo)}.");
                }
            }
        }

        // Reverse: every declared IVT friend must be in the allowlist.
        // Only production modules with an allowlist entry are expected to declare IVT.
        foreach (var projectPath in ModuleBoundaryMatrix.ProductionModuleProjectPaths)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
            var allowlist = ModuleBoundaryMatrix.InternalsVisibleToAllowlist;

            if (!allowlist.TryGetValue(assemblyName, out var allowedFriends))
                continue;

            var allowedFriendSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in allowedFriends) allowedFriendSet.Add(f);

            foreach (var configuration in new[] { "Debug", "Release" })
            {
                var result = Evaluator.Evaluate(projectPath, configuration);
                foreach (var friend in result.InternalsVisibleTo)
                {
                    Assert.True(allowedFriendSet.Contains(friend),
                        $"Project '{projectPath}' ({configuration}) declares InternalsVisibleTo to '{friend}' " +
                        $"which is not in the allowlist. Allowed: {string.Join(", ", allowedFriendSet)}.");
                }
            }
        }
    }

    [Fact]
    public void Only_production_modules_declare_internals_visible_to()
    {
        // Test projects should not declare InternalsVisibleTo (they consume it, not produce it).
        foreach (var testProject in SolutionInventory.TestProjects)
        {
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                var result = Evaluator.Evaluate(testProject, configuration);
                Assert.True(result.InternalsVisibleTo.Count == 0,
                    $"Test project '{testProject}' ({configuration}) declares InternalsVisibleTo: " +
                    $"{string.Join(", ", result.InternalsVisibleTo)}. Only production modules may declare IVT.");
            }
        }
    }

    [Fact]
    public void IVT_friends_must_be_test_projects()
    {
        var testProjectSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tp in ModuleBoundaryMatrix.TestProjects) testProjectSet.Add(tp);

        foreach (var projectPath in ModuleBoundaryMatrix.ProductionModuleProjectPaths)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
            var allowlist = ModuleBoundaryMatrix.InternalsVisibleToAllowlist;
            if (!allowlist.TryGetValue(assemblyName, out var allowedFriends))
                continue;

            foreach (var friend in allowedFriends)
            {
                Assert.True(testProjectSet.Contains(friend),
                    $"InternalsVisibleTo friend '{friend}' (declared by '{assemblyName}') is not a test project. " +
                    $"Friends must be test projects. Test projects: {string.Join(", ", testProjectSet)}.");
            }
        }
    }

    [Fact]
    public void No_unsupported_internals_visible_to_mechanisms()
    {
        var violations = ScanForAlternativeIvtMechanisms(SolutionInventory.SolutionDir);
        Assert.True(violations.Count == 0,
            $"Unsupported InternalsVisibleTo mechanisms detected:{Environment.NewLine}" +
            $"{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void D015_tenant_identity_exception_remains_until_S4_3()
    {
        // Sentinel: the D-015 Tenant -> Identity exception must remain in both allowlists.
        // If S4.3 (Tenant decoupling) has been implemented, remove this exception and this test.
        ModuleBoundaryMatrix.AssertD015TenantIdentityExceptionPresent();
    }

    // ------------------------------------------------------------------
    // MemberData sources
    // ------------------------------------------------------------------

    public static IEnumerable<object[]> ProductionAndCompositionRootProjects()
    {
        foreach (var p in ModuleBoundaryMatrix.ProductionModuleProjectPaths)
            yield return new object[] { p };
        foreach (var p in ModuleBoundaryMatrix.CompositionRoots)
            yield return new object[] { p };
    }

    public static IEnumerable<object[]> ProductionModuleProjectPathsOnly()
    {
        foreach (var p in ModuleBoundaryMatrix.ProductionModuleProjectPaths)
            yield return new object[] { p };
    }

    // ------------------------------------------------------------------
    // Alternative IVT mechanism scanning
    // ------------------------------------------------------------------

    private static List<string> ScanForAlternativeIvtMechanisms(string solutionDir)
    {
        var violations = new List<string>();

        // Directories to exclude from scanning.
        var excludedDirNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git",
        };

        var csFiles = Directory.GetFiles(solutionDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f, excludedDirNames))
            .Where(f => !ExcludedImplementationFiles.Contains(Path.GetFileName(f)))
            .ToList();

        foreach (var csFile in csFiles)
        {
            var content = File.ReadAllText(csFile);
            var stripped = StripComments(content);
            var relativePath = Path.GetRelativePath(solutionDir, csFile).Replace('\\', '/');

            // [assembly: InternalsVisibleTo(...)]
            if (Regex.IsMatch(stripped,
                @"\[\s*assembly\s*:\s*InternalsVisibleTo\s*\(",
                RegexOptions.IgnoreCase))
            {
                violations.Add($"{relativePath}: source-level [assembly: InternalsVisibleTo(...)] attribute");
            }

            // IgnoresAccessChecksTo
            if (Regex.IsMatch(stripped,
                @"IgnoresAccessChecksTo",
                RegexOptions.IgnoreCase))
            {
                violations.Add($"{relativePath}: IgnoresAccessChecksTo reference");
            }

            // [assembly: AssemblyMetadata(...)] with InternalsVisibleTo
            if (Regex.IsMatch(stripped,
                @"\[\s*assembly\s*:\s*AssemblyMetadata\s*\(.*InternalsVisibleTo",
                RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                violations.Add($"{relativePath}: [assembly: AssemblyMetadata(...)] with InternalsVisibleTo");
            }
        }

        // Scan .csproj files for IgnoresAccessChecksTo
        var csprojFiles = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f, excludedDirNames))
            .ToList();

        foreach (var csprojFile in csprojFiles)
        {
            var content = File.ReadAllText(csprojFile);
            var relativePath = Path.GetRelativePath(solutionDir, csprojFile).Replace('\\', '/');

            if (Regex.IsMatch(content, @"IgnoresAccessChecksTo", RegexOptions.IgnoreCase))
            {
                violations.Add($"{relativePath}: IgnoresAccessChecksTo in project file");
            }
        }

        return violations;
    }

    private static bool IsInExcludedDirectory(string filePath, HashSet<string> excludedDirNames)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
        {
            if (excludedDirNames.Contains(part))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Strips C# comments (// line comments and /* ... */ block comments) to avoid
    /// false positives from comment text matching search patterns.
    /// </summary>
    private static string StripComments(string input)
    {
        var result = new System.Text.StringBuilder(input.Length);
        var i = 0;
        while (i < input.Length)
        {
            // Line comment
            if (i + 1 < input.Length && input[i] == '/' && input[i + 1] == '/')
            {
                while (i < input.Length && input[i] != '\n' && input[i] != '\r')
                    i++;
                continue;
            }

            // Block comment
            if (i + 1 < input.Length && input[i] == '/' && input[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/'))
                    i++;
                i += 2; // skip */
                continue;
            }

            // String literal (verbatim @"""...")
            if (input[i] == '@' && i + 1 < input.Length && input[i + 1] == '"')
            {
                result.Append(input[i]);
                result.Append(input[i + 1]);
                i += 2;
                while (i < input.Length)
                {
                    result.Append(input[i]);
                    if (input[i] == '"' && i + 1 < input.Length && input[i + 1] == '"')
                    {
                        i += 2; // skip escaped quote
                        continue;
                    }
                    if (input[i] == '"')
                        break;
                    i++;
                }
                continue;
            }

            // Regular string literal
            if (input[i] == '"')
            {
                result.Append(input[i]);
                i++;
                while (i < input.Length)
                {
                    result.Append(input[i]);
                    if (input[i] == '\\' && i + 1 < input.Length)
                    {
                        result.Append(input[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (input[i] == '"')
                        break;
                    i++;
                }
                continue;
            }

            result.Append(input[i]);
            i++;
        }
        return result.ToString();
    }
}
