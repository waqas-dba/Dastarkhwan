using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Validates declared project references in .csproj files against the shared architecture boundary matrix.
/// Parses Dastarkhwan.slnx as the authoritative project inventory, classifies each project, and verifies
/// that every declared reference is explicitly allowed.
/// </summary>
public sealed class ProjectReferenceBoundaryTests
{
    private static readonly string SolutionDir = FindSolutionDirectory();
    private static readonly string SolutionPath = GetAbsoluteProjectPath("Dastarkhwan.slnx");

    [Fact]
    public void Every_production_module_in_slnx_has_a_matrix_entry()
    {
        var productionModules = GetProductionModulesFromSlnx();

        foreach (var module in productionModules)
        {
            Assert.True(
                ModuleBoundaryMatrix.ProductionModules.Contains(module),
                $"Production module '{module}' found in Dastarkhwan.slnx has no entry in " +
                $"ModuleBoundaryMatrix.ProductionModules. Add an explicit matrix entry for the new module.");
        }
    }

    [Fact]
    public void Every_project_reference_declared_by_a_production_module_is_allowed()
    {
        foreach (var projectPath in GetProductionProjectPathsFromSlnx())
        {
            var allowlist = ModuleBoundaryMatrix.ProjectReferenceAllowlist;

            Assert.True(
                allowlist.ContainsKey(projectPath),
                $"Production project '{projectPath}' has no project-reference allowlist entry in " +
                $"ModuleBoundaryMatrix.ProjectReferenceAllowlist.");

            var declaredRefs = GetDeclaredProjectReferences(projectPath);
            var allowedRefs = allowlist[projectPath];

            foreach (var declaredRef in declaredRefs)
            {
                var referencedProjectName = Path.GetFileNameWithoutExtension(declaredRef);
                Assert.False(
                    ModuleBoundaryMatrix.TestProjects.Contains(referencedProjectName),
                    $"Production project '{projectPath}' references test project '{declaredRef}'. " +
                    "Production modules must not reference test projects.");

                Assert.True(
                    allowedRefs.Contains(declaredRef),
                    $"Production project '{projectPath}' declares a forbidden ProjectReference to '{declaredRef}'. " +
                    $"Allowed references: {string.Join(", ", allowedRefs)}.");
            }
        }
    }

    [Fact]
    public void SampleHost_project_references_match_its_explicit_allowlist()
    {
        var sampleHostPath = "CoreKit.SampleHost/CoreKit.SampleHost.csproj";
        var declaredRefs = GetDeclaredProjectReferences(sampleHostPath);
        var allowedRefs = ModuleBoundaryMatrix.ProjectReferenceAllowlist[sampleHostPath];

        foreach (var declaredRef in declaredRefs)
        {
            var referencedProjectName = Path.GetFileNameWithoutExtension(declaredRef);
            Assert.False(
                ModuleBoundaryMatrix.TestProjects.Contains(referencedProjectName),
                $"SampleHost references test project '{declaredRef}'. " +
                "The composition root must not reference test projects.");

            Assert.True(
                allowedRefs.Contains(declaredRef),
                $"SampleHost declares a forbidden ProjectReference to '{declaredRef}'. " +
                $"Allowed references: {string.Join(", ", allowedRefs)}.");
        }
    }

    [Fact]
    public void Project_reference_includes_are_static_paths()
    {
        foreach (var csprojPath in GetAllCsprojPathsFromSlnx())
        {
            var absoluteCsprojPath = GetAbsoluteProjectPath(csprojPath);
            var doc = XDocument.Load(absoluteCsprojPath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            foreach (var pr in doc.Root!.Descendants(ns + "ProjectReference"))
            {
                var include = pr.Attribute("Include")?.Value ?? string.Empty;
                Assert.False(
                    IsDynamicReference(include),
                    $"Project '{csprojPath}' uses a dynamic ProjectReference Include '{include}'. " +
                    "Dynamic references bypass boundary checks and are not supported.");
            }
        }
    }

    [Fact]
    public void InternalsVisibleTo_declarations_match_the_friend_assembly_allowlist()
    {
        foreach (var csprojPath in GetAllCsprojPathsFromSlnx())
        {
            var absoluteCsprojPath = GetAbsoluteProjectPath(csprojPath);
            var doc = XDocument.Load(absoluteCsprojPath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var assemblyName = Path.GetFileNameWithoutExtension(csprojPath);

            foreach (var ivt in doc.Root!.Descendants(ns + "InternalsVisibleTo"))
            {
                var include = ivt.Attribute("Include")?.Value ?? string.Empty;

                Assert.True(
                    ModuleBoundaryMatrix.InternalsVisibleToAllowlist.TryGetValue(assemblyName, out var friends) &&
                    friends.Contains(include),
                    $"Assembly '{assemblyName}' declares InternalsVisibleTo '{include}' which is not in " +
                    $"ModuleBoundaryMatrix.InternalsVisibleToAllowlist.");
            }
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Converts a solution-relative project path to an absolute file path.</summary>
    private static string GetAbsoluteProjectPath(string solutionRelativePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(SolutionDir, solutionRelativePath));
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException(
                $"Project file not found: '{absolutePath}' (from solution-relative path '{solutionRelativePath}'). " +
                $"Solution directory: '{SolutionDir}'.");
        }
        return absolutePath;
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Dastarkhwan.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Dastarkhwan.slnx not found by searching upward from '{Directory.GetCurrentDirectory()}'. " +
            "Tests must be run from within the repository directory tree.");
    }

    private static IEnumerable<XElement> GetSlnxProjects()
    {
        var doc = XDocument.Load(SolutionPath);
        return doc.Root!.Descendants("Project");
    }

    private static List<string> GetAllCsprojPathsFromSlnx()
    {
        var result = new List<string>();
        foreach (var p in GetSlnxProjects())
        {
            var path = p.Attribute("Path")?.Value;
            if (path is not null)
                result.Add(path);
        }
        return result;
    }

    private static List<string> GetProductionModulesFromSlnx()
    {
        var result = new List<string>();
        foreach (var p in GetAllCsprojPathsFromSlnx())
        {
            if (IsProductionModule(p))
                result.Add(Path.GetFileNameWithoutExtension(p));
        }
        return result;
    }

    private static List<string> GetProductionProjectPathsFromSlnx()
    {
        var result = new List<string>();
        foreach (var p in GetAllCsprojPathsFromSlnx())
        {
            if (IsProductionModule(p) || IsCompositionRoot(p))
                result.Add(p);
        }
        return result;
    }

    private static bool IsProductionModule(string projectPath)
    {
        return !IsTestProject(projectPath) && !IsCompositionRoot(projectPath);
    }

    private static bool IsTestProject(string projectPath)
    {
        return projectPath.Contains(".Tests");
    }

    private static bool IsCompositionRoot(string projectPath)
    {
        return projectPath.Equals("CoreKit.SampleHost/CoreKit.SampleHost.csproj", StringComparison.Ordinal);
    }

    private static List<string> GetDeclaredProjectReferences(string solutionRelativeCsprojPath)
    {
        var csprojFullPath = GetAbsoluteProjectPath(solutionRelativeCsprojPath);
        var doc = XDocument.Load(csprojFullPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var refs = new List<string>();
        foreach (var pr in doc.Root!.Descendants(ns + "ProjectReference"))
        {
            var include = pr.Attribute("Include")?.Value ?? string.Empty;
            refs.Add(ResolveProjectReference(solutionRelativeCsprojPath, include));
        }
        return refs;
    }

    private static string ResolveProjectReference(string solutionRelativeCsprojPath, string include)
    {
        var csprojFullPath = GetAbsoluteProjectPath(solutionRelativeCsprojPath);
        var csprojDir = Path.GetDirectoryName(csprojFullPath)!;
        var resolvedFullPath = Path.GetFullPath(Path.Combine(csprojDir, include));
        var relativePath = Path.GetRelativePath(SolutionDir, resolvedFullPath);
        return relativePath.Replace('\\', '/');
    }

    private static bool IsDynamicReference(string include)
    {
        return include.Contains("$(") ||
               include.Contains("@(") ||
               include.Contains("%(") ||
               include.Contains("*") ||
               include.Contains("?") ||
               include.Contains("{") ||
               include.Contains("}");
    }
}
