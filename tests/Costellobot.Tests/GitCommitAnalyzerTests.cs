// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot;

[Collection(AppCollection.Name)]
public class GitCommitAnalyzerTests : IntegrationTests<AppFixture>
{
    public GitCommitAnalyzerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Theory]
    [InlineData("@actions/github", true)]
    [InlineData("actions/checkout", true)]
    [InlineData("martincostello/update-dotnet-sdk", true)]
    [InlineData("Microsoft.NET.Sdk", true)]
    [InlineData("NodaTime", true)]
    [InlineData("NodaTimee", false)]
    [InlineData("NodaTime.Testing", true)]
    [InlineData("NodaTime.Testingg", false)]
    [InlineData("typescript-hax", false)]
    public void Commit_Is_Analyzed_Correct(string dependency, bool expected)
    {
        // Arrange
        using var scope = Fixture.Services.CreateScope();
        var target = scope.ServiceProvider.GetRequiredService<GitCommitAnalyzer>();

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        string sha = "0304f7fb4e17d674ea52392d70e775761ccf5aed";
        string commitMessage = TrustedCommitMessage(dependency);

        // Act
        bool actual = target.IsTrustedDependencyUpdate(
            owner.Login,
            repo.Name,
            sha,
            commitMessage);

        // Assert
        actual.ShouldBe(expected);
    }
}
