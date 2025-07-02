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
                packages[package] = (minimum.ToString(), maximum.ToString());
            }
        }

        return packages;
    }

    private static List<(string Path, DiffLineType Type, string Package, NuGetVersion Version)> GetEdits(string diff)
    {
        // See https://stackoverflow.com/a/2530012/1064169
        using var reader = new StringReader(diff);
        string? line;

        bool inHunk = false;

        var edits = new List<(string Path, DiffLineType Type, string Package, NuGetVersion Version)>();
        string path = string.Empty;

        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith(TargetPrefix, StringComparison.Ordinal))
            {
                path = line[TargetPrefix.Length..];
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
                    if (TryParseLine(path, line, out var type, out var package))
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

    private static bool TryParseLine(
        string path,
        string line,
        out DiffLineType lineType,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        lineType = DiffLineType.None;
        package = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

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

        var fragmentText = line[1..];
        var fragmentTrimmed = fragmentText.Trim();
        var fileName = Path.GetFileName(path);

        if (fragmentTrimmed.StartsWith('<'))
        {
            return TryParseXml(fragmentTrimmed, out package);
        }
        else if (fragmentTrimmed.StartsWith('"') && fileName is "package.json")
        {
            return TryParseJson(fragmentTrimmed, out package);
        }
        else if (fragmentTrimmed.StartsWith("FROM", StringComparison.OrdinalIgnoreCase) &&
                 fileName?.Contains("dockerfile", StringComparison.OrdinalIgnoreCase) is true)
        {
            return TryParseDockerfile(fragmentTrimmed, out package);
        }

        return false;
    }

    private static bool TryParseDockerfile(
        string text,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;

        var match = DockerImage().Match(text);

        if (match.Success)
        {
            var image = match.Groups["image"].Value;
            var tag = match.Groups["tag"].Value;
            var digest = match.Groups["digest"].Value;

            if (!string.IsNullOrEmpty(image) && !string.IsNullOrEmpty(tag))
            {
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

        // TODO
        return false;
    }

    private static bool TryParseJson(
        string text,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;
        text = text.TrimEnd(',');

        try
        {
            // This will return false positives if a non-version property is changed
            // if it happens to parse to a NuGet version string, but within the bounds
            // of what is actually being used in the application, this is fine.
            using var element = JsonDocument.Parse('{' + text + '}');
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
        string text,
        [NotNullWhen(true)] out (string Package, NuGetVersion Version)? package)
    {
        package = null;

        XElement fragment;

        try
        {
            fragment = XElement.Parse(text);

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

    [GeneratedRegex(@"^(?i)FROM(?-i) ((?<platform>--platform=[\$\w]+)\s)?(?<image>[\w\.\/\-]+)(:(?<tag>[\w\-\.]+)(@sha256:(?<digest>[0-9a-f{64}]+))?)?(\s(?<name>(?i)AS(?-i) [\S]+))?$")]
    private static partial Regex DockerImage();
}
