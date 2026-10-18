// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using static MartinCostello.Costellobot.FuzzGenerators;

namespace MartinCostello.Costellobot;

public static class GitDiffParserFuzzTests
{
    private const string HunkHeader = "@@ -1,3 +1,3 @@";

    private static readonly Gen<PackageUpdate> PackageUpdates =
        from manifest in Gen.Elements(Enum.GetValues<Manifest>())
        from path in ManifestPath(manifest)
        from name in PackageName(manifest)
        from update in Updates
        from digest in Gen.Elements(true, false)
        select new PackageUpdate(manifest, path, name, update, manifest is Manifest.Dockerfile or Manifest.DockerCompose && digest);

    private static readonly Gen<Diff> Diffs =
        from updates in DistinctListOf(PackageUpdates, 5, PackageUpdate.NameComparer)
        from newLine in LineEnding
        select new Diff(updates, newLine);

    private enum Manifest
    {
        NuGet,
        Npm,
        Dockerfile,
        DockerCompose,
        GoModule,
    }

    [Property]
    public static void TryParseUpdatedPackages_Does_Not_Throw_For_Arbitrary_Input(string diff)
    {
        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

        // Assert
        AssertConsistent(actual, packages);
    }

    [Property]
    public static Property TryParseUpdatedPackages_Does_Not_Throw_For_Arbitrary_Manifest_Content()
    {
        var arbitrary =
            (from manifest in Gen.Elements(Enum.GetValues<Manifest>())
             from path in ManifestPath(manifest)
             from removed in ArbMap.Default.GeneratorFor<string>()
             from added in ArbMap.Default.GeneratorFor<string>()
             from prefix in Gen.Elements(string.Empty, "<PackageReference ", "<PackageVersion ", "\"", "FROM ", "image: ", "require ")
             select (Path: path, Removed: prefix + removed, Added: prefix + added)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            string diff = RenderDiff(input.Path, [(input.Removed, input.Added)], "\n");

            // Act
            bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

            // Assert
            AssertConsistent(actual, packages);
        });
    }

    [Property]
    public static Property TryParseUpdatedPackages_Parses_Generated_Diff()
    {
        return Prop.ForAll(Diffs.ToArbitrary(), (diff) =>
        {
            // Act
            bool actual = GitDiffParser.TryParseUpdatedPackages(diff.ToString(), out var packages);

            // Assert
            actual.ShouldBeTrue();
            packages.ShouldNotBeNull();
            packages.Count.ShouldBe(diff.Updates.Count);

            foreach (var update in diff.Updates)
            {
                packages.ShouldContainKeyAndValue(update.Name, (update.ExpectedFrom, update.ExpectedTo));
            }
        });
    }

    [Property]
    public static Property TryParseUpdatedPackages_Is_Independent_Of_Line_Endings()
    {
        return Prop.ForAll(Diffs.ToArbitrary(), (diff) =>
        {
            // Arrange
            string lf = diff.ToString("\n");
            string crlf = diff.ToString("\r\n");
            string cr = diff.ToString("\r");

            // Act
            bool actualLf = GitDiffParser.TryParseUpdatedPackages(lf, out var packagesLf);
            bool actualCrlf = GitDiffParser.TryParseUpdatedPackages(crlf, out var packagesCrlf);
            bool actualCr = GitDiffParser.TryParseUpdatedPackages(cr, out var packagesCr);

            // Assert
            actualLf.ShouldBeTrue();
            actualCrlf.ShouldBe(actualLf);
            actualCr.ShouldBe(actualLf);

            packagesLf.ShouldNotBeNull();
            packagesCrlf.ShouldBe(packagesLf);
            packagesCr.ShouldBe(packagesLf);
        });
    }

    [Property]
    public static void TryParseUpdatedPackages_Does_Not_Throw_For_Arbitrary_Unicode_Input(UnicodeString diff)
    {
        // Act
        bool actual = GitDiffParser.TryParseUpdatedPackages(diff.Get, out var packages);

        // Assert
        AssertConsistent(actual, packages);
    }

    [Property]
    public static Property TryParseUpdatedPackages_Returns_False_When_Version_Is_Unchanged()
    {
        // A Docker image whose digest changes is a valid update even if the tag is the same
        var arbitrary =
            (from update in PackageUpdates
             select update with { HasDigest = false, Update = update.Update with { To = update.Update.From } }).ToArbitrary();

        return Prop.ForAll(arbitrary, (update) =>
        {
            // Arrange
            string diff = new Diff([update], "\n").ToString();

            // Act
            bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

            // Assert
            actual.ShouldBeFalse();
            packages.ShouldBeNull();
        });
    }

    [Property]
    public static Property TryParseUpdatedPackages_Returns_False_For_Stable_Downgrade()
    {
        var arbitrary =
            (from update in PackageUpdates
             where update.Update.From.Prerelease is null && update.Update.To.Prerelease is null
             select update with { Update = update.Update with { From = update.Update.To, To = update.Update.From } }).ToArbitrary();

        return Prop.ForAll(arbitrary, (update) =>
        {
            // Arrange
            string diff = new Diff([update], "\n").ToString();

            // Act
            bool actual = GitDiffParser.TryParseUpdatedPackages(diff, out var packages);

            // Assert
            actual.ShouldBeFalse();
            packages.ShouldBeNull();
        });
    }

    [Property]
    public static Property TryParseUpdatedPackages_Returns_False_Without_Hunk_Header()
    {
        return Prop.ForAll(Diffs.ToArbitrary(), (diff) =>
        {
            // Arrange
            string text = diff.ToString().Replace(HunkHeader, string.Empty, StringComparison.Ordinal);

            // Act
            bool actual = GitDiffParser.TryParseUpdatedPackages(text, out var packages);

            // Assert
            actual.ShouldBeFalse();
            packages.ShouldBeNull();
        });
    }

    private static void AssertConsistent(bool result, IDictionary<string, (string From, string To)>? packages)
    {
        if (result)
        {
            packages.ShouldNotBeNull();
            packages.ShouldNotBeEmpty();

            foreach ((string name, (string from, string to)) in packages)
            {
                name.ShouldNotBeNullOrEmpty();
                from.ShouldNotBeNullOrEmpty();
                to.ShouldNotBeNullOrEmpty();
            }
        }
        else
        {
            packages.ShouldBeNull();
        }
    }

    private static Gen<string> ManifestPath(Manifest manifest) => manifest switch
    {
        Manifest.NuGet => Gen.Elements("Directory.Packages.props", "src/App/App.csproj", "tests/App.Tests/App.Tests.csproj"),
        Manifest.Npm => Gen.Elements("package.json", "src/App/package.json"),
        Manifest.Dockerfile => Gen.Elements("Dockerfile", "src/App/Dockerfile", "docker/app.Dockerfile"),
        Manifest.DockerCompose => Gen.Elements("docker-compose.yml", "docker-compose.yaml", "src/docker-compose.yml"),
        Manifest.GoModule => Gen.Constant("go.mod"),
        _ => throw new ArgumentOutOfRangeException(nameof(manifest)),
    };

    private static Gen<string> PackageName(Manifest manifest) => manifest switch
    {
        Manifest.NuGet => NuGetPackageId,
        Manifest.Npm => NpmPackageName,
        Manifest.Dockerfile or Manifest.DockerCompose => DockerImageName,
        Manifest.GoModule => GoModulePath,
        _ => throw new ArgumentOutOfRangeException(nameof(manifest)),
    };

    private static string RenderDiff(string path, IEnumerable<(string Removed, string Added)> edits, string newLine)
    {
        var builder = new StringBuilder()
            .Append("diff --git a/").Append(path).Append(" b/").Append(path).Append(newLine)
            .Append("index 1234567..89abcde 100644").Append(newLine)
            .Append("--- a/").Append(path).Append(newLine)
            .Append("+++ b/").Append(path).Append(newLine);

        foreach ((string removed, string added) in edits)
        {
            builder
                .Append(HunkHeader).Append(newLine)
                .Append(" # context").Append(newLine)
                .Append('-').Append(removed).Append(newLine)
                .Append('+').Append(added).Append(newLine)
                .Append(" # context").Append(newLine);
        }

        return builder.ToString();
    }

    private sealed record PackageUpdate(Manifest Manifest, string Path, string Name, VersionUpdate Update, bool HasDigest)
    {
        private const string DigestFrom = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string DigestTo = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public static IEqualityComparer<PackageUpdate> NameComparer { get; } =
            EqualityComparer<PackageUpdate>.Create(
                (x, y) => string.Equals(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase),
                (p) => StringComparer.OrdinalIgnoreCase.GetHashCode(p.Name));

        public string ExpectedFrom => HasDigest ? $"{Update.From}-{DigestFrom}" : Update.From.ToString();

        public string ExpectedTo => HasDigest ? $"{Update.To}-{DigestTo}" : Update.To.ToString();

        public (string Removed, string Added) Render() =>
            (Render(Update.From.ToString(), HasDigest ? DigestFrom : null), Render(Update.To.ToString(), HasDigest ? DigestTo : null));

        private string Render(string version, string? digest) => Manifest switch
        {
            Manifest.NuGet => $"""    <PackageVersion Include="{Name}" Version="{version}" />""",
            Manifest.Npm => $"""    "{Name}": "^{version}",""",
            Manifest.Dockerfile => $"FROM --platform=$BUILDPLATFORM {Name}:{version}{(digest is null ? string.Empty : $"@sha256:{digest}")} AS build",
            Manifest.DockerCompose => $"    image: {Name}:{version}{(digest is null ? string.Empty : $"@sha256:{digest}")}",
            Manifest.GoModule => $"require {Name} v{version}",
            _ => throw new InvalidOperationException($"Unknown manifest {Manifest}."),
        };
    }

    private sealed record Diff(List<PackageUpdate> Updates, string NewLine)
    {
        public override string ToString() => ToString(NewLine);

        public string ToString(string newLine)
        {
            var builder = new StringBuilder();

            foreach (var group in Updates.GroupBy((p) => p.Path, StringComparer.Ordinal))
            {
                builder.Append(RenderDiff(group.Key, group.Select((p) => p.Render()), newLine));
            }

            return builder.ToString();
        }
    }
}
