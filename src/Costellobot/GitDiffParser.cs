// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NuGet.Versioning;

namespace MartinCostello.Costellobot;

public static partial class GitDiffParser
{
    private const char Added = '+';
    private const char Removed = '-';
    private const string HunkPrefix = "@@ ";
    private const string TargetPrefix = "+++ b/";

    private const string LatestTag = "latest";
    private static readonly Version LatestVersion = new(int.MaxValue, int.MaxValue, 0, 0);

    private static readonly XName Include = nameof(Include);
    private static readonly XName GlobalPackageReference = nameof(GlobalPackageReference);
    private static readonly XName PackageReference = nameof(PackageReference);
    private static readonly XName PackageVersion = nameof(PackageVersion);
    private static readonly XName Version = nameof(Version);

    private static readonly char[] NpmVersionPrefixes = ['^', '~'];

    private enum DiffLineType
    {
        None = 0,
        Added,
        Removed,
    }

    public static bool TryParseUpdatedPackages(
        string diff,
        [NotNullWhen(true)] out IDictionary<string, (string From, string To)>? packages)
    {
        packages = null;

        if (string.IsNullOrEmpty(diff))
        {
            return false;
        }

        var edits = GetEdits(diff);

        if (edits.Count < 1)
        {
            return false;
        }

        var packageUpdates = GetPackages(edits);

        if (packageUpdates.Count < 1)
        {
            return false;
        }

        packages = packageUpdates;
        return true;
    }

    private static Dictionary<string, (string From, string To)> GetPackages(
        List<(string Path, DiffLineType Type, string Package, NuGetVersion Version)> edits)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var added = new Dictionary<string, IList<NuGetVersion>>(comparer);
        var removed = new Dictionary<string, IList<NuGetVersion>>(comparer);

        foreach (var edit in edits)
        {
            Dictionary<string, IList<NuGetVersion>> target;

            if (edit.Type == DiffLineType.Added)
            {
                target = added;
            }
            else
            {
                Debug.Assert(edit.Type is DiffLineType.Removed, "Expected a DiffLineType of Removed.");
                target = removed;
            }

            if (!target.TryGetValue(edit.Package, out var versions))
            {
                target[edit.Package] = versions = [];
            }

            versions.Add(edit.Version);
        }

        var packages = new Dictionary<string, (string From, string To)>(comparer);

        foreach ((var package, var previous) in removed)
        {
            if (previous.Count < 1 ||
                !added.TryGetValue(package, out var updated) ||
                updated.Count < 1)
            {
                continue;
            }

            var minimum = previous.Min()!;
            var maximum = updated.Max()!;

            if (minimum < maximum ||
                (minimum.Version == maximum.Version && minimum.Release != maximum.Release))
            {
                // Special-case "latest" tags
                if (minimum.Version == LatestVersion && maximum.Version == LatestVersion)
                {
                    packages[package] = ($"{LatestTag}-{minimum.Release}", $"{LatestTag}-{maximum.Release}");
                }
                else
                {
                    packages[package] = (minimum.ToString(), maximum.ToString());
                }
            }
        }

        return packages;
    }

    private static List<(string Path, DiffLineType Type, string Package, NuGetVersion Version)> GetEdits(string diff)
    {
        // See https://stackoverflow.com/a/2530012/1064169
        bool inHunk = false;

        var edits = new List<(string Path, DiffLineType Type, string Package, NuGetVersion Version)>();

        string path = string.Empty;
        ReadOnlySpan<char> fileName = [];

        var remaining = diff.AsSpan();

        while (TryReadLine(ref remaining, out var line))
        {
            if (line.StartsWith(TargetPrefix, StringComparison.Ordinal))
            {
                path = line[TargetPrefix.Length..].ToString();
                fileName = Path.GetFileName(path.AsSpan());
            }
            else if (line.StartsWith(HunkPrefix, StringComparison.Ordinal))
            {
                inHunk = true;
            }
            else if (inHunk && line.Length > 0)
            {
                char prefix = line[0];

                if (prefix is Added or Removed)
                {
                    if (TryParseLine(fileName, line, out var type, out var package))
                    {
                        edits.Add((path, type, package.Value.Package, package.Value.Version));
                    }
                }
                else if (prefix is not ' ')
                {
                    inHunk = false;
                }
            }
        }

        return edits;
    }

    /// <summary>
    /// Reads the next line from the specified text using the same semantics as <see cref="TextReader.ReadLine()"/>.
    /// </summary>
    private static bool TryReadLine(ref ReadOnlySpan<char> remaining, out ReadOnlySpan<char> line)
    {
        if (remaining.IsEmpty)
        {
            line = default;
            return false;
        }

        int index = remaining.IndexOfAny('\r', '\n');

        if (index < 0)
        {
            line = remaining;
            remaining = default;
            return true;
        }

        line = remaining[..index];

        int length = 1;

        if (remaining[index] is '\r' && index + 1 < remaining.Length && remaining[index + 1] is '\n')
        {
            length = 2;
        }

        remaining = remaining[(index + length)..];
        return true;
    }

    private static bool TryParseLine(
        ReadOnlySpan<char> fileName,
        ReadOnlySpan<char> line,
        out DiffLineType lineType,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;

        lineType = line[0] switch
        {
            Added => DiffLineType.Added,
            Removed => DiffLineType.Removed,
            _ => DiffLineType.None,
        };

        if (lineType == DiffLineType.None)
        {
            return false;
        }

        var fragmentTrimmed = line[1..].Trim();

        if (fragmentTrimmed.StartsWith('<'))
        {
            // Only attempt to parse the XML if the fragment is for an element
            // with a name that is of interest, as parsing XML is expensive.
            return IsPackageElement(fragmentTrimmed) && TryParseXml(fragmentTrimmed, out package);
        }
        else if (fragmentTrimmed.StartsWith('"') && fileName.SequenceEqual("package.json"))
        {
            return TryParseJson(fragmentTrimmed, out package);
        }
        else if (IsDockerImage(fragmentTrimmed, fileName))
        {
            return TryParseDockerImage(fragmentTrimmed, out package);
        }
        else if (fileName.SequenceEqual("go.mod"))
        {
            return TryParseGoModule(fragmentTrimmed, out package);
        }

        return false;

        static bool IsPackageElement(ReadOnlySpan<char> fragment)
        {
            return
                fragment.StartsWith("<PackageReference", StringComparison.Ordinal) ||
                fragment.StartsWith("<PackageVersion", StringComparison.Ordinal) ||
                fragment.StartsWith("<GlobalPackageReference", StringComparison.Ordinal);
        }

        static bool IsDockerImage(ReadOnlySpan<char> fragment, ReadOnlySpan<char> fileName)
        {
            if (fragment.StartsWith("FROM", StringComparison.OrdinalIgnoreCase) &&
                fileName.Contains("dockerfile", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (fragment.StartsWith("image:", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileNameWithoutExtension(fileName).Equals("docker-compose", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }

    private static bool TryParseDockerImage(
        ReadOnlySpan<char> text,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;

        var match = DockerImage().Match(text.ToString());

        if (match.Success)
        {
            var image = match.Groups["image"].Value;
            var tag = match.Groups["tag"].Value;
            var digest = match.Groups["digest"].Value;

            if (!string.IsNullOrEmpty(image) && !string.IsNullOrEmpty(tag))
            {
                // Special-case "latest" tags
                if (tag is LatestTag)
                {
                    package = (image, new(LatestVersion, digest));
                    return true;
                }

                var versionString = tag;

                if (!string.IsNullOrEmpty(digest))
                {
                    versionString += $"-{digest}";
                }

                if (NuGetVersion.TryParse(versionString, out var version))
                {
                    package = (image, version);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseGoModule(
        ReadOnlySpan<char> text,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;

        // Only the first three parts are ever inspected, so any further
        // parts can be left unsplit in the final range of the buffer.
        Span<Range> parts = stackalloc Range[4];
        int count = text.Split(parts, ' ');

        if (count < 2)
        {
            return false;
        }

        var first = text[parts[0]];

        if (first is "go" or "module" or "toolchain")
        {
            return false;
        }

        int index = 0;

        if (first is "require")
        {
            if (count < 3)
            {
                return false;
            }

            index = 1;
        }

        var name = text[parts[index]];
        var versionString = text[parts[index + 1]];

        if (NuGetVersion.TryParse(versionString.TrimStart('v').ToString(), out var version))
        {
            package = (name.ToString(), version);
            return true;
        }

        return false;
    }

    private static bool TryParseJson(
        ReadOnlySpan<char> text,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;
        text = text.TrimEnd(',');

        try
        {
            // This will return false positives if a non-version property is changed
            // if it happens to parse to a NuGet version string, but within the bounds
            // of what is actually being used in the application, this is fine.
            using var element = JsonDocument.Parse(string.Concat("{", text, "}"));
            using var enumerator = element.RootElement.EnumerateObject();

            if (enumerator.MoveNext())
            {
                var property = enumerator.Current;

                if (property.Value.ValueKind is JsonValueKind.String &&
                    property.Value.GetString() is { Length: > 0 } versionString)
                {
                    versionString = versionString.TrimStart(NpmVersionPrefixes);

                    if (NuGetVersion.TryParse(versionString, out var version))
                    {
                        package = (property.Name, version);
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parsing errors
        }

        return false;
    }

    private static bool TryParseXml(
        ReadOnlySpan<char> text,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;

        XElement fragment;

        try
        {
            fragment = XElement.Parse(text.ToString());

            if ((fragment.Name == PackageReference ||
                 fragment.Name == PackageVersion ||
                 fragment.Name == GlobalPackageReference) &&
                 TryParsePackage(fragment, out var name, out var version))
            {
                package = (name, version);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;

        static bool TryParsePackage(
            XElement element,
            [NotNullWhen(true)] out string? name,
            [NotNullWhen(true)] out NuGetVersion? version)
        {
            name = null;
            version = null;

            if (element.Attribute(Include) is not { } include ||
                include.Value is not { Length: > 0 } packageId)
            {
                return false;
            }

            if (element.Attribute(Version) is not { } attribute ||
                attribute.Value is not { Length: > 0 } versionString)
            {
                return false;
            }

            name = packageId;
            return NuGetVersion.TryParse(versionString, out version);
        }
    }

    [GeneratedRegex(@"^(?i)(FROM|IMAGE:)(?-i) ((?<platform>--platform=[\$\w]+)\s)?(?<image>[\w\.\/\-]+)(:(?<tag>[\w\-\.]+)(@sha256:(?<digest>[0-9a-f{64}]+))?)?(\s(?<name>(?i)AS(?-i) [\S]+))?$")]
    private static partial Regex DockerImage();
}
