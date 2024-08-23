// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public static class GitDiffParserTests
{
    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_One_File()
    {
        // Arrange
        string diff = GetDiff("OneFile");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("AWSSDK.SecurityToken", (new("3.7.400.10"), new("3.7.400.11")));
        packages.ShouldContainKeyAndValue("AWSSDK.SimpleSystemsManagement", (new("3.7.401.8"), new("3.7.401.9")));
        packages.Count.ShouldBe(2);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_Multiple_Files()
    {
        // Arrange
        string diff = GetDiff("MultipleFiles");

        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        actual.ShouldBeTrue();
        packages.ShouldNotBeNull();
        packages.ShouldContainKeyAndValue("Microsoft.AspNetCore.Mvc.Testing", (new("8.0.8"), new("9.0.0-preview.7.24406.2")));
        packages.ShouldContainKeyAndValue("Microsoft.EntityFrameworkCore.Sqlite", (new("8.0.8"), new("9.0.0-preview.7.24405.3")));
        packages.Count.ShouldBe(2);
    }

    [Fact]
    public static void TryParseUpdatedPackages_Parses_Diff_Correctly_For_No_Package_Updates()
    {
        // Arrange
        string diff = GetDiff("NoUpdates");

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
