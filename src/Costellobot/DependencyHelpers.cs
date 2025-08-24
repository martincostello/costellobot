// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

internal static class DependencyHelpers
{
    private const string DefaultStyles = "fa-solid fa-cube";
    private const string DockerStyles = "fa-brands fa-docker text-primary";
    private const string GitHubStyles = "fa-brands fa-square-github text-dark";
    private const string MicrosoftStyles = "fa-brands fa-microsoft text-primary";
    private const string NpmStyles = "fa-brands fa-npm text-danger";
    private const string NuGetStyles = "fa-solid fa-cube text-dotnet"; // Maybe one day... https://bsky.app/profile/martincostello.com/post/3lrkz62rwvs2a
    private const string PythonStyles = "fa-brands fa-python text-primary";
    private const string RubyGemStyles = "fa-solid fa-gem text-danger";

    public static (string Name, string Url, string CssClasses) GetPackageMetadata(DependencyEcosystem ecosystem, string id, string version)
    {
        return ecosystem switch
        {
            DependencyEcosystem.Docker when id.StartsWith("dotnet/", StringComparison.Ordinal) => ("Docker", MicrosoftArtifactRegistryUrl($"/artifact/mar/{id}/tags"), MicrosoftStyles),
            DependencyEcosystem.Docker => ("Docker", DockerHubUrl($"/r/{id}/tags"), DockerStyles),
            DependencyEcosystem.GitHubActions => ("GitHub Actions", GitHubUrl(id), GitHubStyles),
            DependencyEcosystem.GitHubRelease => ("GitHub", GitHubUrl(id), GitHubStyles),
            DependencyEcosystem.Npm => ("npm", NpmUrl($"/package/{id}/v/{version}"), NpmStyles),
            DependencyEcosystem.NuGet => ("NuGet", NuGetUrl($"/packages/{id}/{version}"), NuGetStyles),
            DependencyEcosystem.Pip => ("PyPI", PyPIUrl($"/project/{id}/{version}/"), PythonStyles),
            DependencyEcosystem.Ruby => ("Ruby", RubyGemUrl($"/gems/{id}/versions/{version}"), RubyGemStyles),
            _ => (ecosystem.ToString(), string.Empty, DefaultStyles),
        };
    }

    public static (string Name, string Url, string CssClasses) GetPublisherMetadata(DependencyEcosystem ecosystem, string id)
    {
        return ecosystem switch
        {
            DependencyEcosystem.Docker when id is Registries.DockerPackageRegistry.MicrosoftArtifactRegistry => ("Docker", MicrosoftArtifactRegistryUrl(string.Empty), MicrosoftStyles),
            DependencyEcosystem.Docker => ("Docker", DockerHubUrl($"/u/{id}"), DockerStyles),
            DependencyEcosystem.GitHubActions => ("GitHub Actions", GitHubUrl(id), GitHubStyles),
            DependencyEcosystem.GitHubRelease => ("GitHub", GitHubUrl(id), GitHubStyles),
            DependencyEcosystem.GitSubmodule => ("Git Submodule", id, GitHubStyles),
            DependencyEcosystem.Npm => ("npm", NpmUrl($"~{id}"), NpmStyles),
            DependencyEcosystem.NuGet => ("NuGet", NuGetUrl($"/profiles/{id}"), NuGetStyles),
            DependencyEcosystem.Pip => ("PyPI", PyPIUrl($"/user/{id}/"), PythonStyles),
            DependencyEcosystem.Ruby => ("Ruby", RubyGemUrl($"/profiles/{id}"), RubyGemStyles),
            _ => (ecosystem.ToString(), string.Empty, DefaultStyles),
        };
    }

    private static string DockerHubUrl(string path)
        => BuildUrl("hub.docker.com", path);

    private static string GitHubUrl(string path)
        => BuildUrl("github.com", path);

    private static string MicrosoftArtifactRegistryUrl(string path)
        => BuildUrl("mcr.microsoft.com", path);

    private static string NpmUrl(string path)
        => BuildUrl("www.npmjs.com", path);

    private static string NuGetUrl(string path)
        => BuildUrl("www.nuget.org", path);

    private static string PyPIUrl(string path)
        => BuildUrl("pypi.org", path);

    private static string RubyGemUrl(string path)
        => BuildUrl("rubygems.org", path);

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
