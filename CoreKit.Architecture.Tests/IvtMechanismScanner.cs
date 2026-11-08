using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Result of scanning a source file for assembly-visibility bypass mechanisms.
/// </summary>
internal sealed record IvtFinding(string RelativePath, int Line, string Kind);

/// <summary>
/// Scans C# source files for assembly-visibility bypass mechanisms using Roslyn syntax analysis.
///
/// Unlike text-based scanning, Roslyn parsing distinguishes comments, string literals,
/// interpolated strings, and raw strings from actual attribute syntax, eliminating false positives.
/// Parsing is performed under two preprocessor symbol sets (DEBUG;TRACE and TRACE) so that
/// attribute blocks inside #if DEBUG / #if RELEASE blocks are both covered.
/// </summary>
internal static class IvtMechanismScanner
{
    private const string ArchitectureTestProject = "CoreKit.Architecture.Tests/CoreKit.Architecture.Tests.csproj";

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git",
    };

    private static readonly CSharpParseOptions DebugParseOptions = new(LanguageVersion.Latest, preprocessorSymbols: new[] { "DEBUG", "TRACE" });
    private static readonly CSharpParseOptions ReleaseParseOptions = new(LanguageVersion.Latest, preprocessorSymbols: new[] { "TRACE", "RELEASE" });

    /// <summary>
    /// Scans every project in Dastarkhwan.slnx except the architecture test project itself.
    /// </summary>
    public static IReadOnlyList<IvtFinding> ScanSolution(string solutionDir)
    {
        var files = new List<(string Path, string Text)>();
        foreach (var projectPath in SolutionInventory.AllSolutionProjects)
        {
            if (string.Equals(projectPath, ArchitectureTestProject, StringComparison.OrdinalIgnoreCase))
                continue;

            var projectDir = Path.GetDirectoryName(Path.Combine(solutionDir, projectPath))!;
            foreach (var csFile in Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsInExcludedDirectory(csFile, ExcludedDirectoryNames))
                    continue;

                var relativePath = Path.GetRelativePath(solutionDir, csFile).Replace('\\', '/');
                var text = File.ReadAllText(csFile);
                files.Add((relativePath, text));
            }
        }

        return ScanProject(files);
    }

    /// <summary>
    /// Scans a collection of in-memory source files for assembly-visibility bypass mechanisms.
    /// </summary>
    internal static IReadOnlyList<IvtFinding> ScanProject(IReadOnlyList<(string Path, string Text)> files)
    {
        // Collect global (project-level) aliases per configuration from all files.
        var globalDebugAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var globalReleaseAliases = new Dictionary<string, string>(StringComparer.Ordinal);

        // Collect per-file aliases per configuration.
        var fileDebugAliases = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var fileReleaseAliases = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var (path, text) in files)
        {
            var debugAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            var releaseAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            CollectAliases(text, globalDebugAliases, globalReleaseAliases, debugAliases, releaseAliases);
            fileDebugAliases[path] = debugAliases;
            fileReleaseAliases[path] = releaseAliases;
        }

        var findings = new List<IvtFinding>();

        foreach (var (relativePath, text) in files)
        {
            var debugAliases = fileDebugAliases[relativePath];
            var releaseAliases = fileReleaseAliases[relativePath];

            foreach (var options in new[] { DebugParseOptions, ReleaseParseOptions })
            {
                var isDebug = options.PreprocessorSymbolNames.Contains("DEBUG");
                var combinedAliases = isDebug
                    ? MergeAliases(globalDebugAliases, debugAliases)
                    : MergeAliases(globalReleaseAliases, releaseAliases);

                var tree = CSharpSyntaxTree.ParseText(text, options: options);
                var root = tree.GetRoot();

                foreach (var attributeList in root.DescendantNodes().OfType<AttributeListSyntax>())
                {
                    if (attributeList.Target is not { Identifier: { ValueText: "assembly" } })
                        continue;

                    foreach (var attribute in attributeList.Attributes)
                    {
                        var attributeName = ResolveAttributeName(attribute.Name, combinedAliases);
                        if (attributeName == null)
                            continue;

                        var finalSegment = NormalizeAttributeName(attributeName);
                        if (finalSegment == null)
                            continue;

                        var line = attribute.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                        switch (finalSegment)
                        {
                            case "InternalsVisibleTo":
                                findings.Add(new IvtFinding(relativePath, line, "InternalsVisibleTo attribute"));
                                break;
                            case "IgnoresAccessChecksTo":
                                findings.Add(new IvtFinding(relativePath, line, "IgnoresAccessChecksTo attribute"));
                                break;
                            case "AssemblyMetadata":
                                foreach (var arg in attribute.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>())
                                {
                                    if (arg.Expression is LiteralExpressionSyntax literal &&
                                        literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                                        literal.Token.ValueText.Contains("InternalsVisibleTo"))
                                    {
                                        findings.Add(new IvtFinding(relativePath, line, "AssemblyMetadata with InternalsVisibleTo"));
                                        break;
                                    }
                                }
                                break;
                        }
                    }
                }
            }
        }

        return findings
            .Distinct()
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ThenBy(f => f.Line)
            .ThenBy(f => f.Kind, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Collects file-level using aliases from a source file under both Debug and Release
    /// preprocessor symbol sets. Only compilation-unit-level aliases are collected;
    /// namespace-scoped aliases do not influence assembly-level attributes.
    /// </summary>
    private static void CollectAliases(string text,
        Dictionary<string, string> globalDebugAliases,
        Dictionary<string, string> globalReleaseAliases,
        Dictionary<string, string> debugAliases,
        Dictionary<string, string> releaseAliases)
    {
        foreach (var options in new[] { DebugParseOptions, ReleaseParseOptions })
        {
            var tree = CSharpSyntaxTree.ParseText(text, options: options);
            foreach (var usingDirective in tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                // Only compilation-unit-level aliases apply to assembly-level attributes.
                // Aliases declared inside namespace declarations are scoped to that namespace.
                if (usingDirective.Parent is not CompilationUnitSyntax)
                    continue;
                if (usingDirective.Alias is null)
                    continue;

                var name = usingDirective.Name?.ToString();
                if (name is not null)
                {
                    var aliasName = usingDirective.Alias.Name.Identifier.ValueText;
                    bool isGlobal = usingDirective.GetFirstToken().IsKind(SyntaxKind.GlobalKeyword);

                    if (options.PreprocessorSymbolNames.Contains("DEBUG"))
                    {
                        if (isGlobal)
                            TryAddAlias(globalDebugAliases, aliasName, name);
                        else
                            TryAddAlias(debugAliases, aliasName, name);
                    }
                    if (options.PreprocessorSymbolNames.Contains("RELEASE"))
                    {
                        if (isGlobal)
                            TryAddAlias(globalReleaseAliases, aliasName, name);
                        else
                            TryAddAlias(releaseAliases, aliasName, name);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds an alias to the dictionary. If the alias already exists with a different value,
    /// the conflicting alias is not added (silent overwrite is prevented).
    /// </summary>
    private static void TryAddAlias(Dictionary<string, string> aliases, string name, string value)
    {
        if (aliases.TryGetValue(name, out var existing))
        {
            if (!string.Equals(existing, value, StringComparison.Ordinal))
                aliases.Remove(name); // Conflicting alias - do not resolve ambiguously
        }
        else
        {
            aliases[name] = value;
        }
    }

    /// <summary>
    /// Merges file-level aliases into global aliases. File-level aliases take precedence.
    /// </summary>
    private static Dictionary<string, string> MergeAliases(
        Dictionary<string, string> global,
        Dictionary<string, string> file)
    {
        var merged = new Dictionary<string, string>(global, StringComparer.Ordinal);
        foreach (var kvp in file)
            TryAddAlias(merged, kvp.Key, kvp.Value);
        return merged;
    }

    private static string? ResolveAttributeName(NameSyntax? name, Dictionary<string, string> aliases)
    {
        if (name == null)
            return null;

        var text = name.ToString();

        // Strip global:: prefix
        if (text.StartsWith("global::"))
            text = text[8..];

        // Resolve using alias for the first segment
        var firstDot = text.IndexOf('.');
        var firstSegment = firstDot < 0 ? text : text[..firstDot];
        if (aliases.TryGetValue(firstSegment, out var resolved))
        {
            // Alias matched - return resolved name
            return resolved + (firstDot < 0 ? string.Empty : text[firstDot..]);
        }

        // No alias matched - use last segment
        if (text.Contains('.'))
            return text[(text.LastIndexOf('.') + 1)..];

        return text;
    }

    private static string? NormalizeAttributeName(string name)
    {
        var text = name;

        // Strip leading @ (verbatim identifier)
        if (text.StartsWith('@'))
            text = text[1..];

        // Strip global:: prefix
        if (text.StartsWith("global::"))
            text = text[8..];

        // Strip trailing Attribute suffix
        if (text.EndsWith("Attribute") && text.Length > "Attribute".Length)
            text = text[..^9];

        // Take last segment
        var lastDot = text.LastIndexOf('.');
        if (lastDot >= 0)
            text = text[(lastDot + 1)..];

        return text;
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

    private static bool IsGlobalUsing(UsingDirectiveSyntax usingDirective)
    {
        return usingDirective.GetFirstToken().IsKind(SyntaxKind.GlobalKeyword);
    }

    /// <summary>
    /// Scans .csproj files in the solution directory for IgnoresAccessChecksTo references.
    /// Preserved from the previous text-based scanner to catch provider-level suppression
    /// of access checks that Roslyn source scanning cannot see.
    /// </summary>
    public static IReadOnlyList<IvtFinding> ScanProjectFilesForIgnoresAccessChecksTo(string solutionDir)
    {
        var violations = new List<IvtFinding>();

        var csprojFiles = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f, ExcludedDirectoryNames))
            .ToList();

        foreach (var csprojFile in csprojFiles)
        {
            var content = File.ReadAllText(csprojFile);
            var relativePath = Path.GetRelativePath(solutionDir, csprojFile).Replace('\\', '/');

            if (Regex.IsMatch(content, @"IgnoresAccessChecksTo", RegexOptions.IgnoreCase))
            {
                violations.Add(new IvtFinding(relativePath, 0, "IgnoresAccessChecksTo in project file"));
            }
        }

        return violations;
    }
}