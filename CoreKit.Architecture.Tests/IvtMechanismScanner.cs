using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private static readonly CSharpParseOptions ReleaseParseOptions = new(LanguageVersion.Latest, preprocessorSymbols: new[] { "TRACE" });

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
        // Collect global using aliases from all files (project-level aliases).
        var globalAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, text) in files)
            CollectAliases(text, globalAliases, isGlobal: true);

        var findings = new List<IvtFinding>();

        foreach (var (relativePath, text) in files)
        {
            // Combine global aliases with file-level aliases (file-level takes precedence).
            var allAliases = new Dictionary<string, string>(globalAliases, StringComparer.Ordinal);
            CollectAliases(text, allAliases, isGlobal: false);

            // Parse under two preprocessor symbol sets so #if DEBUG / #if RELEASE blocks are both covered.
            foreach (var options in new[] { DebugParseOptions, ReleaseParseOptions })
            {
                var tree = CSharpSyntaxTree.ParseText(text, options: options);
                var root = tree.GetRoot();

                foreach (var attributeList in root.DescendantNodes().OfType<AttributeListSyntax>())
                {
                    if (attributeList.Target is not { Identifier: { ValueText: "assembly" } })
                        continue;

                    foreach (var attribute in attributeList.Attributes)
                    {
                        var attributeName = ResolveAttributeName(attribute.Name, allAliases);
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

    private static void CollectAliases(string text, Dictionary<string, string> aliases, bool isGlobal)
    {
        var tree = CSharpSyntaxTree.ParseText(text);
        foreach (var usingDirective in tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (IsGlobalUsing(usingDirective) != isGlobal)
                continue;
            if (usingDirective.Alias is null)
                continue;

            var name = usingDirective.Name?.ToString();
            if (name is not null)
                aliases[usingDirective.Alias.Name.Identifier.ValueText] = name;
        }
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
            text = resolved + (firstDot < 0 ? string.Empty : text[firstDot..]);
        }

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
}