// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using FsCheck;
using FsCheck.Fluent;

namespace MartinCostello.Costellobot;

/// <summary>
/// A class containing FsCheck generators for inputs that resemble the
/// dependency metadata the application parses, such as package names and versions.
/// </summary>
internal static class FuzzGenerators
{
    private const string Digits = "0123456789";
    private const string HexDigits = "0123456789abcdef";
    private const string LowerAlpha = "abcdefghijklmnopqrstuvwxyz";
    private const string UpperAlpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Alpha = LowerAlpha + UpperAlpha;

    public enum VersionBump
    {
        Major,
        Minor,
        Patch,
    }

    /// <summary>
    /// Gets a generator for a NuGet package ID, such as <c>Microsoft.Extensions.Logging</c>.
    /// </summary>
    public static Gen<string> NuGetPackageId { get; } =
        from count in Gen.Choose(1, 3)
        from segments in Segment(Alpha, Alpha + Digits + "_", 8).ArrayOf(count)
        select string.Join('.', segments);

    /// <summary>
    /// Gets a generator for an npm package name, such as <c>@types/node</c> or <c>webpack-cli</c>.
    /// </summary>
    public static Gen<string> NpmPackageName { get; } =
        from scoped in Gen.Elements(true, false)
        from scope in Segment(LowerAlpha, LowerAlpha + Digits + "-", 8)
        from name in Segment(LowerAlpha, LowerAlpha + Digits + "-.", 12)
        select scoped ? $"@{scope}/{name}" : name;

    /// <summary>
    /// Gets a generator for a Docker image name, such as <c>mcr.microsoft.com/dotnet/sdk</c>.
    /// </summary>
    public static Gen<string> DockerImageName { get; } =
        from registry in Gen.Elements(string.Empty, "mcr.microsoft.com/", "ghcr.io/", "docker.io/library/")
        from count in Gen.Choose(1, 2)
        from segments in Segment(LowerAlpha, LowerAlpha + Digits + "-", 10).ArrayOf(count)
        select registry + string.Join('/', segments);

    /// <summary>
    /// Gets a generator for a Go module path, such as <c>github.com/google/go-cmp</c>.
    /// </summary>
    public static Gen<string> GoModulePath { get; } =
        from host in Gen.Elements("github.com", "golang.org/x", "gopkg.in")
        from count in Gen.Choose(1, 2)
        from segments in Segment(LowerAlpha, LowerAlpha + Digits + "-", 10).ArrayOf(count)
        select $"{host}/{string.Join('/', segments)}";

    /// <summary>
    /// Gets a generator for a GitHub repository slug, such as <c>actions/checkout</c>.
    /// </summary>
    public static Gen<string> GitHubRepository { get; } =
        from owner in Segment(LowerAlpha, LowerAlpha + Digits + "-", 10)
        from name in Segment(LowerAlpha, LowerAlpha + Digits + "-.", 12)
        select $"{owner}/{name}";

    /// <summary>
    /// Gets a generator for a dependency name from any of the supported ecosystems.
    /// </summary>
    public static Gen<string> DependencyName { get; } =
        Gen.OneOf(NuGetPackageId, NpmPackageName, DockerImageName, GoModulePath, GitHubRepository);

    /// <summary>
    /// Gets a generator for a semantic version pre-release label.
    /// </summary>
    public static Gen<string?> PrereleaseLabel { get; } =
        Gen.Elements<string?>(null, null, "alpha", "beta.1", "preview.7.24406.2", "rc.2");

    /// <summary>
    /// Gets a generator for a semantic version.
    /// </summary>
    public static Gen<SemanticVersion> Versions { get; } =
        from major in Gen.Choose(0, 99)
        from minor in Gen.Choose(0, 99)
        from patch in Gen.Choose(0, 999)
        from prerelease in PrereleaseLabel
        select new SemanticVersion(major, minor, patch, prerelease);

    /// <summary>
    /// Gets a generator for a pair of semantic versions where the second is strictly greater than the first.
    /// </summary>
    public static Gen<VersionUpdate> Updates { get; } =
        from previous in Versions
        from bump in Gen.Elements(Enum.GetValues<VersionBump>())
        from prerelease in PrereleaseLabel
        select new VersionUpdate(previous, previous.Bump(bump, prerelease), bump);

    /// <summary>
    /// Gets a generator for a Git commit SHA.
    /// </summary>
    public static Gen<string> CommitSha { get; } = Characters(HexDigits, 40, 40);

    /// <summary>
    /// Gets a generator for an abbreviated Git commit SHA.
    /// </summary>
    public static Gen<string> ShortCommitSha { get; } = Characters(HexDigits, 7, 7);

    /// <summary>
    /// Gets a generator for the different line endings a Git diff or commit message may use.
    /// </summary>
    public static Gen<string> LineEnding { get; } = Gen.Elements("\n", "\r\n", "\r");

    /// <summary>
    /// Returns a generator for a string of the specified length drawn from the specified alphabet.
    /// </summary>
    public static Gen<string> Characters(string alphabet, int minLength, int maxLength) =>
        from length in Gen.Choose(minLength, maxLength)
        from characters in Gen.Elements(alphabet.ToCharArray()).ArrayOf(length)
        select new string(characters);

    /// <summary>
    /// Returns a generator for a non-empty list of up to <paramref name="maxCount"/> items
    /// that are distinct according to <paramref name="comparer"/>.
    /// </summary>
    public static Gen<List<T>> DistinctListOf<T>(Gen<T> generator, int maxCount, IEqualityComparer<T> comparer) =>
        from count in Gen.Choose(1, maxCount)
        from items in generator.ArrayOf(count)
        select items.Distinct(comparer).ToList();

    private static Gen<string> Segment(string first, string rest, int maxLength) =>
        from head in Gen.Elements(first.ToCharArray())
        from tail in Characters(rest, 0, maxLength - 1)
        select head + tail;

    public sealed record SemanticVersion(int Major, int Minor, int Patch, string? Prerelease)
    {
        public SemanticVersion Bump(VersionBump bump, string? prerelease) => bump switch
        {
            VersionBump.Major => new(Major + 1, 0, 0, prerelease),
            VersionBump.Minor => new(Major, Minor + 1, 0, prerelease),
            _ => new(Major, Minor, Patch + 1, prerelease),
        };

        public override string ToString() =>
            Prerelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Prerelease}";
    }

    public sealed record VersionUpdate(SemanticVersion From, SemanticVersion To, VersionBump Bump)
    {
        public string UpdateType => Bump switch
        {
            VersionBump.Major => "version-update:semver-major",
            VersionBump.Minor => "version-update:semver-minor",
            _ => "version-update:semver-patch",
        };
    }
}
