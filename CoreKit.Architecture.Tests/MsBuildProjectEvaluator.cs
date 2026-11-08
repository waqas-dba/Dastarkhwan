using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Evaluates .csproj projects using <c>dotnet msbuild -getProperty</c> and
/// <c>dotnet msbuild -getItem</c> through a subprocess.
///
/// Reads <c>AssemblyName</c>, <c>ProjectReference</c>, and
/// <c>InternalsVisibleTo</c> for both Debug and Release configurations.
/// Follows imported .props/.targets and evaluates conditions (MSBuild handles this).
/// Does not build, restore, or execute build targets.
///
/// Results are cached per (project, configuration). All project paths are
/// normalized to solution-relative forward-slash paths; comparison is
/// case-insensitive.
///
/// Limitation: evaluation reflects the statically evaluated project graph at
/// the time of the call. It does not detect references added later by
/// arbitrary MSBuild target execution.
/// </summary>
internal sealed class MsBuildProjectEvaluator
{
    private readonly string _solutionDir;
    private readonly Dictionary<(string projectPath, string configuration), EvaluationResult> _cache
        = new(TupleStringComparer.Instance);

    /// <summary>
    /// Case-insensitive equality comparer for (projectPath, configuration) cache keys.
    /// Both tuple fields are compared using StringComparison.OrdinalIgnoreCase.
    /// </summary>
    private sealed class TupleStringComparer : IEqualityComparer<(string projectPath, string configuration)>
    {
        public static readonly TupleStringComparer Instance = new();

        private TupleStringComparer() { }

        public bool Equals(
            (string projectPath, string configuration) x,
            (string projectPath, string configuration) y)
            => string.Equals(x.projectPath, y.projectPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.configuration, y.configuration, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string projectPath, string configuration) obj)
        {
            return HashCode.Combine(
                obj.projectPath == null
                    ? 0
                    : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.projectPath),
                obj.configuration == null
                    ? 0
                    : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.configuration));
        }
    }

    public MsBuildProjectEvaluator(string solutionDir)
    {
        _solutionDir = Path.GetFullPath(solutionDir);
    }

    /// <summary>Evaluates a project for a given configuration, with caching.</summary>
    public EvaluationResult Evaluate(string solutionRelativeProjectPath, string configuration)
    {
        var key = (solutionRelativeProjectPath, configuration);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var result = EvaluateInternal(solutionRelativeProjectPath, configuration);
        _cache[key] = result;
        return result;
    }

    private EvaluationResult EvaluateInternal(string solutionRelativeProjectPath, string configuration)
    {
        var absoluteCsproj = GetAbsoluteProjectPath(solutionRelativeProjectPath);

        // Detect multi-targeting (<TargetFrameworks> plural) before evaluating.
        var targetFrameworks = RunGetProperty(absoluteCsproj, configuration, "TargetFrameworks");
        if (!string.IsNullOrWhiteSpace(targetFrameworks))
        {
            throw new InvalidOperationException(
                $"Unsupported multi-targeting in project '{solutionRelativeProjectPath}': " +
                $"TargetFrameworks='{targetFrameworks}'. Projects must use single TargetFramework.");
        }

        var assemblyName = RunGetProperty(absoluteCsproj, configuration, "AssemblyName");
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            throw new InvalidOperationException(
                $"Failed to evaluate AssemblyName for project '{solutionRelativeProjectPath}' " +
                $"in configuration '{configuration}'. The MSBuild SDK or required CLI switches may be unavailable.");
        }

        var projectRefsRaw = RunGetItem(absoluteCsproj, configuration, "ProjectReference");
        var ivtRaw = RunGetItem(absoluteCsproj, configuration, "InternalsVisibleTo");

        var projectRefs = projectRefsRaw
            .Select(r => NormalizeProjectReference(solutionRelativeProjectPath, r))
            .ToList();

        var internalsVisibleTo = ivtRaw
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToList();

        return new EvaluationResult
        {
            ProjectPath = solutionRelativeProjectPath,
            Configuration = configuration,
            AssemblyName = assemblyName.Trim(),
            ProjectReferences = projectRefs,
            InternalsVisibleTo = internalsVisibleTo,
        };
    }

    /// <summary>Runs <c>dotnet msbuild -getProperty</c> and returns the trimmed value.</summary>
    private string RunGetProperty(string absoluteCsproj, string configuration, string propertyName)
    {
        var arguments =
            $"msbuild \"{absoluteCsproj}\" " +
            $"-getProperty:{propertyName} " +
            $"-p:Configuration={configuration} " +
            $"-nologo";

        var output = RunDotnetMsbuild(arguments);
        return output.Trim();
    }

    /// <summary>Runs <c>dotnet msbuild -getItem</c> and returns item Include values.</summary>
    private List<string> RunGetItem(string absoluteCsproj, string configuration, string itemName)
    {
        var arguments =
            $"msbuild \"{absoluteCsproj}\" " +
            $"-getItem:{itemName} " +
            $"-p:Configuration={configuration} " +
            $"-nologo";

        var output = RunDotnetMsbuild(arguments);
        return ParseGetItemOutput(output);
    }

    /// <summary>Executes <c>dotnet msbuild ...</c> and captures stdout.</summary>
    private string RunDotnetMsbuild(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _solutionDir,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException(
                    "Failed to start 'dotnet msbuild'. The .NET SDK may not be installed or on PATH.");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"dotnet msbuild exited with code {process.ExitCode} for arguments: {arguments}." +
                    Environment.NewLine +
                    $"stderr: {stderr.Trim()}");
            }

            return stdout;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start 'dotnet'. The .NET SDK may not be installed or on PATH. " +
                $"Inner error: {ex.Message}", ex);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to execute 'dotnet msbuild {arguments}'. " +
                $"Inner error: {ex.Message}", ex);
        }
    }

    /// <summary>Parses -getItem output (JSON) into item Identity values.</summary>
    private List<string> ParseGetItemOutput(string output)
    {
        var result = new List<string>();
        var trimmed = output.Trim();

        if (trimmed.Length == 0)
            return result;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            // MSBuild -getItem returns a JSON object with an "Items" property
            // containing item collections keyed by item type name.
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Items", out var itemsProp) &&
                itemsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var itemCollection in itemsProp.EnumerateObject())
                {
                    if (itemCollection.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in itemCollection.Value.EnumerateArray())
                        {
                            var identity = TryGetStringProperty(item, "Identity");
                            if (identity is not null)
                                result.Add(identity);
                        }
                    }
                }
                return result;
            }

            throw new InvalidOperationException(
                $"Unexpected JSON structure in dotnet msbuild -getItem output. " +
                $"Expected a root object with an 'Items' property containing item collections. " +
                $"Output was: {trimmed}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse dotnet msbuild -getItem JSON output: {ex.Message}. " +
                $"Output was: {trimmed}", ex);
        }
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    /// <summary>
    /// Normalizes a ProjectReference Include value (which may be a relative path
    // from the referencing project's directory) to a solution-relative forward-slash path.
    /// </summary>
    private string NormalizeProjectReference(string solutionRelativeCsprojPath, string include)
    {
        var csprojAbsolute = GetAbsoluteProjectPath(solutionRelativeCsprojPath);
        var csprojDir = Path.GetDirectoryName(csprojAbsolute)!;
        var resolvedFullPath = Path.GetFullPath(Path.Combine(csprojDir, include));
        var relativePath = Path.GetRelativePath(_solutionDir, resolvedFullPath);
        return relativePath.Replace('\\', '/');
    }

    private string GetAbsoluteProjectPath(string solutionRelativePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(_solutionDir, solutionRelativePath));
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException(
                $"Project file not found: '{absolutePath}' (from solution-relative path '{solutionRelativePath}'). " +
                $"Solution directory: '{_solutionDir}'.");
        }
        return absolutePath;
    }
}

/// <summary>Result of evaluating one project in one configuration.</summary>
internal sealed class EvaluationResult
{
    public string ProjectPath { get; init; } = string.Empty;
    public string Configuration { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public IReadOnlyList<string> ProjectReferences { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> InternalsVisibleTo { get; init; } = Array.Empty<string>();
}
