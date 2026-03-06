// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using Azure;
using Azure.Data.Tables;
using MartinCostello.Costellobot.Models;
using NSubstitute;
using DenyEntity = MartinCostello.Costellobot.AzureTableDenyStore.DenyEntity;

namespace MartinCostello.Costellobot;

public class AzureTableDenyStoreTests
{
    [Fact]
    public async Task AllowAllAsync_Allows_All_Entities()
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("DenyStore")
              .Returns(table);

        var page = Substitute.For<Page<DenyEntity>>();
        page.Values.Returns([new(), new()]);

        var pages = Substitute.For<AsyncPageable<DenyEntity>>();
        pages.AsPages().Returns(Pages([page]));

        table.QueryAsync<DenyEntity>(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(pages);

        var target = new AzureTableDenyStore(client);

        // Act and Assert
        await Should.NotThrowAsync(() => target.AllowAllAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(DependencyEcosystem.Docker, "devcontainers/dotnet", "latest", "DOCKER", "DEVCONTAINERS~DOTNET@LATEST")]
    [InlineData(DependencyEcosystem.GitHubActions, "martincostello/rebaser", "2.0.1", "GITHUBACTIONS", "MARTINCOSTELLO~REBASER@2.0.1")]
    [InlineData(DependencyEcosystem.Npm, "@octokit/request", "9.2.2", "NPM", "@OCTOKIT~REQUEST@9.2.2")]
    [InlineData(DependencyEcosystem.NuGet, "Polly.Core", "8.5.2", "NUGET", "POLLY.CORE@8.5.2")]
    [InlineData(DependencyEcosystem.PyPI, "boto3", "1.42.51", "PYPI", "BOTO3@1.42.51")]
    [InlineData(DependencyEcosystem.Ruby, "rack", "3.1.16", "RUBY", "RACK@3.1.16")]
    public async Task AllowAsync_Allows_Entity(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        string expectedPartitionKey,
        string expectedRowKey)
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("DenyStore")
              .Returns(table);

        var target = new AzureTableDenyStore(client);

        // Act
        await target.AllowAsync(
            ecosystem,
            id,
            version,
            TestContext.Current.CancellationToken);

        // Assert
        await table.Received().DeleteEntityAsync(
            expectedPartitionKey,
            expectedRowKey,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetDeniedAsync_Returns_Correct_Values()
    {
        // Arrange
        var ecosystem = DependencyEcosystem.NuGet;
        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("DenyStore")
              .Returns(table);

        DenyEntity[] entities =
        [
            new()
            {
                DependencyEcosystem = "NuGet",
                DependencyId = "Humanizer.Core",
                DependencyVersion = "2.14.1",
                Timestamp = new(2025, 02, 23, 12, 34, 55, TimeSpan.Zero),
            },
            new()
            {
                DependencyEcosystem = "NuGet",
                DependencyId = "Humanizer.Core",
                DependencyVersion = "2.14.2",
                Timestamp = new(2025, 02, 23, 12, 34, 56, TimeSpan.Zero),
            },
        ];

        var page = Substitute.For<Page<DenyEntity>>();
        page.Values.Returns(entities);

        var pages = Substitute.For<AsyncPageable<DenyEntity>>();
        pages.AsPages().Returns(Pages([page]));

        table.QueryAsync<DenyEntity>(Arg.Any<Expression<Func<DenyEntity, bool>>>(), Arg.Any<int>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(pages);

        var target = new AzureTableDenyStore(client);

        // Act
        var actual = await target.GetDeniedAsync(ecosystem, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldNotBeEmpty();
        actual.ShouldContain(new DeniedDependency("Humanizer.Core", "2.14.1") { DeniedAt = new(2025, 02, 23, 12, 34, 55, TimeSpan.Zero) });
        actual.ShouldContain(new DeniedDependency("Humanizer.Core", "2.14.2") { DeniedAt = new(2025, 02, 23, 12, 34, 56, TimeSpan.Zero) });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task IsDeniedAsync_Returns_Correct_Value(bool hasValue, bool expected)
    {
        // Arrange
        var ecosystem = DependencyEcosystem.NuGet;
        var id = "Humanizer.Core";
        var version = "2.14.1";

        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("DenyStore")
              .Returns(table);

        var response = Substitute.For<NullableResponse<DenyEntity>>();
        response.HasValue.Returns(hasValue);

        table.GetEntityIfExistsAsync<DenyEntity>(
            "NUGET",
            "HUMANIZER.CORE@2.14.1",
            cancellationToken: TestContext.Current.CancellationToken).Returns(response);

        var target = new AzureTableDenyStore(client);

        // Act
        var actual = await target.IsDeniedAsync(ecosystem, id, version, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task DenyAsync_Does_Not_Throw()
    {
        // Arrange
        var ecosystem = DependencyEcosystem.NuGet;
        var id = "Humanizer.Core";
        var version = "2.14.1";

        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("DenyStore")
              .Returns(table);

        var target = new AzureTableDenyStore(client);

        // Act and Assert
        await Should.NotThrowAsync(() => target.DenyAsync(ecosystem, id, version, TestContext.Current.CancellationToken));
    }

    private static async IAsyncEnumerable<Page<DenyEntity>> Pages(IEnumerable<Page<DenyEntity>> pages)
    {
        foreach (var page in pages)
        {
            yield return page;
        }

        await Task.CompletedTask;
    }
}
