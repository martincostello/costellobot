// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot;

[Collection(AppCollection.Name)]
public class GitCommitAnalyzerTests : IntegrationTests<AppFixture>
{
    public GitCommitAnalyzerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Fact]
    public static void Version_Is_Extracted_From_NuGet_Package_Update_Commit_Message()
    {
        string commitMessage = @"Bump AWSSDK.S3 from 3.7.9.32 to 3.7.9.33
Bumps [AWSSDK.S3](https://github.com/aws/aws-sdk-net) from 3.7.9.32 to 3.7.9.33.
- [Release notes](https://github.com/aws/aws-sdk-net/releases)
- [Commits](https://github.com/aws/aws-sdk-net/commits)

---
updated-dependencies:
- dependency-name: AWSSDK.S3
  dependency-type: direct:production
  update-type: version-update:semver-patch
...

Signed-off-by: dependabot[bot] <support@github.com>";

        // Act
        bool result = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, out var actual);

        // Assert
        result.ShouldBeTrue();
        actual.ShouldBe("3.7.9.33");
    }

    [Fact]
    public static void Version_Is_Extracted_From_Submodule_Update_Commit_Message()
    {
        string commitMessage = @"Bump src/submodules/dependabot-helper from 697aaa7 to aca93c2
Bumps [src/submodules/dependabot-helper](https://github.com/martincostello/dependabot-helper) from `697aaa7` to `aca93c2`.
- [Release notes](https://github.com/martincostello/dependabot-helper/releases)
- [Commits](https://github.com/martincostello/dependabot-helper/compare/697aaa778e5e0a27c7ba6a2c82f83cd5ddf9ae55...aca93c280ec51a3e7ffd9314de799d805cf7a407)

---
updated-dependencies:
- dependency-name: src/submodules/dependabot-helper
  dependency-type: direct:production
...

Signed-off-by: dependabot[bot] <support@github.com>";

        // Act
        bool result = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, out var actual);

        // Assert
        result.ShouldBeTrue();
        actual.ShouldBe("aca93c2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Do some stuff")]
    [InlineData("Bump src/submodules/dependabot-helper to aca93c2")]
    public static void No_Version_Is_Extracted_From_Arbitrary_Commit_Message(string commitMessage)
    {
        // Act
        bool result = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, out var actual);

        // Assert
        result.ShouldBeFalse();
        actual.ShouldBeNull();
    }

    [Theory]
    [InlineData("@actions/github", "dependabot/npm_and_yarn/actions/github-5.0.3", true)]
    [InlineData("actions/checkout", "dependabot/github_actions/actions/checkout-3", true)]
    [InlineData("JustEat.HttpClientInterception", "dependabot/nuget/JustEat.HttpClientInterception-3.1.1", true)]
    [InlineData("martincostello/update-dotnet-sdk", "dependabot/github_actions/martincostello/update-dotnet-sdk-2", true)]
    [InlineData("Microsoft.NET.Sdk", "update-dotnet-sdk-6.0.302", true)]
    [InlineData("NodaTime", "dependabot/nuget/NodaTimeVersion-3.1.0", true)]
    [InlineData("NodaTimee", "dependabot/nuget/NodaTimee-3.1.1", false)]
    [InlineData("NodaTime.Testing", "dependabot/nuget/NodaTimeVersion-3.1.0", true)]
    [InlineData("NodaTime.Testingg", "dependabot/nuget/NodaTime.Testingg-3.1.1", false)]
    [InlineData("typescript-hax", "dependabot/npm_and_yarn/typescript-hax-1.0.0", false)]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Dependencies(
        string dependency,
        string reference,
        bool expected)
    {
        // Arrange
        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Dependencies = new[]
                {
                    @"^@actions\/.*$",
                    @"^@microsoft\/signalr$",
                    @"^@octokit\/types$",
                    @"^@types\/.*$",
                    @"^actions\/.*$",
                    @"^Amazon.Lambda\\..*$",
                    @"^AspNet.Security.OAuth\\..*$",
                    @"^AWSSDK\\..*$",
                    @"^Azure.Extensions\\..*$",
                    @"^Azure.Identity$",
                    @"^BenchmarkDotNet$",
                    @"^JustEat.HttpClientInterception$",
                    @"^MartinCostello\\..*$",
                    @"^martincostello\/update-dotnet-sdk$",
                    @"^Microsoft.ApplicationInsights\\..*$",
                    @"^Microsoft.AspNetCore\\..*$",
                    @"^Microsoft.Azure\\..*$",
                    @"^Microsoft.EntityFrameworkCore\\..*$",
                    @"^Microsoft.Extensions\\..*$",
                    @"^Microsoft.IdentityModel\\..*$",
                    @"^Microsoft.NET.Sdk$",
                    @"^Microsoft.NET.Test.Sdk$",
                    @"^Microsoft.Playwright$",
                    @"^Microsoft.SourceLink.GitHub$",
                    @"^Microsoft.TypeScript.MSBuild$",
                    @"^Newtonsoft.Json$",
                    @"^Octokit$",
                    @"^Octokit.GraphQL$",
                    @"^Octokit.Webhooks.AspNetCore$",
                    @"^NodaTime$",
                    @"^NodaTime.Testing$",
                    @"^System.Text.Json$",
                    @"^typescript$",
                    @"^xunit$",
                    @"^xunit.runner.visualstudio$",
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options);

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage(dependency);

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            owner.Login,
            repo.Name,
            reference,
            sha,
            commitMessage);

        // Assert
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData("@actions/github", "dependabot/npm_and_yarn/actions/github-5.0.3", "5.0.3", DependencyEcosystem.Npm, new[] { "thboop" }, false)]
    [InlineData("actions/checkout", "dependabot/github_actions/actions/checkout-3", "3", DependencyEcosystem.GitHubActions, new[] { "actions" }, true)]
    [InlineData("JustEat.HttpClientInterception", "dependabot/nuget/JustEat.HttpClientInterception-3.1.1", "3.1.1", DependencyEcosystem.NuGet, new[] { "JUSTEAT_OSS" }, false)]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", "dependabot/nuget/Microsoft.EntityFrameworkCore.SqlServer-7.0.0-preview.6.22329.4", "7.0.0-preview.6.22329.4", DependencyEcosystem.NuGet, new[] { "aspnet", "EntityFramework", "Microsoft" }, true)]
    [InlineData("Microsoft.NET.Sdk", "update-dotnet-sdk-6.0.302", "6.0.302", DependencyEcosystem.Unknown, new string[0], false)]
    [InlineData("NodaTime", "dependabot/nuget/NodaTimeVersion-3.1.0", "3.1.0", DependencyEcosystem.NuGet, new[] { "NodaTime" }, false)]
    [InlineData("python-dotenv", "dependabot/pip/python-dotenv-0.17.1", "0.17.1", DependencyEcosystem.Unsupported, new string[0], false)]
    [InlineData("src/submodules/dependabot-helper", "dependabot/submodules/src/submodules/dependabot-helper-697aaa7", "697aaa7", DependencyEcosystem.Submodules, new[] { "https://github.com/martincostello" }, true)]
    [InlineData("typescript", "dependabot/npm_and_yarn/typescript-5.0.1", "5.0.1", DependencyEcosystem.Npm, new[] { "typescript-bot" }, true)]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publishers(
        string dependency,
        string reference,
        string version,
        DependencyEcosystem ecosystem,
        string[] owners,
        bool expected)
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var mock = new Mock<IPackageRegistry>();

        mock.Setup((p) => p.Ecosystem)
            .Returns(ecosystem);

        mock.Setup((p) => p.GetPackageOwnersAsync(owner.Login, repo.Name, dependency, version))
            .ReturnsAsync(owners);

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.GitHubActions] = new[] { "actions" },
                    [DependencyEcosystem.Npm] = new[] { "types", "typescript-bot" },
                    [DependencyEcosystem.NuGet] = new[] { "aspnet", "Microsoft" },
                    [DependencyEcosystem.Submodules] = new[] { "https://github.com/martincostello" },
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, new[] { mock.Object });

        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage(dependency, version);

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            owner.Login,
            repo.Name,
            reference,
            sha,
            commitMessage);

        // Assert
        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Untrusted_Publisher()
    {
        // Arrange
        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.GitHubActions] = new[] { "actions" },
                    [DependencyEcosystem.Npm] = new[] { "types", "typescript-bot" },
                    [DependencyEcosystem.NuGet] = new[] { "aspnet", "Microsoft" },
                    [DependencyEcosystem.Submodules] = new[] { "https://github.com/martincostello" },
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, new[] { Mock.Of<IPackageRegistry>() });

        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = @"Bump Foo to 1.0.1
Bumps `Foo` to 1.0.1.

---
updated-dependencies:
- dependency-name: Foo
  dependency-type: direct:production
  update-type: version-update:semver-patch
...

Signed-off-by: dependabot[bot] <support@github.com>";

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            "someone",
            "something",
            "blah-blah",
            sha,
            commitMessage);

        // Assert
        actual.ShouldBeFalse();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_If_No_Trusted_Publishers()
    {
        // Arrange
        var options = new WebhookOptions()
        {
            TrustedEntities = new(),
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, new[] { Mock.Of<IPackageRegistry>() });

        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage();

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            "someone",
            "something",
            "dependabot/nuget/NodaTimeVersion-3.1.0",
            sha,
            commitMessage);

        // Assert
        actual.ShouldBeFalse();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_If_No_Trusted_Publishers_For_Ecosystem()
    {
        // Arrange
        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = Array.Empty<string>(),
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, new[] { Mock.Of<IPackageRegistry>() });

        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage();

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            "someone",
            "something",
            "dependabot/nuget/NodaTimeVersion-3.1.0",
            sha,
            commitMessage);

        // Assert
        actual.ShouldBeFalse();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_If_Package_Registry_Lookup_Fails()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var mock = new Mock<IPackageRegistry>();

        mock.Setup((p) => p.Ecosystem)
            .Returns(DependencyEcosystem.GitHubActions);

        mock.Setup((p) => p.GetPackageOwnersAsync(owner.Login, repo.Name, "actions/checkout", "3"))
            .ThrowsAsync(new InvalidOperationException());

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.GitHubActions] = new[] { "actions" },
                    [DependencyEcosystem.Npm] = new[] { "types", "typescript-bot" },
                    [DependencyEcosystem.NuGet] = new[] { "aspnet", "Microsoft" },
                    [DependencyEcosystem.Submodules] = new[] { "https://github.com/martincostello" },
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, new[] { mock.Object });

        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage("actions/checkout", "3");

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            owner.Login,
            repo.Name,
            "dependabot/github_actions/actions/checkout-3",
            sha,
            commitMessage);

        // Assert
        actual.ShouldBeFalse();
    }

    private static GitCommitAnalyzer CreateTarget(
        IServiceProvider serviceProvider,
        WebhookOptions? options = null,
        IEnumerable<IPackageRegistry>? registries = null)
    {
        registries ??= serviceProvider.GetServices<IPackageRegistry>();
        var optionsMonitor = options?.ToMonitor() ?? serviceProvider.GetRequiredService<IOptionsMonitor<WebhookOptions>>();
        var logger = serviceProvider.GetRequiredService<ILogger<GitCommitAnalyzer>>();

        return new(registries, optionsMonitor, logger);
    }
}
