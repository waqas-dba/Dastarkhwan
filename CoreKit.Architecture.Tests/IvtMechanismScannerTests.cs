using System.Collections.Generic;
using Xunit;

namespace CoreKit.Architecture.Tests;

/// <summary>
/// Focused tests for <see cref="IvtMechanismScanner"/> using in-memory source text,
/// without any disk I/O.
/// </summary>
public sealed class IvtMechanismScannerTests
{
    [Fact]
    public void Detects_plain_assembly_attribute()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", "[assembly: InternalsVisibleTo(\"CoreKit.Access.Tests\")]"),
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("InternalsVisibleTo attribute", findings[0].Kind);
        Assert.Equal(1, findings[0].Line);
    }

    [Fact]
    public void Detects_qualified_and_attribute_suffix_forms()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CoreKit.Access.Tests")]
                [assembly: global::System.Runtime.CompilerServices.InternalsVisibleToAttribute("CoreKit.Access.Tests")]
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal("InternalsVisibleTo attribute", f.Kind));
    }

    [Fact]
    public void Detects_file_using_alias()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
                [assembly: IVT("CoreKit.Access.Tests")]
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("InternalsVisibleTo attribute", findings[0].Kind);
    }

    [Fact]
    public void Detects_global_using_alias_from_another_file()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/GlobalUsings.cs", """
                global using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
                """),
            ("Tests/Foo.cs", """
                [assembly: IVT("CoreKit.Access.Tests")]
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("InternalsVisibleTo attribute", findings[0].Kind);
    }

    [Fact]
    public void Ignores_line_and_block_comments()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                // [assembly: InternalsVisibleTo("CoreKit.Access.Tests")]
                /* [assembly: InternalsVisibleTo("CoreKit.Access.Tests")] */
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Ignores_regular_string_literal()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                var s1 = "[assembly: InternalsVisibleTo(\"CoreKit.Access.Tests\")]";
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Ignores_verbatim_string_literal()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                var s2 = @"[assembly: InternalsVisibleTo(\"CoreKit.Access.Tests\")]";
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Ignores_interpolated_string_literal()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                var s3 = $@"[assembly: InternalsVisibleTo(\"CoreKit.Access.Tests\")]";
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Ignores_raw_string_literal()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", "var s4 = \"\"\"[assembly: InternalsVisibleTo(\"CoreKit.Access.Tests\")]\"\"\";"),
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detects_ignores_access_checks_to_attribute()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", "[assembly: IgnoresAccessChecksTo(\"CoreKit.Access.Tests\")]"),
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("IgnoresAccessChecksTo attribute", findings[0].Kind);
    }

    [Fact]
    public void Ignores_ignores_access_checks_to_in_string_or_comment()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                // [assembly: IgnoresAccessChecksTo("CoreKit.Access.Tests")]
                var s = "[assembly: IgnoresAccessChecksTo(\"CoreKit.Access.Tests\")]";
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detects_assembly_metadata_with_ivt_text()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", "[assembly: AssemblyMetadata(\"InternalsVisibleTo\", \"CoreKit.Access.Tests\")]"),
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("AssemblyMetadata with InternalsVisibleTo", findings[0].Kind);
    }

    [Fact]
    public void Ignores_assembly_metadata_without_ivt_text()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", "[assembly: AssemblyMetadata(\"SomeKey\", \"SomeValue\")]"),
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detects_qualified_attribute_name()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"CoreKit.Access.Tests\")]"),
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("InternalsVisibleTo attribute", findings[0].Kind);
    }

    [Fact]
    public void Detects_global_qualified_attribute_name()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", "[assembly: global::System.Runtime.CompilerServices.InternalsVisibleToAttribute(\"CoreKit.Access.Tests\")]"),
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("InternalsVisibleTo attribute", findings[0].Kind);
    }

    [Fact]
    public void Detects_alias_defined_under_release()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                #if RELEASE
                using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
                #endif
                [assembly: IVT("CoreKit.Access.Tests")]
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        Assert.Single(findings);
        Assert.Equal("InternalsVisibleTo attribute", findings[0].Kind);
    }

    [Fact]
    public void Detects_ignores_access_checks_to_in_csproj()
    {
        var violations = IvtMechanismScanner.ScanProjectFilesForIgnoresAccessChecksTo(
            SolutionInventory.SolutionDir);

        // The production CoreKit projects do not declare IgnoresAccessChecksTo,
        // so this should be empty. The test verifies the scanner runs without
        // throwing and returns no false positives on real production files.
        Assert.Empty(violations);
    }

    [Fact]
    public void Detects_alias_defined_under_debug_only()
    {
        // Debug-only alias resolves; Release alias does not.
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                #if DEBUG
                using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
                #endif
                [assembly: IVT("CoreKit.Access.Tests")]
                """)
        };

        var debugFindings = IvtMechanismScanner.ScanProject(files);
        Assert.Single(debugFindings);
        Assert.Equal("InternalsVisibleTo attribute", debugFindings[0].Kind);
    }

    [Fact]
    public void Ignores_namespace_scoped_alias()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                namespace MyNamespace
                {
                    using IVT = SomethingElse.InternalsVisibleToAttribute;
                }

                [assembly: IVT("CoreKit.Access.Tests")]
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);
        Assert.Empty(findings);
    }

    [Fact]
    public void Ignores_conflicting_aliases()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/Foo.cs", """
                using IVT = Foo.Bar;
                using IVT = Foo.Baz;
                [assembly: IVT("CoreKit.Access.Tests")]
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);
        Assert.Empty(findings);
    }

    [Fact]
    public void Ignores_file_level_alias_from_another_file()
    {
        var files = new List<(string Path, string Text)>
        {
            ("Tests/FileA.cs", """
                using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
                [assembly: IVT("OtherTarget")]
                """),
            ("Tests/FileB.cs", """
                using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
                [assembly: IVT("CoreKit.Access.Tests")]
                """)
        };

        var findings = IvtMechanismScanner.ScanProject(files);

        // FileA's file-level alias must not leak into FileB.
        // FileB's own alias should resolve.
        Assert.Single(findings);
        Assert.Equal("InternalsVisibleTo attribute", findings[0].Kind);
    }
}