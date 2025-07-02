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

    private static string GetDiff(string name)
    {
        var type = typeof(GitDiffParserTests);
        var assembly = type.Assembly;
        var resource = assembly.GetManifestResourceStream($"{type.FullName}.{name}.diff")!;
        using var reader = new StreamReader(resource);
        return reader.ReadToEnd();
    }
}
