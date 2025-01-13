// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Registries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Octokit;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot;

[Collection<AppCollection>]
public class GitCommitAnalyzerTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Fact]
    public static void Version_Is_Extracted_From_NuGet_Package_Update_Commit_Message()
    {
        string dependencyName = "AWSSDK.S3";
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
        bool result = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, dependencyName, out var actual);

        // Assert
        result.ShouldBeTrue();
        actual.ShouldBe("3.7.9.33");
    }

    [Fact]
    public static void Version_Is_Extracted_From_Submodule_Update_Commit_Message()
    {
        string dependencyName = "src/submodules/dependabot-helper";
        string commitMessage = @"""
                               Bump src/submodules/dependabot-helper from 697aaa7 to aca93c2
                               Bumps [src/submodules/dependabot-helper](https://github.com/martincostello/dependabot-helper) from `697aaa7` to `aca93c2`.
                               - [Release notes](https://github.com/martincostello/dependabot-helper/releases)
                               - [Commits](https://github.com/martincostello/dependabot-helper/compare/697aaa778e5e0a27c7ba6a2c82f83cd5ddf9ae55...aca93c280ec51a3e7ffd9314de799d805cf7a407)

                               ---
                               updated-dependencies:
                               - dependency-name: src/submodules/dependabot-helper
                                 dependency-type: direct:production
                               ...

                               Signed-off-by: dependabot[bot] <support@github.com>
                               """;

        // Act
        bool result = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, dependencyName, out var actual);

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
        bool result = GitCommitAnalyzer.TryParseVersionNumber(commitMessage, "whatever", out var actual);

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
                Dependencies =
                [
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
                ],
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options);

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var diff = string.Empty;
        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage(dependency);

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            reference,
            sha,
            commitMessage,
            diff);

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
    [InlineData("src/submodules/dependabot-helper", "dependabot/submodules/src/submodules/dependabot-helper-697aaa7", "697aaa7", DependencyEcosystem.GitSubmodule, new[] { "https://github.com/martincostello" }, true)]
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
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(ecosystem);

        registry.GetPackageOwnersAsync(repository, dependency, version)
                .Returns(Task.FromResult<IReadOnlyList<string>>(owners));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.GitHubActions] = ["actions"],
                    [DependencyEcosystem.Npm] = ["types", "typescript-bot"],
                    [DependencyEcosystem.NuGet] = ["aspnet", "Microsoft"],
                    [DependencyEcosystem.GitSubmodule] = ["https://github.com/martincostello"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage(dependency, version);

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            reference,
            sha,
            commitMessage,
            diff);

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
                    [DependencyEcosystem.GitHubActions] = ["actions"],
                    [DependencyEcosystem.Npm] = ["types", "typescript-bot"],
                    [DependencyEcosystem.NuGet] = ["aspnet", "Microsoft"],
                    [DependencyEcosystem.GitSubmodule] = ["https://github.com/martincostello"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [Substitute.For<IPackageRegistry>()]);

        var diff = string.Empty;
        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = @"""
                            Bump Foo to 1.0.1
                            Bumps `Foo` to 1.0.1.

                            ---
                            updated-dependencies:
                            - dependency-name: Foo
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            new("someone", "something"),
            "blah-blah",
            sha,
            commitMessage,
            diff);

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
        var target = CreateTarget(scope.ServiceProvider, options, [Substitute.For<IPackageRegistry>()]);

        var diff = string.Empty;
        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage();

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            new("someone", "something"),
            "dependabot/nuget/NodaTimeVersion-3.1.0",
            sha,
            commitMessage,
            diff);

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
                    [DependencyEcosystem.NuGet] = [],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [Substitute.For<IPackageRegistry>()]);

        var diff = string.Empty;
        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage();

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            new("someone", "something"),
            "dependabot/nuget/NodaTimeVersion-3.1.0",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeFalse();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_If_Package_Registry_Lookup_Fails()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem
                .Returns(DependencyEcosystem.GitHubActions);

        registry.When((p) => p.GetPackageOwnersAsync(repository, "actions/checkout", "3"))
                .Throw(new InvalidOperationException());

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.GitHubActions] = ["actions"],
                    [DependencyEcosystem.Npm] = ["types", "typescript-bot"],
                    [DependencyEcosystem.NuGet] = ["aspnet", "Microsoft"],
                    [DependencyEcosystem.GitSubmodule] = ["https://github.com/martincostello"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        var commitMessage = TrustedCommitMessage("actions/checkout", "3");

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/github_actions/actions/checkout-3",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeFalse();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publisher_For_Multiple_Package_Updates()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "OpenTelemetry.Instrumentation.AspNetCore", "1.0.0-rc9.12")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["OpenTelemetry"]));

        registry.GetPackageOwnersAsync(repository, "OpenTelemetry.Instrumentation.Http", "1.0.0-rc9.12")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["OpenTelemetry"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.GitHubActions] = ["actions"],
                    [DependencyEcosystem.Npm] = ["types", "typescript-bot"],
                    [DependencyEcosystem.NuGet] = ["aspnet", "Microsoft", "OpenTelemetry"],
                    [DependencyEcosystem.GitSubmodule] = ["https://github.com/martincostello"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "987879d752236a5a574000f40da7630be061faca";
        var commitMessage = """
                            Bump OpenTelemetryInstrumentationVersion
                            Bumps `OpenTelemetryInstrumentationVersion` from 1.0.0-rc9.11 to 1.0.0-rc9.12.

                            Updates `OpenTelemetry.Instrumentation.AspNetCore` from 1.0.0-rc9.11 to 1.0.0-rc9.12
                            - [Release notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
                            - [Commits](https://github.com/open-telemetry/opentelemetry-dotnet/compare/1.0.0-rc9.11...1.0.0-rc9.12)

                            Updates `OpenTelemetry.Instrumentation.Http` from 1.0.0-rc9.11 to 1.0.0-rc9.12
                            - [Release notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
                            - [Commits](https://github.com/open-telemetry/opentelemetry-dotnet/compare/1.0.0-rc9.11...1.0.0-rc9.12)

                            ---
                            updated-dependencies:
                            - dependency-name: OpenTelemetry.Instrumentation.AspNetCore
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                            - dependency-name: OpenTelemetry.Instrumentation.Http
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/OpenTelemetryInstrumentationVersion-1.0.0-rc9.12",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publisher_For_Grouped_Package_Update_For_One_Package()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "xunit", "2.6.3")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["dotnetfoundation", "xunit"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["dotnetfoundation", "xunit"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "62bf0f9046a782d0233aed41e9fe466aa6751f95";
        var commitMessage = """
                            Bump the xunit group with 1 update
                            Bumps the xunit group with 1 update: [xunit](https://github.com/xunit/xunit).

                            - [Commits](https://github.com/xunit/xunit/compare/2.6.2...2.6.3)

                            ---
                            updated-dependencies:
                            - dependency-name: xunit
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: xunit
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/xunit-beb0c94413",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publisher_For_Grouped_Package_Update_For_Two_Packages()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "xunit", "2.6.3")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["dotnetfoundation", "xunit"]));

        registry.GetPackageOwnersAsync(repository, "xunit.runner.visualstudio", "2.5.5")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["dotnetfoundation", "xunit"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["dotnetfoundation", "xunit"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "dd1180fe710bc24ef13403f2b9dec6e069cb607c";
        var commitMessage = """
                            Bump the xunit group with 2 updates (#315)
                            Bumps the xunit group with 2 updates: [xunit](https://github.com/xunit/xunit) and [xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit).


                            Updates `xunit` from 2.6.2 to 2.6.3
                            - [Commits](xunit/xunit@2.6.2...2.6.3)

                            Updates `xunit.runner.visualstudio` from 2.5.4 to 2.5.5
                            - [Release notes](https://github.com/xunit/visualstudio.xunit/releases)
                            - [Commits](xunit/visualstudio.xunit@2.5.4...2.5.5)

                            ---
                            updated-dependencies:
                            - dependency-name: xunit
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: xunit
                            - dependency-name: xunit.runner.visualstudio
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: xunit
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            Co-authored-by: dependabot[bot] <49699333+dependabot[bot]@users.noreply.github.com>
                            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/xunit-9380cae661",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publisher_For_Grouped_Package_Update_For_Two_Packages_With_No_Release_Notes()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "xunit", "2.9.0")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["dotnetfoundation", "xunit"]));

        registry.GetPackageOwnersAsync(repository, "xunit.runner.visualstudio", "2.8.2")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["dotnetfoundation", "xunit"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["dotnetfoundation", "xunit"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "661369a466e4ac01d5db6057d499074733d8086f";
        var commitMessage = """
                            Bump the xunit group with 2 updates
                            Bumps the xunit group with 2 updates: xunit and xunit.runner.visualstudio.


                            Updates `xunit` from 2.8.1 to 2.9.0

                            Updates `xunit.runner.visualstudio` from 2.8.1 to 2.8.2

                            ---
                            updated-dependencies:
                            - dependency-name: xunit
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: xunit
                            - dependency-name: xunit.runner.visualstudio
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: xunit
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/xunit-8e312df114",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publisher_For_Single_Package_Update()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem
            .Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "Microsoft.TypeScript.MSBuild", "4.9.5")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["Microsoft", "TypeScriptTeam"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.GitHubActions] = ["actions"],
                    [DependencyEcosystem.Npm] = ["types", "typescript-bot"],
                    [DependencyEcosystem.NuGet] = ["aspnet", "Microsoft"],
                    [DependencyEcosystem.GitSubmodule] = ["https://github.com/martincostello"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "552aae859c24f0ed63bcc1f82ef96dd83040762f";
        var commitMessage = """
                            Bump Microsoft.TypeScript.MSBuild from 4.9.4 to 4.9.5
                            Bumps Microsoft.TypeScript.MSBuild from 4.9.4 to 4.9.5.
                            ---
                            updated-dependencies:
                            - dependency-name: Microsoft.TypeScript.MSBuild
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                            ...
                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/Microsoft.TypeScript.MSBuild-4.9.5",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publisher_For_Multiple_Package_Update_Using_Update_DotNet_Sdk()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "Microsoft.Extensions.Configuration.Binder", "7.0.4")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["Microsoft", "aspnet"]));

        registry.GetPackageOwnersAsync(repository, "Microsoft.Extensions.Http.Polly", "7.0.5")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["Microsoft", "aspnet"]));

        registry.GetPackageOwnersAsync(repository, "Microsoft.NET.Test.Sdk", "17.5.0")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["Microsoft", "aspnet"]));

        registry.GetPackageOwnersAsync(repository, "System.Text.Json", "7.0.2")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["Microsoft", "dotnetframework"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["aspnet", "dotnetframework", "Microsoft"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var diff = string.Empty;
        var sha = "4f01e284f9bfac38bcf14f7595b1258fc5b1b542";
        var commitMessage = """
                            Bump .NET NuGet packages
                            Bumps .NET dependencies to their latest versions for the .NET 7.0.203 SDK.
                            Bumps Microsoft.Extensions.Configuration.Binder from 7.0.0 to 7.0.4.
                            Bumps Microsoft.Extensions.Http.Polly from 7.0.2 to 7.0.5.
                            Bumps Microsoft.NET.Test.Sdk from 17.4.0 to 17.5.0.
                            Bumps System.Text.Json from 7.0.0 to 7.0.2.
                            ---
                            updated-dependencies:
                            - dependency-name: Microsoft.Extensions.Configuration.Binder
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                            - dependency-name: Microsoft.Extensions.Http.Polly
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                            - dependency-name: Microsoft.NET.Test.Sdk
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                            - dependency-name: System.Text.Json
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                            ...
                            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "update-dotnet-sdk-7.0.203",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Trusted_Publisher_For_Grouped_Package_Update_With_Only_Yaml_Frontmatter()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "AWSSDK.SecurityToken", "3.7.300.118")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["awsdotnet"]));

        registry.GetPackageOwnersAsync(repository, "AWSSDK.SimpleSystemsManagement", "3.7.305.8")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["awsdotnet"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["awsdotnet"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var sha = "bd2804732332a86c336d1c9308b9dba36f0c2e03";
        var commitMessage = """
                            Bump the awssdk group with 2 updates
                            ---
                            updated-dependencies:
                            - dependency-name: AWSSDK.SecurityToken
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: awssdk
                            - dependency-name: AWSSDK.SimpleSystemsManagement
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: awssdk
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        var diff =
            """
            diff --git a/Directory.Packages.props b/Directory.Packages.props
            index 5efad590..bd28047 100644
            --- a/Directory.Packages.props
            +++ b/Directory.Packages.props
            @@ -7,8 +7,8 @@
               <ItemGroup>
                 <PackageVersion Include="Amazon.AspNetCore.DataProtection.SSM" Version="3.2.1" />
                 <PackageVersion Include="Aspire.Hosting.AppHost" Version="8.1.0" />
            -    <PackageVersion Include="AWSSDK.SecurityToken" Version="3.7.300.117" />
            -    <PackageVersion Include="AWSSDK.SimpleSystemsManagement" Version="3.7.305.7" />
            +    <PackageVersion Include="AWSSDK.SecurityToken" Version="3.7.300.118" />
            +    <PackageVersion Include="AWSSDK.SimpleSystemsManagement" Version="3.7.305.8" />
                 <PackageVersion Include="BenchmarkDotNet" Version="0.14.0" />
                 <PackageVersion Include="coverlet.msbuild" Version="6.0.2" />
                 <PackageVersion Include="GitHubActionsTestLogger" Version="2.4.1" />
            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/awssdk-b8164d6bd2",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Mix_Of_Trust_By_Name_And_Publisher()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "Microsoft.IdentityModel.JsonWebTokens", "8.0.0")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["AzureAD", "Microsoft"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Dependencies = ["^AspNet.Security.OAuth\\..*$"],
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["Microsoft"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var sha = "815aad7927000ff23a2a61f2c640dad01a88658c";
        var commitMessage = """
                            Bump the aspnet-security-oauth group with 4 updates
                            Bumps the aspnet-security-oauth group with 4 updates: [AspNet.Security.OAuth.Amazon](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers), [AspNet.Security.OAuth.Apple](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers), [Microsoft.IdentityModel.JsonWebTokens](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) and [AspNet.Security.OAuth.GitHub](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers).


                            Updates `AspNet.Security.OAuth.Amazon` from 9.0.0-rc.2.24554.41 to 9.0.0-rc.2.24557.45
                            - [Release notes](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/releases)
                            - [Commits](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/commits)

                            Updates `AspNet.Security.OAuth.Apple` from 9.0.0-rc.2.24554.41 to 9.0.0-rc.2.24557.45
                            - [Release notes](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/releases)
                            - [Commits](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/commits)

                            Updates `Microsoft.IdentityModel.JsonWebTokens` from 8.2.0 to 8.0.0
                            - [Release notes](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/releases)
                            - [Changelog](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/dev/CHANGELOG.md)
                            - [Commits](AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet@8.2.0...8.0.0)

                            Updates `AspNet.Security.OAuth.GitHub` from 9.0.0-rc.2.24554.41 to 9.0.0-rc.2.24557.45
                            - [Release notes](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/releases)
                            - [Commits](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/commits)

                            ---
                            updated-dependencies:
                            - dependency-name: AspNet.Security.OAuth.Amazon
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: aspnet-security-oauth
                            - dependency-name: AspNet.Security.OAuth.Apple
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: aspnet-security-oauth
                            - dependency-name: Microsoft.IdentityModel.JsonWebTokens
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: aspnet-security-oauth
                            - dependency-name: AspNet.Security.OAuth.GitHub
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: aspnet-security-oauth
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        var diff =
            """
            diff --git a/Directory.Packages.props b/Directory.Packages.props
            index ead99a216..44207d0d1 100644
            --- a/Directory.Packages.props
            +++ b/Directory.Packages.props
            @@ -11,9 +11,9 @@
                 <PackageVersion Include="Aspire.Hosting.Azure.KeyVault" Version="9.0.0-rc.1.24511.1" />
                 <PackageVersion Include="Aspire.Hosting.Azure.Storage" Version="9.0.0-rc.1.24511.1" />
                 <PackageVersion Include="Aspire.Microsoft.Azure.Cosmos" Version="9.0.0-rc.1.24511.1" />
            -    <PackageVersion Include="AspNet.Security.OAuth.Amazon" Version="9.0.0-rc.2.24554.41" />
            -    <PackageVersion Include="AspNet.Security.OAuth.Apple" Version="9.0.0-rc.2.24554.41" />
            -    <PackageVersion Include="AspNet.Security.OAuth.GitHub" Version="9.0.0-rc.2.24554.41" />
            +    <PackageVersion Include="AspNet.Security.OAuth.Amazon" Version="9.0.0-rc.2.24557.45" />
            +    <PackageVersion Include="AspNet.Security.OAuth.Apple" Version="9.0.0-rc.2.24557.45" />
            +    <PackageVersion Include="AspNet.Security.OAuth.GitHub" Version="9.0.0-rc.2.24557.45" />
                 <PackageVersion Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.3.2" />
                 <PackageVersion Include="Azure.Extensions.AspNetCore.DataProtection.Blobs" Version="1.3.4" />
                 <PackageVersion Include="Azure.Extensions.AspNetCore.DataProtection.Keys" Version="1.2.4" />
            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/aspnet-security-oauth-b2c2f7560d",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeTrue();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Ignored_Packages_In_Dependabot_Configuration()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);
        var reference = "dependabot/nuget/aspnet-security-oauth-b2c2f7560d";

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "Microsoft.IdentityModel.JsonWebTokens", "8.0.0")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["AzureAD", "Microsoft"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Dependencies = ["^AspNet.Security.OAuth\\..*$"],
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["Microsoft"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();

        var dependabotConfiguration =
            """
            version: 2
            updates:
            - package-ecosystem: "github-actions"
              directory: "/"
              schedule:
                interval: daily
                time: "05:30"
                timezone: Europe/London
              reviewers:
                - "octocat"
            - package-ecosystem: nuget
              directory: "/"
              groups:
                Microsoft.OpenApi:
                  patterns:
                    - Microsoft.OpenApi*
                xunit:
                  patterns:
                    - Verify.Xunit
                    - xunit*
              schedule:
                interval: daily
                time: "05:30"
                timezone: Europe/London
              reviewers:
                - "octocat"
              open-pull-requests-limit: 99
              ignore:
                - dependency-name: "Microsoft.IdentityModel.*"
            """;

        var target = CreateTarget(
            scope.ServiceProvider,
            options,
            [registry],
            (client) =>
            {
                client.GetRawContentByRef(
                    repository.Owner,
                    repository.Name,
                    ".github/dependabot.yml",
                    reference)
                    .Returns(Task.FromResult(Encoding.UTF8.GetBytes(dependabotConfiguration)));
            });

        var sha = "815aad7927000ff23a2a61f2c640dad01a88658c";
        var commitMessage = """
                            Bump the aspnet-security-oauth group with 4 updates
                            Bumps the aspnet-security-oauth group with 4 updates: [AspNet.Security.OAuth.Amazon](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers), [AspNet.Security.OAuth.Apple](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers), [Microsoft.IdentityModel.JsonWebTokens](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) and [AspNet.Security.OAuth.GitHub](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers).


                            Updates `AspNet.Security.OAuth.Amazon` from 9.0.0-rc.2.24554.41 to 9.0.0-rc.2.24557.45
                            - [Release notes](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/releases)
                            - [Commits](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/commits)

                            Updates `AspNet.Security.OAuth.Apple` from 9.0.0-rc.2.24554.41 to 9.0.0-rc.2.24557.45
                            - [Release notes](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/releases)
                            - [Commits](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/commits)

                            Updates `Microsoft.IdentityModel.JsonWebTokens` from 8.2.0 to 8.0.0
                            - [Release notes](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/releases)
                            - [Changelog](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/dev/CHANGELOG.md)
                            - [Commits](AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet@8.2.0...8.0.0)

                            Updates `AspNet.Security.OAuth.GitHub` from 9.0.0-rc.2.24554.41 to 9.0.0-rc.2.24557.45
                            - [Release notes](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/releases)
                            - [Commits](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers/commits)

                            ---
                            updated-dependencies:
                            - dependency-name: AspNet.Security.OAuth.Amazon
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: aspnet-security-oauth
                            - dependency-name: AspNet.Security.OAuth.Apple
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: aspnet-security-oauth
                            - dependency-name: Microsoft.IdentityModel.JsonWebTokens
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: aspnet-security-oauth
                            - dependency-name: AspNet.Security.OAuth.GitHub
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: aspnet-security-oauth
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        var diff =
            """
            diff --git a/Directory.Packages.props b/Directory.Packages.props
            index ead99a216..44207d0d1 100644
            --- a/Directory.Packages.props
            +++ b/Directory.Packages.props
            @@ -11,9 +11,9 @@
                 <PackageVersion Include="Aspire.Hosting.Azure.KeyVault" Version="9.0.0-rc.1.24511.1" />
                 <PackageVersion Include="Aspire.Hosting.Azure.Storage" Version="9.0.0-rc.1.24511.1" />
                 <PackageVersion Include="Aspire.Microsoft.Azure.Cosmos" Version="9.0.0-rc.1.24511.1" />
            -    <PackageVersion Include="AspNet.Security.OAuth.Amazon" Version="9.0.0-rc.2.24554.41" />
            -    <PackageVersion Include="AspNet.Security.OAuth.Apple" Version="9.0.0-rc.2.24554.41" />
            -    <PackageVersion Include="AspNet.Security.OAuth.GitHub" Version="9.0.0-rc.2.24554.41" />
            +    <PackageVersion Include="AspNet.Security.OAuth.Amazon" Version="9.0.0-rc.2.24557.45" />
            +    <PackageVersion Include="AspNet.Security.OAuth.Apple" Version="9.0.0-rc.2.24557.45" />
            +    <PackageVersion Include="AspNet.Security.OAuth.GitHub" Version="9.0.0-rc.2.24557.45" />
                 <PackageVersion Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.3.2" />
                 <PackageVersion Include="Azure.Extensions.AspNetCore.DataProtection.Blobs" Version="1.3.4" />
                 <PackageVersion Include="Azure.Extensions.AspNetCore.DataProtection.Keys" Version="1.2.4" />
            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            reference,
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeFalse();
    }

    [Fact]
    public async Task Commit_Is_Analyzed_Correctly_With_Duplicated_Dependency_Names()
    {
        // Arrange
        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var repository = new RepositoryId(repo.Owner.Login, repo.Name);

        var registry = Substitute.For<IPackageRegistry>();

        registry.Ecosystem.Returns(DependencyEcosystem.NuGet);

        registry.GetPackageOwnersAsync(repository, "Microsoft.IdentityModel.JsonWebTokens", "8.0.0")
                .Returns(Task.FromResult<IReadOnlyList<string>>(["AzureAD", "Microsoft"]));

        var options = new WebhookOptions()
        {
            TrustedEntities = new()
            {
                Dependencies = ["^AspNet.Security.OAuth\\..*$"],
                Publishers = new Dictionary<DependencyEcosystem, IList<string>>()
                {
                    [DependencyEcosystem.NuGet] = ["Microsoft"],
                },
            },
        };

        using var scope = Fixture.Services.CreateScope();
        var target = CreateTarget(scope.ServiceProvider, options, [registry]);

        var sha = "314783fa32248fc8961b9cea5a3dbf8e32a93393";
        var commitMessage = """
                            Bump the opentelemetry group with 5 updates
                            Bumps the opentelemetry group with 5 updates:

                            | Package | From | To |
                            | --- | --- | --- |
                            | [Microsoft.Extensions.Configuration.Binder](https://github.com/dotnet/runtime) | `9.0.0` | `9.0.0` |
                            | [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | `9.0.0` | `9.0.0` |
                            | [OpenTelemetry](https://github.com/open-telemetry/opentelemetry-dotnet) | `1.9.0` | `1.10.0` |
                            | [OpenTelemetry.Exporter.OpenTelemetryProtocol](https://github.com/open-telemetry/opentelemetry-dotnet) | `1.9.0` | `1.10.0` |
                            | [OpenTelemetry.Extensions.Hosting](https://github.com/open-telemetry/opentelemetry-dotnet) | `1.9.0` | `1.10.0` |


                            Updates `Microsoft.Extensions.Configuration.Binder` from 9.0.0 to 9.0.0
                            - [Release notes](https://github.com/dotnet/runtime/releases)
                            - [Commits](dotnet/runtime@v9.0.0...v9.0.0)

                            Updates `Microsoft.Extensions.DependencyInjection` from 9.0.0 to 9.0.0
                            - [Release notes](https://github.com/dotnet/runtime/releases)
                            - [Commits](dotnet/runtime@v9.0.0...v9.0.0)

                            Updates `OpenTelemetry` from 1.9.0 to 1.10.0
                            - [Release notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
                            - [Changelog](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/RELEASENOTES.md)
                            - [Commits](open-telemetry/opentelemetry-dotnet@core-1.9.0...core-1.10.0)

                            Updates `Microsoft.Extensions.Configuration.Binder` from 9.0.0 to 9.0.0
                            - [Release notes](https://github.com/dotnet/runtime/releases)
                            - [Commits](dotnet/runtime@v9.0.0...v9.0.0)

                            Updates `Microsoft.Extensions.DependencyInjection` from 9.0.0 to 9.0.0
                            - [Release notes](https://github.com/dotnet/runtime/releases)
                            - [Commits](dotnet/runtime@v9.0.0...v9.0.0)

                            Updates `OpenTelemetry` from 1.9.0 to 1.10.0
                            - [Release notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
                            - [Changelog](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/RELEASENOTES.md)
                            - [Commits](open-telemetry/opentelemetry-dotnet@core-1.9.0...core-1.10.0)

                            Updates `OpenTelemetry.Exporter.OpenTelemetryProtocol` from 1.9.0 to 1.10.0
                            - [Release notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
                            - [Changelog](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/RELEASENOTES.md)
                            - [Commits](open-telemetry/opentelemetry-dotnet@core-1.9.0...core-1.10.0)

                            Updates `Microsoft.Extensions.Configuration.Binder` from 9.0.0 to 9.0.0
                            - [Release notes](https://github.com/dotnet/runtime/releases)
                            - [Commits](dotnet/runtime@v9.0.0...v9.0.0)

                            Updates `Microsoft.Extensions.DependencyInjection` from 9.0.0 to 9.0.0
                            - [Release notes](https://github.com/dotnet/runtime/releases)
                            - [Commits](dotnet/runtime@v9.0.0...v9.0.0)

                            Updates `OpenTelemetry` from 1.9.0 to 1.10.0
                            - [Release notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
                            - [Changelog](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/RELEASENOTES.md)
                            - [Commits](open-telemetry/opentelemetry-dotnet@core-1.9.0...core-1.10.0)

                            Updates `OpenTelemetry.Extensions.Hosting` from 1.9.0 to 1.10.0
                            - [Release notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
                            - [Changelog](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/RELEASENOTES.md)
                            - [Commits](open-telemetry/opentelemetry-dotnet@core-1.9.0...core-1.10.0)

                            ---
                            updated-dependencies:
                            - dependency-name: Microsoft.Extensions.Configuration.Binder
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: opentelemetry
                            - dependency-name: Microsoft.Extensions.DependencyInjection
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: opentelemetry
                            - dependency-name: OpenTelemetry
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: opentelemetry
                            - dependency-name: Microsoft.Extensions.Configuration.Binder
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: opentelemetry
                            - dependency-name: Microsoft.Extensions.DependencyInjection
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: opentelemetry
                            - dependency-name: OpenTelemetry
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: opentelemetry
                            - dependency-name: OpenTelemetry.Exporter.OpenTelemetryProtocol
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: opentelemetry
                            - dependency-name: Microsoft.Extensions.Configuration.Binder
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: opentelemetry
                            - dependency-name: Microsoft.Extensions.DependencyInjection
                              dependency-type: direct:production
                              update-type: version-update:semver-patch
                              dependency-group: opentelemetry
                            - dependency-name: OpenTelemetry
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: opentelemetry
                            - dependency-name: OpenTelemetry.Extensions.Hosting
                              dependency-type: direct:production
                              update-type: version-update:semver-minor
                              dependency-group: opentelemetry
                            ...

                            Signed-off-by: dependabot[bot] <support@github.com>
                            """;

        var diff =
            """
            diff --git a/Directory.Packages.props b/Directory.Packages.props
            index 72e72ccd..35f86689 100644
            --- a/Directory.Packages.props
            +++ b/Directory.Packages.props
            @@ -28,9 +28,9 @@
                 <PackageVersion Include="Microsoft.Extensions.Telemetry" Version="9.0.0" />
                 <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
                 <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
            -    <PackageVersion Include="OpenTelemetry" Version="1.9.0" />
            -    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
            -    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
            +    <PackageVersion Include="OpenTelemetry" Version="1.10.0" />
            +    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.10.0" />
            +    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
                 <PackageVersion Include="OpenTelemetry.Instrumentation.AWS" Version="1.1.0-beta.6" />
                 <PackageVersion Include="OpenTelemetry.Instrumentation.AWSLambda" Version="1.3.0-beta.1" />
                 <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
            """;

        // Act
        var actual = await target.IsTrustedDependencyUpdateAsync(
            repository,
            "dependabot/nuget/opentelemetry-97480a8ec4",
            sha,
            commitMessage,
            diff);

        // Assert
        actual.ShouldBeFalse();
    }

    private static GitCommitAnalyzer CreateTarget(
        IServiceProvider serviceProvider,
        WebhookOptions? options = null,
        IEnumerable<IPackageRegistry>? registries = null,
        Action<IRepositoryContentsClient>? configureActionsClient = null)
    {
        registries ??= serviceProvider.GetServices<IPackageRegistry>();
        var optionsMonitor = options?.ToMonitor() ?? serviceProvider.GetRequiredService<IOptionsMonitor<WebhookOptions>>();
        var logger = serviceProvider.GetRequiredService<ILogger<GitCommitAnalyzer>>();

        var contents = Substitute.For<IRepositoryContentsClient>();
        var repositories = Substitute.For<IRepositoriesClient>();
        var client = Substitute.For<IGitHubClientForInstallation>();

        repositories.Content.Returns(contents);
        client.Repository.Returns(repositories);

        configureActionsClient?.Invoke(contents);

        return new(client, registries, optionsMonitor, logger);
    }
}
