// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

internal static class DependencyHelpers
{
    private const string DefaultStyles = "fa-solid fa-cube";
    private const string GitHubStyles = "fa-brands fa-square-github text-dark";
    private const string NpmStyles = "fa-brands fa-npm text-danger";
    private const string NuGetStyles = "fa-solid fa-cube text-dotnet";

    public static (string Name, string Url, string CssClasses) GetPackageMetadata(DependencyEcosystem ecosystem, string id, string version)
    {
        return ecosystem switch
        {
            DependencyEcosystem.GitHubActions => ("GitHub Actions", GitHubUrl(id), GitHubStyles),
            DependencyEcosystem.Npm => ("npm", $"/package/{id}/v/{version}", NpmStyles),
            DependencyEcosystem.NuGet => ("NuGet", NuGetUrl($"/packages/{id}/{version}"), NuGetStyles),
            _ => (ecosystem.ToString(), string.Empty, DefaultStyles),
        };
    }

    public static (string Name, string Url, string CssClasses) GetPublisherMetadata(DependencyEcosystem ecosystem, string id)
    {
        return ecosystem switch
        {
            DependencyEcosystem.GitHubActions => ("GitHub Actions", GitHubUrl(id), GitHubStyles),
            DependencyEcosystem.GitSubmodule => ("Git Submodule", id, GitHubStyles),
            DependencyEcosystem.Npm => ("npm", NpmUrl($"~{id}"), NpmStyles),
            DependencyEcosystem.NuGet => ("NuGet", NuGetUrl($"/profiles/{id}"), NuGetStyles),
            _ => (ecosystem.ToString(), string.Empty, DefaultStyles),
        };
    }

    private static string GitHubUrl(string path)
        => BuildUrl("github.com", path);

    private static string NpmUrl(string path)
        => BuildUrl("www.npmjs.com", path);

    private static string NuGetUrl(string path)
        => BuildUrl("www.nuget.org", path);

    private static string BuildUrl(string hostname, string path)
    {
        return new UriBuilder()
        {
            Host = hostname,
            Path = path,
            Scheme = Uri.UriSchemeHttps,
        }.ToString();
    }
}
