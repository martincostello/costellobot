// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using Azure;
using Azure.Data.Tables;
using MartinCostello.Costellobot.Models;
using NSubstitute;
using TrustEntity = MartinCostello.Costellobot.AzureTableTrustStore.TrustEntity;

namespace MartinCostello.Costellobot;

public class AzureTableTrustStoreTests
{
    [Fact]
    public async Task DistrustAllAsync_Distrusts_All_Entities()
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("TrustStore")
              .Returns(table);

        var page = Substitute.For<Page<TrustEntity>>();
        page.Values.Returns([new(), new()]);

        var pages = Substitute.For<AsyncPageable<TrustEntity>>();
        pages.AsPages().Returns(Pages([page]));

        table.QueryAsync<TrustEntity>(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(pages);

        var target = new AzureTableTrustStore(client);

        // Act and Assert
        await Should.NotThrowAsync(() => target.DistrustAllAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(DependencyEcosystem.Docker, "devcontainers/dotnet", "latest", "DOCKER", "DEVCONTAINERS~DOTNET@LATEST")]
    [InlineData(DependencyEcosystem.GitHubActions, "martincostello/rebaser", "2.0.1", "GITHUBACTIONS", "MARTINCOSTELLO~REBASER@2.0.1")]
    [InlineData(DependencyEcosystem.Npm, "@octokit/request", "9.2.2", "NPM", "@OCTOKIT~REQUEST@9.2.2")]
    [InlineData(DependencyEcosystem.NuGet, "Polly.Core", "8.5.2", "NUGET", "POLLY.CORE@8.5.2")]
    [InlineData(DependencyEcosystem.Ruby, "rack", "3.1.16", "RUBY", "RACK@3.1.16")]
    public async Task DistrustAsync_Distrusts_Entity(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        string expectedPartitionKey,
        string expectedRowKey)
    {
        // Arrange
        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("TrustStore")
              .Returns(table);

        var target = new AzureTableTrustStore(client);

        // Act
        await target.DistrustAsync(
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
    public async Task GetTrustAsync_Returns_Correct_Values()
    {
        // Arrange
        var ecosystem = DependencyEcosystem.NuGet;
        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("TrustStore")
              .Returns(table);

        TrustEntity[] entities =
        [
            new()
            {
                DependencyEcosystem = "NuGet",
                DependencyId = "Polly",
                DependencyVersion = "8.5.2",
                Timestamp = new(2025, 02, 23, 12, 34, 55, TimeSpan.Zero),
            },
            new()
            {
                DependencyEcosystem = "NuGet",
                DependencyId = "Polly.Core",
                DependencyVersion = "8.5.2",
                Timestamp = new(2025, 02, 23, 12, 34, 56, TimeSpan.Zero),
            },
        ];

        var page = Substitute.For<Page<TrustEntity>>();
        page.Values.Returns(entities);

        var pages = Substitute.For<AsyncPageable<TrustEntity>>();
        pages.AsPages().Returns(Pages([page]));

        table.QueryAsync<TrustEntity>(Arg.Any<Expression<Func<TrustEntity, bool>>>(), Arg.Any<int>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(pages);

        var target = new AzureTableTrustStore(client);

        // Act
        var actual = await target.GetTrustAsync(ecosystem, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldNotBeEmpty();
        actual.ShouldContain(new TrustedDependency("Polly", "8.5.2") { TrustedAt = new(2025, 02, 23, 12, 34, 55, TimeSpan.Zero) });
        actual.ShouldContain(new TrustedDependency("Polly.Core", "8.5.2") { TrustedAt = new(2025, 02, 23, 12, 34, 56, TimeSpan.Zero) });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task IsTrustedAsync_Returns_Correct_Value(bool hasValue, bool expected)
    {
        // Arrange
        var ecosystem = DependencyEcosystem.NuGet;
        var id = "Polly.Core";
        var version = "8.5.2";

        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("TrustStore")
              .Returns(table);

        var response = Substitute.For<NullableResponse<TrustEntity>>();
        response.HasValue.Returns(hasValue);

        table.GetEntityIfExistsAsync<TrustEntity>(
            "NUGET",
            "POLLY.CORE@8.5.2",
            cancellationToken: TestContext.Current.CancellationToken).Returns(response);

        var target = new AzureTableTrustStore(client);

        // Act
        var actual = await target.IsTrustedAsync(ecosystem, id, version, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task TrustAsync_Does_Not_Throw()
    {
        // Arrange
        var ecosystem = DependencyEcosystem.NuGet;
        var id = "Polly.Core";
        var version = "8.5.2";

        var table = Substitute.For<TableClient>();
        var client = Substitute.For<TableServiceClient>();

        client.GetTableClient("TrustStore")
              .Returns(table);

        var target = new AzureTableTrustStore(client);

        // Act and Assert
        await Should.NotThrowAsync(() => target.TrustAsync(ecosystem, id, version, TestContext.Current.CancellationToken));
    }

    private static async IAsyncEnumerable<Page<TrustEntity>> Pages(IEnumerable<Page<TrustEntity>> pages)
    {
        foreach (var page in pages)
        {
            yield return page;
        }

        await Task.CompletedTask;
    }
}
