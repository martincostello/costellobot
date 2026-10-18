// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MartinCostello.Costellobot.Models;
using static MartinCostello.Costellobot.FuzzGenerators;

namespace MartinCostello.Costellobot;

public static class DependencyHighlighterFuzzTests
{
    private static readonly Gen<string> DigestVersions =
        from version in Versions
        from digest in Characters("0123456789abcdef", 64, 64)
        select $"{version}-{digest}";

    private static readonly Gen<string> VersionStrings = Gen.OneOf(
        Versions.Select((p) => p.ToString()),
        Gen.Choose(0, 99).Select((p) => p.ToString(CultureInfo.InvariantCulture)),
        DigestVersions,
        Gen.Elements("latest", "main", "v1", "1.x", "lts-alpine", string.Empty),
        ArbMap.Default.GeneratorFor<NonNull<string>>().Select((p) => p.Get));

    private static readonly Gen<TrustedDependency> Dependencies =
        from id in Gen.OneOf(DependencyName, Gen.Elements("a", "b", "c"))
        from version in VersionStrings
        from trustedAt in Gen.OneOf(
            Gen.Constant<DateTimeOffset?>(null),
            Gen.Choose(0, 10_000).Select((p) => (DateTimeOffset?)new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddHours(p)))
        select new TrustedDependency(id, version) { TrustedAt = trustedAt };

    [Property]
    public static Property Order_Preserves_Dependencies_And_Orders_By_Id()
    {
        var arbitrary =
            (from ecosystem in Gen.Elements(Enum.GetValues<DependencyEcosystem>())
             from dependencies in Dependencies.ListOf()
             select (Ecosystem: ecosystem, Dependencies: dependencies)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Act
            var actual = DependencyHighlighter.Order(input.Ecosystem, input.Dependencies, (p) => p.TrustedAt);

            // Assert
            actual.ShouldNotBeNull();
            actual.Count.ShouldBe(input.Dependencies.Count);

            // Every input appears exactly once in the output
            actual.Select((p) => p.Dependency).ShouldBe(input.Dependencies, ignoreOrder: true);

            // The output is ordered by ID
            actual.Select((p) => p.Dependency.Id).ShouldBeInOrder(SortDirection.Ascending, StringComparer.Ordinal);

            foreach (var group in actual.GroupBy((p) => p.Dependency.Id, StringComparer.Ordinal))
            {
                var items = group.ToList();

                // At least one version of each dependency is current
                items.ShouldContain((p) => !p.IsOutdated);

                // Current versions are listed before outdated versions
                items.Select((p) => p.IsOutdated).ShouldBeInOrder();
            }
        });
    }

    [Property]
    public static Property Order_Marks_Only_Newest_Version_As_Current_For_Semantic_Versions()
    {
        var arbitrary =
            (from ecosystem in Gen.Elements(DependencyEcosystem.NuGet, DependencyEcosystem.Npm, DependencyEcosystem.GitHubActions, DependencyEcosystem.Docker)
             from id in DependencyName
             from versions in DistinctListOf(Versions, 6, EqualityComparer<SemanticVersion>.Default)
             select (Ecosystem: ecosystem, Id: id, Versions: versions)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            var dependencies = input.Versions
                .Select((p) => new TrustedDependency(input.Id, p.ToString()))
                .ToList();

            var newest = input.Versions
                .MaxBy((p) => NuGet.Versioning.NuGetVersion.Parse(p.ToString()))!;

            // Act
            var actual = DependencyHighlighter.Order(input.Ecosystem, dependencies, (p) => p.TrustedAt);

            // Assert
            actual.Count.ShouldBe(dependencies.Count);

            var current = actual.Where((p) => !p.IsOutdated).ToList();

            current.Count.ShouldBe(1);
            current[0].Dependency.Version.ShouldBe(newest.ToString());
            actual[0].Dependency.Version.ShouldBe(newest.ToString());
        });
    }
}
