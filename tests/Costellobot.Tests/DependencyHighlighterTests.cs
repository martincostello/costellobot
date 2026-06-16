// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot;

public static class DependencyHighlighterTests
{
    private const string DigestA = "0a972391db0b24ec336e35d1bc98b237237e26f82bf5120cf2f6b1688d1df973";
    private const string DigestB = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string DigestC = "2222222222222222222222222222222222222222222222222222222222222222";

    [Fact]
    public static void Order_Marks_Older_Versions_As_Outdated_For_NuGet()
    {
        // Arrange
        TrustedDependency[] dependencies =
        [
            Trusted("Verify.XunitV3", "28.10.1"),
            Trusted("Verify.XunitV3", "28.11.0"),
            Trusted("Verify.ImageMagick", "3.5.0"),
        ];

        // Act
        var actual = Order(DependencyEcosystem.NuGet, dependencies);

        // Assert
        actual.ShouldSatisfyAllConditions(
            () => Outdated(actual, "Verify.ImageMagick", "3.5.0").ShouldBeFalse(),
            () => Outdated(actual, "Verify.XunitV3", "28.11.0").ShouldBeFalse(),
            () => Outdated(actual, "Verify.XunitV3", "28.10.1").ShouldBeTrue());

        // The newest version of each dependency is displayed first.
        actual.Select((p) => p.Dependency.Version).ShouldBe(["3.5.0", "28.11.0", "28.10.1"]);
    }

    [Fact]
    public static void Order_Treats_Docker_Tag_And_Its_Digest_As_Equivalent()
    {
        // Arrange
        TrustedDependency[] dependencies =
        [
            Trusted("dotnet/runtime", "8.8.0"),
            Trusted("dotnet/runtime", $"8.8.0-{DigestA}"),
        ];

        // Act
        var actual = Order(DependencyEcosystem.Docker, dependencies);

        // Assert - neither is highlighted as outdated as they are the same version
        actual.ShouldAllBe((p) => !p.IsOutdated);
    }

    [Fact]
    public static void Order_Marks_Equivalent_Docker_Versions_As_Outdated_When_Newer_Version_Exists()
    {
        // Arrange
        TrustedDependency[] dependencies =
        [
            Trusted("dotnet/runtime", "8.8.0"),
            Trusted("dotnet/runtime", $"8.8.0-{DigestA}"),
            Trusted("dotnet/runtime", "8.8.1"),
            Trusted("dotnet/runtime", $"8.8.1-{DigestB}"),
        ];

        // Act
        var actual = Order(DependencyEcosystem.Docker, dependencies);

        // Assert - the newer 8.8.1 pair makes both 8.8.0 versions outdated
        actual.ShouldSatisfyAllConditions(
            () => Outdated(actual, "dotnet/runtime", "8.8.1").ShouldBeFalse(),
            () => Outdated(actual, "dotnet/runtime", $"8.8.1-{DigestB}").ShouldBeFalse(),
            () => Outdated(actual, "dotnet/runtime", "8.8.0").ShouldBeTrue(),
            () => Outdated(actual, "dotnet/runtime", $"8.8.0-{DigestA}").ShouldBeTrue());
    }

    [Fact]
    public static void Order_Marks_Older_Docker_Digests_As_Outdated_By_Timestamp()
    {
        // Arrange
        var baseline = new DateTimeOffset(2026, 06, 16, 12, 00, 00, TimeSpan.Zero);

        TrustedDependency[] dependencies =
        [
            Trusted("dotnet/runtime", "8.8.0", baseline),
            Trusted("dotnet/runtime", $"8.8.0-{DigestA}", baseline.AddHours(1)),
            Trusted("dotnet/runtime", $"8.8.0-{DigestB}", baseline.AddHours(2)),
        ];

        // Act
        var actual = Order(DependencyEcosystem.Docker, dependencies);

        // Assert - the plain tag and the newest digest are current; the older digest is outdated
        actual.ShouldSatisfyAllConditions(
            () => Outdated(actual, "dotnet/runtime", "8.8.0").ShouldBeFalse(),
            () => Outdated(actual, "dotnet/runtime", $"8.8.0-{DigestB}").ShouldBeFalse(),
            () => Outdated(actual, "dotnet/runtime", $"8.8.0-{DigestA}").ShouldBeTrue());
    }

    [Fact]
    public static void Order_Marks_Older_Docker_Digests_As_Outdated_By_Timestamp_With_No_Plain_Tag()
    {
        // Arrange
        var baseline = new DateTimeOffset(2026, 06, 16, 12, 00, 00, TimeSpan.Zero);

        TrustedDependency[] dependencies =
        [
            Trusted("dotnet/runtime", $"8.8.0-{DigestA}", baseline.AddHours(2)),
            Trusted("dotnet/runtime", $"8.8.0-{DigestB}", baseline.AddHours(1)),
            Trusted("dotnet/runtime", $"8.8.0-{DigestC}", baseline),
        ];

        // Act
        var actual = Order(DependencyEcosystem.Docker, dependencies);

        // Assert - only the digest with the newest timestamp is current
        actual.ShouldSatisfyAllConditions(
            () => Outdated(actual, "dotnet/runtime", $"8.8.0-{DigestA}").ShouldBeFalse(),
            () => Outdated(actual, "dotnet/runtime", $"8.8.0-{DigestB}").ShouldBeTrue(),
            () => Outdated(actual, "dotnet/runtime", $"8.8.0-{DigestC}").ShouldBeTrue());
    }

    [Fact]
    public static void Order_Treats_Major_Version_Only_As_Equivalent_For_GitHub_Actions()
    {
        // Arrange
        TrustedDependency[] dependencies =
        [
            Trusted("actions/checkout", "23"),
            Trusted("actions/checkout", "23.2.0"),
        ];

        // Act
        var actual = Order(DependencyEcosystem.GitHubActions, dependencies);

        // Assert - the major-only entry is equivalent to the more specific version
        actual.ShouldAllBe((p) => !p.IsOutdated);
    }

    [Fact]
    public static void Order_Marks_Older_Specific_GitHub_Actions_Versions_As_Outdated()
    {
        // Arrange
        TrustedDependency[] dependencies =
        [
            Trusted("actions/checkout", "23"),
            Trusted("actions/checkout", "23.1.0"),
            Trusted("actions/checkout", "23.2.0"),
            Trusted("actions/checkout", "22.5.0"),
        ];

        // Act
        var actual = Order(DependencyEcosystem.GitHubActions, dependencies);

        // Assert
        actual.ShouldSatisfyAllConditions(
            () => Outdated(actual, "actions/checkout", "23").ShouldBeFalse(),
            () => Outdated(actual, "actions/checkout", "23.2.0").ShouldBeFalse(),
            () => Outdated(actual, "actions/checkout", "23.1.0").ShouldBeTrue(),
            () => Outdated(actual, "actions/checkout", "22.5.0").ShouldBeTrue());
    }

    [Fact]
    public static void Order_Marks_Major_Version_Only_As_Outdated_When_Newer_Major_Exists()
    {
        // Arrange
        TrustedDependency[] dependencies =
        [
            Trusted("actions/checkout", "23"),
            Trusted("actions/checkout", "24.0.0"),
        ];

        // Act
        var actual = Order(DependencyEcosystem.GitHubActions, dependencies);

        // Assert - a newer major version makes the major-only entry outdated
        actual.ShouldSatisfyAllConditions(
            () => Outdated(actual, "actions/checkout", "24.0.0").ShouldBeFalse(),
            () => Outdated(actual, "actions/checkout", "23").ShouldBeTrue());
    }

    private static IReadOnlyList<OrderedDependency<TrustedDependency>> Order(
        DependencyEcosystem ecosystem,
        IEnumerable<TrustedDependency> dependencies)
        => DependencyHighlighter.Order(ecosystem, dependencies, (p) => p.TrustedAt);

    private static TrustedDependency Trusted(string id, string version, DateTimeOffset? trustedAt = null)
        => new(id, version) { TrustedAt = trustedAt };

    private static bool Outdated(IEnumerable<OrderedDependency<TrustedDependency>> dependencies, string id, string version)
        => dependencies.Single((p) => p.Dependency.Id == id && p.Dependency.Version == version).IsOutdated;
}
