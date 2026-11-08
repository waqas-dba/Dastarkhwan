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
}