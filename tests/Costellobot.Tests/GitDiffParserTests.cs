// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public static class GitDiffParserTests
{
    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_One_File_NuGet()
    {
        // Arrange
        string diff = GetDiff("NuGet.OneFile");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("AWSSDK.SecurityToken", ("3.7.400.10", "3.7.400.11"));
        packages.ShouldContainKeyAndValue("AWSSDK.SimpleSystemsManagement", ("3.7.401.8", "3.7.401.9"));
        packages.Count.ShouldBe(2);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_Multiple_Files_NuGet()
    {
        // Arrange
        string diff = GetDiff("NuGet.MultipleFiles");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("Microsoft.AspNetCore.Mvc.Testing", ("8.0.8", "9.0.0-preview.7.24406.2"));
        packages.ShouldContainKeyAndValue("Microsoft.EntityFrameworkCore.Sqlite", ("8.0.8", "9.0.0-preview.7.24405.3"));
        packages.Count.ShouldBe(2);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_No_Package_Updates_NuGet()
    {
        // Arrange
        string diff = GetDiff("NuGet.NoUpdates");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeFalse();
        packages.ShouldBeNull();
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_One_Package_Npm()
    {
        // Arrange
        string diff = GetDiff("npm.OnePackage");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("webpack-cli", ("6.0.0", "6.0.1"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_One_Package_Last_Line_Npm()
    {
        // Arrange
        string diff = GetDiff("npm.OnePackageLastLine");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("webpack-remove-empty-scripts", ("1.0.3", "1.0.4"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Multiple_Packages_Npm()
    {
        // Arrange
        string diff = GetDiff("npm.MultiplePackages");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("@typescript-eslint/eslint-plugin", ("8.19.1", "8.20.0"));
        packages.ShouldContainKeyAndValue("@typescript-eslint/parser", ("8.19.1", "8.20.0"));
        packages.Count.ShouldBe(2);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Docker_Image()
    {
        // Arrange
        string diff = GetDiff("Docker.OneFile");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("mcr.microsoft.com/dotnet/sdk", ("9.0.301-faa2daf2b72cbe787ee1882d9651fa4ef3e938ee56792b8324516f5a448f3abe", "9.0.301-b768b444028d3c531de90a356836047e48658cd1e26ba07a539a6f1a052a35d9"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_No_Package_Updates_Npm()
    {
        // Arrange
        string diff = GetDiff("npm.None");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeFalse();
        packages.ShouldBeNull();
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Go_Module()
    {
        // Arrange
        string diff = GetDiff("Go.OneFile");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("github.com/onsi/gomega", ("1.37.0", "1.38.0"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Go_Module_With_Require()
    {
        // Arrange
        string diff = GetDiff("Go.OneRequire");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("github.com/google/go-cmp", ("0.6.0", "0.7.0"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Go_Module_No_Changes()
    {
        // Arrange
        string diff = GetDiff("Go.None");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeFalse();
        packages.ShouldBeNull();
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Go_Module_With_Require_Block()
    {
        // Arrange
        string diff = GetDiff("Go.Block");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("github.com/google/go-cmp", ("0.6.0", "0.7.0"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Python_Requirements_Dev()
    {
        // Arrange
        string diff = GetDiff("Python.OneFile");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("ruff", ("0.17.3", "0.17.5"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Python_Requirements()
    {
        // Arrange
        string diff = GetDiff("Python.Requirements");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("fastapi", ("0.162.0", "0.163.1"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Python_Requirements_With_Extras_Markers_And_Hashes()
    {
        // Arrange
        string diff = GetDiff("Python.Multiple");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("fastapi", ("0.162.0", "0.163.1"));
        packages.ShouldContainKeyAndValue("uvicorn", ("0.54.0", "0.54.1"));
        packages.ShouldContainKeyAndValue("pytest", ("9.2.0", "9.2.1"));
        packages.Count.ShouldBe(3);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Python_Requirements_With_No_Newline_At_End_Of_File()
    {
        // Arrange
        string diff = GetDiff("Python.NoNewLine");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("ruff", ("0.17.3", "0.17.5"));
        packages.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("package.json", "    \"webpack-cli\": \"^6.0.0\"", "    \"webpack-cli\": \"^6.0.1\"", "webpack-cli", "6.0.0", "6.0.1")]
    [InlineData("go.mod", "require github.com/google/go-cmp v0.6.0", "require github.com/google/go-cmp v0.7.0", "github.com/google/go-cmp", "0.6.0", "0.7.0")]
    [InlineData("Directory.Packages.props", "<PackageVersion Include=\"Foo\" Version=\"1.0.0\" />", "<PackageVersion Include=\"Foo\" Version=\"1.0.1\" />", "Foo", "1.0.0", "1.0.1")]
    [InlineData("Dockerfile", "FROM mcr.microsoft.com/dotnet/sdk:9.0.301", "FROM mcr.microsoft.com/dotnet/sdk:9.0.302", "mcr.microsoft.com/dotnet/sdk", "9.0.301", "9.0.302")]
    public static void TryParseUpdatedPackages_Parses_Updates_Followed_By_No_Newline_At_End_Of_File_Marker(
        string path,
        string removed,
        string added,
        string name,
        string from,
        string to)
    {
        // Arrange
        string diff = string.Join(
            '\n',
            $"diff --git a/{path} b/{path}",
            "index 7ee474b..628532e 100644",
            $"--- a/{path}",
            $"+++ b/{path}",
            "@@ -1,1 +1,1 @@",
            $"-{removed}",
            "\\ No newline at end of file",
            $"+{added}",
            "\\ No newline at end of file");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue(name, (from, to));
        packages.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(DependencyEcosystem.PyPI, "1.0.0", "1.0.1")]
    [InlineData(DependencyEcosystem.Npm, "2.0.0", "2.0.1")]
    [InlineData(DependencyEcosystem.NuGet, "3.0.0", "3.0.1")]
    [InlineData(DependencyEcosystem.Docker, "4.0.0", "4.0.1")]
    [InlineData(DependencyEcosystem.GoModules, "5.0.0", "5.0.1")]
    public static void TryParseUpdatedPackages_Does_Not_Mix_Versions_Of_Packages_With_The_Same_Name_In_Different_Ecosystems(
        DependencyEcosystem ecosystem,
        string from,
        string to)
    {
        // Arrange
        string diff = """
            diff --git a/requirements.txt b/requirements.txt
            --- a/requirements.txt
            +++ b/requirements.txt
            @@ -1,1 +1,1 @@
            -foo==1.0.0
            +foo==1.0.1
            diff --git a/package.json b/package.json
            --- a/package.json
            +++ b/package.json
            @@ -1,1 +1,1 @@
            -    "foo": "^2.0.0",
            +    "foo": "^2.0.1",
            diff --git a/Directory.Packages.props b/Directory.Packages.props
            --- a/Directory.Packages.props
            +++ b/Directory.Packages.props
            @@ -1,1 +1,1 @@
            -    <PackageVersion Include="foo" Version="3.0.0" />
            +    <PackageVersion Include="foo" Version="3.0.1" />
            diff --git a/Dockerfile b/Dockerfile
            --- a/Dockerfile
            +++ b/Dockerfile
            @@ -1,1 +1,1 @@
            -FROM foo:4.0.0
            +FROM foo:4.0.1
            diff --git a/go.mod b/go.mod
            --- a/go.mod
            +++ b/go.mod
            @@ -1,1 +1,1 @@
            -require foo v5.0.0
            +require foo v5.0.1
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, ecosystem, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("foo", (from, to));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Ignores_Python_Packages_Updated_To_Multiple_Versions()
    {
        // Arrange
        string diff = """
            diff --git a/requirements.txt b/requirements.txt
            --- a/requirements.txt
            +++ b/requirements.txt
            @@ -1,3 +1,3 @@
            -foo==1.0.0 ; python_version < "3.9"
            -foo==2.0.0 ; python_version >= "3.9"
            -bar==1.0.0
            +foo==1.0.1 ; python_version < "3.9"
            +foo==2.0.1 ; python_version >= "3.9"
            +bar==1.0.1
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, DependencyEcosystem.PyPI, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("bar", ("1.0.0", "1.0.1"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Python_Package_Updated_To_The_Same_Version_In_Multiple_Files()
    {
        // Arrange
        string diff = """
            diff --git a/requirements.txt b/requirements.txt
            --- a/requirements.txt
            +++ b/requirements.txt
            @@ -1,1 +1,1 @@
            -foo==1.0.0
            +foo==1.0.1
            diff --git a/requirements-dev.txt b/requirements-dev.txt
            --- a/requirements-dev.txt
            +++ b/requirements-dev.txt
            @@ -1,1 +1,1 @@
            -foo==0.9.0
            +foo==1.0.1
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, DependencyEcosystem.PyPI, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("foo", ("0.9.0", "1.0.1"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Returns_Empty_For_Ecosystem_Without_Updates()
    {
        // Arrange
        string diff = GetDiff("Python.OneFile");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, DependencyEcosystem.Npm, out var packages);

        // Assert
        actual.ShouldBeFalse();
        packages.ShouldBeNull();
    }

    [Fact]
    public static void TryParseUpdatedPackages_Does_Not_Mix_Versions_When_No_Ecosystem_Is_Specified()
    {
        // Arrange
        string diff = """
            diff --git a/requirements.txt b/requirements.txt
            --- a/requirements.txt
            +++ b/requirements.txt
            @@ -1,1 +1,1 @@
            -foo==1.0.0
            +foo==1.0.1
            diff --git a/package.json b/package.json
            --- a/package.json
            +++ b/package.json
            @@ -1,1 +1,1 @@
            -    "foo": "^2.0.0",
            +    "foo": "^2.0.1",
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("foo", ("1.0.0", "1.0.1"));
        packages.Count.ShouldBe(1);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Does_Not_Attribute_Lines_From_Deleted_Files_To_Previous_File()
    {
        // Arrange
        string diff = """
            diff --git a/requirements.txt b/requirements.txt
            index 7ee474b..628532e 100644
            --- a/requirements.txt
            +++ b/requirements.txt
            @@ -1,1 +1,2 @@
             httpx==0.28.1
            +foo==2.0.0
            diff --git a/docs/legacy.txt b/docs/legacy.txt
            deleted file mode 100644
            index 7ee474b..0000000
            --- a/docs/legacy.txt
            +++ /dev/null
            @@ -1,1 +0,0 @@
            -foo==1.0.0
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeFalse();
        packages.ShouldBeNull();
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_Python_Requirements_No_Package_Updates()
    {
        // Arrange
        string diff = GetDiff("Python.None");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeFalse();
        packages.ShouldBeNull();
    }

    [Theory]
    [InlineData("requirements.txt", true)]
    [InlineData("requirements-dev.txt", true)]
    [InlineData("requirements_test.txt", true)]
    [InlineData("dev-requirements.txt", true)]
    [InlineData("Requirements.TXT", true)]
    [InlineData("requirements.in", true)]
    [InlineData("services/app/requirements.txt", true)]
    [InlineData("requirements/base.txt", true)]
    [InlineData("services/app/requirements/prod.in", true)]
    [InlineData("Requirements/dev.txt", true)]
    [InlineData("constraints.txt", true)]
    [InlineData("requirements-constraints.txt", true)]
    [InlineData("requirements/constraints.txt", true)]
    [InlineData("constraints/base.txt", true)]
    [InlineData("requirements/base.md", false)]
    [InlineData("docs/base.txt", false)]
    [InlineData("requirements.txt/base.txt", false)]
    [InlineData("requirements.md", false)]
    [InlineData("requirements", false)]
    [InlineData("notes.txt", false)]
    public static void TryParseUpdatedPackages_Only_Parses_Python_Requirements_Files(string path, bool expected)
    {
        // Arrange
        string diff = $"""
            diff --git a/{path} b/{path}
            index 93f9a10..4b3c3b3 100644
            --- a/{path}
            +++ b/{path}
            @@ -1,2 +1,2 @@
             httpx==0.28.1
            -ruff==0.17.3
            +ruff==0.17.5
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBe(expected);

        if (expected)
        {
            packages.ShouldNotBeNull();
            packages.ShouldContainKeyAndValue("ruff", ("0.17.3", "0.17.5"));
            packages.Count.ShouldBe(1);
        }
        else
        {
            packages.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData("ruff==0.17.3", "ruff==0.17.5", "ruff", "0.17.3", "0.17.5")]
    [InlineData("ruff == 0.17.3", "ruff == 0.17.5", "ruff", "0.17.3", "0.17.5")]
    [InlineData("ruff===0.17.3", "ruff===0.17.5", "ruff", "0.17.3", "0.17.5")]
    [InlineData("Flask_Cors==5.0.0", "Flask_Cors==5.0.1", "flask-cors", "5.0.0", "5.0.1")]
    [InlineData("Flask_Cors==5.0.0", "flask-cors==5.0.1", "flask-cors", "5.0.0", "5.0.1")]
    [InlineData("zope.interface==7.0", "zope.interface==7.1", "zope-interface", "7.0", "7.1")]
    [InlineData("Foo__Bar..Baz==1.0.0", "foo-bar-baz==1.0.1", "foo-bar-baz", "1.0.0", "1.0.1")]
    [InlineData("uvicorn[standard]==0.54.0", "uvicorn[standard]==0.54.1", "uvicorn", "0.54.0", "0.54.1")]
    [InlineData("ruff==0.17.3 # lint", "ruff==0.17.5 # lint", "ruff", "0.17.3", "0.17.5")]
    [InlineData("ruff==0.17.3;python_version>'3.9'", "ruff==0.17.5;python_version>'3.9'", "ruff", "0.17.3", "0.17.5")]
    [InlineData("ruff==0.17.3 \\", "ruff==0.17.5 \\", "ruff", "0.17.3", "0.17.5")]
    [InlineData("ruff==0.17.3 --hash=sha256:aaa", "ruff==0.17.5 --hash=sha256:bbb", "ruff", "0.17.3", "0.17.5")]
    public static void TryParseUpdatedPackages_Parses_Python_Requirement_Lines(
        string removed,
        string added,
        string name,
        string from,
        string to)
    {
        // Arrange
        string diff = $"""
            diff --git a/requirements.txt b/requirements.txt
            --- a/requirements.txt
            +++ b/requirements.txt
            @@ -1,1 +1,1 @@
            -{removed}
            +{added}
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue(name, (from, to));
        packages.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("ruff>=0.17.3", "ruff>=0.17.5")]
    [InlineData("ruff~=0.17.3", "ruff~=0.17.5")]
    [InlineData("ruff<0.17.3", "ruff<0.17.5")]
    [InlineData("ruff==0.17.*", "ruff==0.18.*")]
    [InlineData("ruff==0.17.3,<1.0", "ruff==0.17.5,<1.0")]
    [InlineData("ruff", "ruff")]
    [InlineData("-r requirements-a.txt", "-r requirements-b.txt")]
    [InlineData("--index-url https://a.example/simple", "--index-url https://b.example/simple")]
    [InlineData("# ruff==0.17.3", "# ruff==0.17.5")]
    [InlineData("git+https://github.com/astral-sh/ruff.git@0.17.3#egg=ruff", "git+https://github.com/astral-sh/ruff.git@0.17.5#egg=ruff")]
    [InlineData("ruff==0.17.3rc1", "ruff==0.17.5rc1")]
    [InlineData("ruff-==0.17.3", "ruff-==0.17.5")]
    [InlineData("ruff==0.17.3#comment", "ruff==0.17.5#comment")]
    [InlineData("ruff==", "ruff==")]
    [InlineData("==0.17.3", "==0.17.5")]
    public static void TryParseUpdatedPackages_Ignores_Unsupported_Python_Requirement_Lines(string removed, string added)
    {
        // Arrange
        string diff = $"""
            diff --git a/requirements.txt b/requirements.txt
            --- a/requirements.txt
            +++ b/requirements.txt
            @@ -1,1 +1,1 @@
            -{removed}
            +{added}
            """;

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeFalse();
        packages.ShouldBeNull();
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_Line_Endings(string lineEnding)
    {
        // Arrange
        string diff = GetDiff("NuGet.OneFile").ReplaceLineEndings(lineEnding);

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("AWSSDK.SecurityToken", ("3.7.400.10", "3.7.400.11"));
        packages.ShouldContainKeyAndValue("AWSSDK.SimpleSystemsManagement", ("3.7.401.8", "3.7.401.9"));
        packages.Count.ShouldBe(2);
    }

    private static string GetDiff(string name)
    {
        var type = typeof(GitDiffParserTests);
        var assembly = type.Assembly;
        var resource = assembly.GetManifestResourceStream($"{type.FullName}.{name}.diff")!;
        using var reader = new StreamReader(resource);
        return reader.ReadToEnd();
    }
}
