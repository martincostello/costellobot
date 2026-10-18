// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using static MartinCostello.Costellobot.FuzzGenerators;

namespace MartinCostello.Costellobot;

public static class GitCommitAnalyzerFuzzTests
{
    private static readonly Gen<string> Verbs = Gen.Elements("Bumps", "Updates");

    private static readonly Gen<string> UpdateTypes = Gen.Elements(
        "version-update:semver-major",
        "version-update:semver-minor",
        "version-update:semver-patch");

    private static readonly Gen<DependabotDependency> DependabotDependencies =
        from name in DependencyName
        from version in Gen.OneOf(Gen.Constant<string?>(null), Versions.Select((p) => (string?)p.ToString()), Gen.Constant<string?>("latest"))
        from updateType in UpdateTypes
        select new DependabotDependency(name, version, updateType);

    private static readonly Gen<RenovateDependency> RenovateDependencies =
        from datasource in Gen.Elements("nuget", "npm", "docker", "github-releases", "dotnet-version")
        from name in DependencyName
        from update in Updates
        from prefix in Gen.Elements(string.Empty, "v")
        select new RenovateDependency(datasource, name, update, prefix);

    private static readonly Gen<string> CommitMessageLineEnding = Gen.Elements("\n", "\r\n");

    private enum MessageFormat
    {
        Plain,
        Escaped,
        Linked,
        Grouped,
    }

    [Property]
    public static void TryParseVersionNumber_Does_Not_Throw_For_Arbitrary_Input(NonNull<string> commitMessage, NonNull<string> dependencyName)
    {
        // Act
        bool actual = GitCommitAnalyzer.TryParseVersionNumber(commitMessage.Get, dependencyName.Get, out var version);

        // Assert
        if (actual)
        {
            version.ShouldNotBeNull();
        }
        else
        {
            version.ShouldBeNull();
        }
    }

    [Property]
    public static Property TryParseVersionNumber_Extracts_Version_From_Dependabot_Commit_Message()
    {
        var arbitrary =
            (from verb in Verbs
             from name in DependencyName
             from update in Updates
             from format in Gen.Elements(Enum.GetValues<MessageFormat>())
             from newLine in CommitMessageLineEnding
             select (Verb: verb, Name: name, Update: update, Format: format, NewLine: newLine)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            string from = input.Update.From.ToString();
            string to = input.Update.To.ToString();
            string commitMessage = RenderCommitMessage(input.Verb, input.Name, from, to, input.Format, input.NewLine);

            // Act
            bool actual = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, input.Name, out var version);

            // Assert
            actual.ShouldBeTrue();
            version.ShouldBe(to);
        });
    }

    [Property]
    public static Property TryParseVersionNumber_Extracts_Sha_From_Submodule_Commit_Message()
    {
        var arbitrary =
            (from verb in Verbs
             from name in GitHubRepository
             from previous in ShortCommitSha
             from current in ShortCommitSha
             from withLink in Gen.Elements(true, false)
             from newLine in CommitMessageLineEnding
             select (Verb: verb, Name: name, From: previous, To: current, WithLink: withLink, NewLine: newLine)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            string path = $"src/submodules/{input.Name}";
            string url = $"https://github.com/{input.Name}";

            string subject = input.WithLink
                ? $"{input.Verb} [{path}]({url}) from `{input.From}` to `{input.To}`."
                : $"{input.Verb} {path} from `{input.From}` to `{input.To}`.";

            string commitMessage = string.Join(
                input.NewLine,
                $"Bump {path} from {input.From} to {input.To}",
                subject,
                $"- [Release notes]({url}/releases)",
                $"- [Commits]({url}/compare/{input.From}...{input.To})",
                string.Empty,
                "Signed-off-by: dependabot[bot] <support@github.com>");

            // Act
            bool actual = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, path, out var version);

            // Assert
            actual.ShouldBeTrue();
            version.ShouldBe(input.To);
        });
    }

    [Property]
    public static Property TryParseVersionNumber_Returns_False_When_Dependency_Is_Not_Mentioned()
    {
        var arbitrary =
            (from verb in Verbs
             from name in DependencyName
             from other in DependencyName
             where !name.Contains(other, StringComparison.Ordinal)
             from update in Updates
             from format in Gen.Elements(Enum.GetValues<MessageFormat>())
             select (Verb: verb, Name: name, Other: other, Update: update, Format: format)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            string commitMessage = RenderCommitMessage(
                input.Verb,
                input.Name,
                input.Update.From.ToString(),
                input.Update.To.ToString(),
                input.Format,
                "\n");

            // Act
            bool actual = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, input.Other, out var version);

            // Assert
            actual.ShouldBeFalse();
            version.ShouldBeNull();
        });
    }

    [Property]
    public static void ParseDependencies_Does_Not_Throw_For_Arbitrary_Input(NonNull<string> commitMessage)
    {
        // Act
        var actual = GitCommitAnalyzer.ParseDependencies(commitMessage.Get);

        // Assert
        actual.ShouldNotBeNull();

        foreach (var dependency in actual)
        {
            dependency.Name.ShouldNotBeNullOrEmpty();
        }
    }

    [Property]
    public static Property ParseDependencies_Does_Not_Throw_For_Arbitrary_Dependabot_Metadata()
    {
        var arbitrary =
            (from yaml in ArbMap.Default.GeneratorFor<string>()
             from newLine in LineEnding
             select (Yaml: yaml, NewLine: newLine)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            string commitMessage = string.Join(
                input.NewLine,
                "Bump the all group with 1 update",
                string.Empty,
                "---",
                input.Yaml,
                "...",
                string.Empty,
                "Signed-off-by: dependabot[bot] <support@github.com>");

            // Act
            var actual = GitCommitAnalyzer.ParseDependencies(commitMessage);

            // Assert
            actual.ShouldNotBeNull();
        });
    }

    [Property]
    public static Property ParseDependencies_Does_Not_Throw_For_Arbitrary_Renovate_Metadata()
    {
        var arbitrary =
            (from cells in ArbMap.Default.GeneratorFor<string>().ArrayOf(4)
             from newLine in LineEnding
             select (Cells: cells, NewLine: newLine)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            string commitMessage = string.Join(
                input.NewLine,
                "Update dependencies",
                "| datasource | package | from | to |",
                "| ---------- | ------- | ---- | -- |",
                $"| {string.Join(" | ", input.Cells)} |",
                string.Empty,
                "Signed-off-by: renovate[bot] <29139614+renovate[bot]@users.noreply.github.com>");

            // Act
            var actual = GitCommitAnalyzer.ParseDependencies(commitMessage);

            // Assert
            actual.ShouldNotBeNull();
        });
    }

    [Property]
    public static Property ParseDependencies_Parses_Generated_Dependabot_Metadata()
    {
        var arbitrary =
            (from dependencies in DistinctListOf(DependabotDependencies, 4, DependabotDependency.NameComparer)
             from newLine in LineEnding
             select (Dependencies: dependencies, NewLine: newLine)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            var lines = new List<string>()
            {
                $"Bump the all group with {input.Dependencies.Count} updates",
                string.Empty,
                "---",
                "updated-dependencies:",
            };

            foreach (var dependency in input.Dependencies)
            {
                lines.Add($"- dependency-name: {Quote(dependency.Name)}");

                if (dependency.Version is { } version)
                {
                    lines.Add($"  dependency-version: {Quote(version)}");
                }

                lines.Add("  dependency-type: direct:production");
                lines.Add($"  update-type: {dependency.UpdateType}");
                lines.Add("  dependency-group: all");
            }

            lines.Add("...");
            lines.Add(string.Empty);
            lines.Add("Signed-off-by: dependabot[bot] <support@github.com>");

            string commitMessage = string.Join(input.NewLine, lines);

            // Act
            var actual = GitCommitAnalyzer.ParseDependencies(commitMessage);

            // Assert
            actual.ShouldBe(
                input.Dependencies.Select((p) => (p.Name, p.Version, (string?)p.UpdateType, (string?)null)),
                ignoreOrder: true);
        });
    }

    [Property]
    public static Property ParseDependencies_Parses_Generated_Renovate_Metadata()
    {
        var arbitrary =
            (from dependencies in DistinctListOf(RenovateDependencies, 4, RenovateDependency.NameComparer)
             from padding in Gen.Elements(string.Empty, " ", "   ")
             from newLine in LineEnding
             select (Dependencies: dependencies, Padding: padding, NewLine: newLine)).ToArbitrary();

        return Prop.ForAll(arbitrary, (input) =>
        {
            // Arrange
            string Row(params string[] cells)
                => $"|{string.Join('|', cells.Select((p) => input.Padding + p + input.Padding))}|";

            var lines = new List<string>()
            {
                "Update dependencies",
                Row("datasource", "package", "from", "to"),
                Row("----------", "-------", "----", "--"),
            };

            foreach (var dependency in input.Dependencies)
            {
                lines.Add(Row(dependency.Datasource, dependency.Name, $"{dependency.Prefix}{dependency.Update.From}", $"{dependency.Prefix}{dependency.Update.To}"));
            }

            lines.Add(string.Empty);
            lines.Add(string.Empty);
            lines.Add("Signed-off-by: renovate[bot] <29139614+renovate[bot]@users.noreply.github.com>");

            string commitMessage = string.Join(input.NewLine, lines);

            // Act
            var actual = GitCommitAnalyzer.ParseDependencies(commitMessage);

            // Assert
            actual.ShouldBe(
                input.Dependencies.Select((p) => (p.Name, (string?)p.Update.To.ToString(), (string?)p.Update.UpdateType, (string?)p.Update.From.ToString())),
                ignoreOrder: true);
        });
    }

    private static string RenderCommitMessage(
        string verb,
        string name,
        string from,
        string to,
        MessageFormat format,
        string newLine)
    {
        string url = "https://github.com/owner/repo";

        string subject = format switch
        {
            MessageFormat.Plain => $"{verb} {name} from {from} to {to}.",
            MessageFormat.Escaped => $"{verb} `{name}` from {from} to {to}",
            MessageFormat.Linked => $"{verb} [{name}]({url}) from {from} to {to}.",
            MessageFormat.Grouped => $"{verb} the all group with 1 update: [{name}]({url}).",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        string[] details = format is MessageFormat.Grouped
            ? [string.Empty, $"- [Commits]({url}/compare/{from}...{to})"]
            : [$"- [Release notes]({url}/releases)", $"- [Commits]({url}/compare/{from}...{to})"];

        return string.Join(
            newLine,
            [
                $"Bump {name} from {from} to {to}",
                subject,
                .. details,
                string.Empty,
                "Signed-off-by: dependabot[bot] <support@github.com>",
            ]);
    }

    private static string Quote(string value)
        => value is ['@', ..] ? $"\"{value}\"" : value;

    private sealed record DependabotDependency(string Name, string? Version, string UpdateType)
    {
        public static IEqualityComparer<DependabotDependency> NameComparer { get; } =
            EqualityComparer<DependabotDependency>.Create(
                (x, y) => string.Equals(x?.Name, y?.Name, StringComparison.Ordinal),
                (p) => StringComparer.Ordinal.GetHashCode(p.Name));
    }

    private sealed record RenovateDependency(string Datasource, string Name, VersionUpdate Update, string Prefix)
    {
        public static IEqualityComparer<RenovateDependency> NameComparer { get; } =
            EqualityComparer<RenovateDependency>.Create(
                (x, y) => string.Equals(x?.Name, y?.Name, StringComparison.Ordinal),
                (p) => StringComparer.Ordinal.GetHashCode(p.Name));
    }
}
