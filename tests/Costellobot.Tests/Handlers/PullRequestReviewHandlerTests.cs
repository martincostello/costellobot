// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Handlers;

[Collection<AppCollection>]
public class PullRequestReviewHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Trust_Store_Is_Updated_For_Owner_Approved_Pull_Request_From_Trusted_User()
    {
        // Arrange
        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await trustStoreUpdated.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Trust_Store_Is_Updated_For_Owner_Approved_Pull_Request_From_Trusted_User_For_Multiple_Dependencies_From_Commit()
    {
        // Arrange
        (string Id, string Version)[] dependencies =
        [
            ("foo", "1.2.3"),
            ("bar", "4.5.6"),
        ];

        var driver = await ConfigureAsync(("_", "_"));
        driver.WithCommitMessage(
            """
            Bump the foobar group with 2 updates (#315)
            Bumps the foobar group with 2 updates: foo and bar.
            
            
            Updates `foo` from 1.2.2 to 1.2.3
            
            Updates `bar` from 4.5.5 to 4.5.6
            
            ---
            updated-dependencies:
            - dependency-name: foo
              dependency-type: direct:production
              update-type: version-update:semver-patch
              dependency-group: foobar
            - dependency-name: bar
              dependency-type: direct:production
              update-type: version-update:semver-patch
              dependency-group: foobar
            ...
            
            Signed-off-by: dependabot[bot] <support@github.com>
            Co-authored-by: dependabot[bot] <49699333+dependabot[bot]@users.noreply.github.com>
            """);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependencies);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await trustStoreUpdated.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact(Skip = "https://github.com/justeattakeaway/httpclient-interception/pull/1009")]
    public async Task Trust_Store_Is_Updated_For_Owner_Approved_Pull_Request_From_Trusted_User_For_Multiple_Dependencies_From_Diff()
    {
        // Arrange
        (string Id, string Version)[] dependencies =
        [
            ("bar", "4.5.6"),
            ("foo", "1.2.3"),
        ];

        var driver = await ConfigureAsync(("_", "_"));

        driver.WithCommitMessage(
            """
            Bump the foobar group with 2 updates (#1234)
            ---
            updated-dependencies:
            - dependency-name: bar
              dependency-type: direct:production
              update-type: version-update:semver-patch
              dependency-group: foobar
            - dependency-name: foo
              dependency-type: direct:production
              update-type: version-update:semver-patch
              dependency-group: foobar
            ...
            
            Signed-off-by: dependabot[bot] <support@github.com>
            Co-authored-by: dependabot[bot] <49699333+dependabot[bot]@users.noreply.github.com>
            """);

        driver.WithDiff(
            """
            diff --git a/Directory.Packages.props b/Directory.Packages.props
            index effd847a..4cd09220 100644
            --- a/Directory.Packages.props
            +++ b/Directory.Packages.props
            @@ -10,8 +10,8 @@
                 <PackageVersion Include="Aspire.Azure.Messaging.ServiceBus" Version="9.0.0" />
                 <PackageVersion Include="Aspire.Hosting.AppHost" Version="9.0.0" />
                 <PackageVersion Include="AspNet.Security.OAuth.GitHub" Version="9.0.0" />
            -    <PackageVersion Include="bar" Version="4.5.5" />
            -    <PackageVersion Include="foo" Version="1.2.2" />
            +    <PackageVersion Include="bar" Version="4.5.6" />
            +    <PackageVersion Include="foo" Version="1.2.3" />
                 <PackageVersion Include="GitHubActionsTestLogger" Version="2.4.1" />
                 <PackageVersion Include="GitVersion.Tool" Version="6.1.0" />
                 <PackageVersion Include="Humanizer" Version="2.14.1" />
            """);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependencies);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await trustStoreUpdated.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Other_Pull_Requests_Are_Approved()
    {
        // Arrange
        Fixture.ApprovePullRequests();
        Fixture.AutoMergeEnabled();

        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        (var trustedPullRepo, var trustedPullMerged) = RegisterPullRequest(driver.Owner);
        (var archivedRepo, var archivedPullMerged) = RegisterPullRequest(driver.Owner, isArchived: true);
        (var forkedRepo, var forkedPullMerged) = RegisterPullRequest(driver.Owner, isFork: true);
        (var reviewedPullRepo, var reviewedPullMerged) = RegisterPullRequest(driver.Owner, alreadyReviewed: true);
        (var untrustedPullRepo, var untrustedPullMerged) = RegisterPullRequest(driver.Owner, isTrusted: false);
        (var ignoredPullRepo, var ignoredPullMerged) = RegisterPullRequest(new("ignored-org"), name: "ignored-repo");

        RegisterInstallationRepositories(
        [
            driver.Repository,
            trustedPullRepo,
            archivedRepo,
            forkedRepo,
            reviewedPullRepo,
            untrustedPullRepo,
            ignoredPullRepo,
        ]);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await trustStoreUpdated.Task.WaitAsync(ResultTimeout, CancellationToken);
        await trustedPullMerged.Task.WaitAsync(ResultTimeout, CancellationToken);

        await AssertTaskNotRun(archivedPullMerged);
        await AssertTaskNotRun(forkedPullMerged);
        await AssertTaskNotRun(reviewedPullMerged);
        await AssertTaskNotRun(untrustedPullMerged);
        await AssertTaskNotRun(ignoredPullMerged);

        (RepositoryBuilder Repository, TaskCompletionSource CompletionSource) RegisterPullRequest(
            UserBuilder owner,
            string? name = null,
            bool alreadyReviewed = false,
            bool isArchived = false,
            bool isFork = false,
            bool isTrusted = true)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var pull = PullRequestDriver.ForDependabot();

            if (isTrusted)
            {
                pull.WithCommitMessage(TrustedCommitMessage(dependency.Item1, dependency.Item2));
            }

            pull.Owner = owner;
            pull.Repository.IsArchived = isArchived;
            pull.Repository.IsFork = isFork;
            pull.Repository.Owner = owner;

            if (name is not null)
            {
                pull.Repository.Name = name;
            }

            if (isArchived || isFork)
            {
                return (pull.Repository, tcs);
            }

            object[] reviews =
                alreadyReviewed ?
                [
                    new
                    {
                        author_association = "CONTRIBUTOR",
                        state = "APPROVED",
                        user = new
                        {
                            login = "costellobot[bot]",
                            type = "User",
                        },
                    },
                ]
                :
                [];

            RegisterCommitAndDiff(pull);
            RegisterDependabotConfiguration(pull.Repository, pull.PullRequest.RefHead);
            RegisterDependabotIssues(pull.Repository, [pull.PullRequest.ToIssue()]);
            RegisterRepository(pull.Repository);
            RegisterReviews(pull, reviews);

            var approved = RegisterReview(pull);
            var automerge = RegisterEnableAutomerge(pull);

            _ = Task.Run(
                async () =>
                {
                    await Task.WhenAll(approved.Task, automerge.Task);
                    tcs.TrySetResult();
                },
                CancellationToken);

            return (pull.Repository, tcs);
        }
    }

    [Theory]
    [InlineData("changes_requested")]
    [InlineData("commented")]
    [InlineData("dismissed")]
    public async Task Trust_Store_Is_Not_Updated_For_Pull_Request_Review_That_Does_Not_Approve(string reviewState)
    {
        // Arrange
        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency, reviewState: reviewState);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Trust_Store_Is_Not_Updated_For_Pull_Request_From_Untrusted_User()
    {
        // Arrange
        var dependency = ("foo", "1.2.3");

        var driver = await ConfigureAsync(
            dependency,
            pullRequestAuthorAssociation: "NONE",
            pullRequestAuthorLogin: "rando-calrissian");

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Trust_Store_Is_Not_Updated_For_Trusted_User_With_No_Dependencies_Detected()
    {
        // Arrange
        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency);

        driver.WithCommitMessage("Updated something\n\nNo dependency information.");

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Trust_Store_Is_Not_Updated_For_Pull_Request_From_Owner()
    {
        // Arrange
        var dependency = ("foo", "1.2.3");

        var driver = await ConfigureAsync(
            dependency,
            pullRequestAuthorAssociation: "OWNER");

        driver.PullRequest.User = driver.Repository.Owner;

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Theory]
    [InlineData("dismissed")]
    [InlineData("edited")]
    public async Task Trust_Store_Is_Not_Updated_For_Ignored_Action(string action)
    {
        // Arrange
        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver, action);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Trust_Store_Is_Not_Updated_For_Draft()
    {
        // Arrange
        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        driver.PullRequest.IsDraft = true;

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Trust_Store_Is_Not_Updated_For_Approved_Review_Not_From_Owner()
    {
        // Arrange
        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency, reviewAuthorAssociation: "NONE");

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Trust_Store_Is_Not_Updated_For_Approved_Review_From_Trusted_User()
    {
        // Arrange
        var review = new
        {
            author_association = "CONTRIBUTOR",
            state = "APPROVED",
            user = new
            {
                login = "costellobot[bot]",
                type = "Bot",
            },
        };

        var dependency = ("foo", "1.2.3");

        var driver = await ConfigureAsync(
            dependency,
            reviews: [review]);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Theory]
    [InlineData("costellobot[bot]", "CONTRIBUTOR")]
    [InlineData("martincostello", "OWNER")]
    public async Task Trust_Store_Is_Not_Updated_For_Approved_Review_By_Pull_Request_Author(
        string authorLogin,
        string authorAssociation)
    {
        // Arrange
        var dependency = ("foo", "1.2.3");

        var driver = await ConfigureAsync(
            dependency,
            pullRequestAuthorAssociation: authorAssociation,
            pullRequestAuthorLogin: authorLogin,
            reviewAuthorAssociation: authorAssociation,
            reviewAuthorLogin: authorLogin);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Trust_Store_Is_Not_Updated_When_Disabled_In_Configuration()
    {
        // Arrange
        var dependency = ("foo", "1.2.3");
        var driver = await ConfigureAsync(dependency, isEnabled: false);

        var trustStoreUpdated = RegisterTrusted(DependencyEcosystem.NuGet, dependency);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertNotTrustedAsync(trustStoreUpdated);
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Pull_Requests_Reviews()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<PullRequestReviewHandler>();
        var message = new Octokit.Webhooks.Events.IssueComment.IssueCommentCreatedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message));
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (Fixture.Services.GetRequiredService<ITrustStore>() is InMemoryTrustStore trustStore)
        {
            await trustStore.DistrustAllAsync();
        }

        await base.DisposeAsync(disposing);
    }

    private async Task<PullRequestReviewDriver> ConfigureAsync(
        (string Id, string Version) dependency,
        string reviewAuthorAssociation = "OWNER",
        string reviewAuthorLogin = "martincostello",
        string reviewState = "approved",
        string? pullRequestAuthorAssociation = null,
        string? pullRequestAuthorLogin = null,
        bool isCollaborator = true,
        bool isEnabled = true,
        IEnumerable<object>? reviews = null,
        IEnumerable<RepositoryBuilder>? installationRepositories = null)
    {
        Fixture.ImplicitTrustEnabled(isEnabled);

        var driver = PullRequestReviewDriver.FromUserForDependabot(reviewAuthorLogin);

        driver.WithAuthorAssociation(reviewAuthorAssociation)
              .WithState(reviewState)
              .WithCommitMessage(TrustedCommitMessage(dependency.Id, dependency.Version));

        if (pullRequestAuthorAssociation is { Length: > 0 })
        {
            driver.PullRequest.AuthorAssociation = pullRequestAuthorAssociation;
        }

        if (pullRequestAuthorLogin is { Length: > 0 })
        {
            driver.PullRequest.User ??= CreateUser(pullRequestAuthorLogin);
            driver.PullRequest.User.Login = pullRequestAuthorLogin;
        }

        RegisterGetAccessToken();
        RegisterCollaborator(driver, driver.Sender.Login, isCollaborator);
        RegisterCommitAndDiff(driver);
        RegisterDependabotConfiguration(driver.Repository, driver.PullRequest.RefHead);
        RegisterDependabotIssues(driver.Repository, [driver.PullRequest.ToIssue()]);
        RegisterGitHubApp(driver.Owner);
        RegisterInstallationRepositories(installationRepositories ?? [driver.Repository]);
        RegisterRepository(driver.Repository);
        RegisterReviews(driver, reviews ?? []);

        await Fixture.Interceptor.RegisterBundleAsync(
            Path.Combine("Bundles", "nuget-search.json"),
            cancellationToken: TestContext.Current.CancellationToken);

        return driver;
    }

    private async Task AssertNotTrustedAsync(TaskCompletionSource taskCompletionSource)
    {
        await AssertTaskNotRun(taskCompletionSource);

        if (Fixture.Services.GetRequiredService<ITrustStore>() is InMemoryTrustStore trustStore)
        {
            trustStore.Count.ShouldBe(0);
        }

        if (Fixture.Services.GetRequiredService<IMemoryCache>() is MemoryCache cache)
        {
            cache.Clear();
        }
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(PullRequestDriver driver, string action = "submitted")
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("pull_request_review", value);
    }

    private void RegisterDependabotIssues(RepositoryBuilder repository, IEnumerable<IssueBuilder> issues)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repositories/{repository.Id}/issues")
            .ForQuery("creator=app%2Fdependabot&filter=created&state=open&sort=created&direction=desc&per_page=100")
            .Responds()
            .WithJsonContent(issues)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGitHubApp(UserBuilder owner)
    {
        var app = new GitHubAppBuilder("costellobot", owner);

        CreateDefaultBuilder()
            .Requests()
            .ForPath("/app")
            .Responds()
            .WithJsonContent(app)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterInstallationRepositories(IEnumerable<RepositoryBuilder> repositories)
    {
        var builder = new InstallationRepositoriesBuilder(repositories);

        CreateDefaultBuilder()
            .Requests()
            .ForPath("/installation/repositories")
            .ForQuery("per_page=100")
            .Responds()
            .WithJsonContent(builder)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterRepository(RepositoryBuilder repository)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repositories/{repository.Id}")
            .Responds()
            .WithJsonContent(repository)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterReviews(PullRequestDriver driver, IEnumerable<object> reviews)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.FullName}/pulls/{driver.PullRequest.Number}/reviews")
            .Responds()
            .WithJsonContent(reviews.ToArray())
            .RegisterWith(Fixture.Interceptor);
    }

    private TaskCompletionSource RegisterTrusted(DependencyEcosystem ecosystem, (string Id, string Version) dependency)
        => RegisterTrusted(ecosystem, [dependency]);

    private TaskCompletionSource RegisterTrusted(
        DependencyEcosystem ecosystem,
        ICollection<(string Id, string Version)> dependencies)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var delay = TimeSpan.FromSeconds(0.1);

        var trustStore = Fixture.Services.GetRequiredService<ITrustStore>();
        var trustStoreUpdated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool allTrusted = true;

                    foreach ((var id, var version) in dependencies)
                    {
                        allTrusted &= await trustStore.IsTrustedAsync(ecosystem, id, version, cancellationToken);
                    }

                    if (allTrusted)
                    {
                        trustStoreUpdated.TrySetResult();
                    }

                    await Task.Delay(delay, cancellationToken);
                }
            },
            cancellationToken);

        return trustStoreUpdated;
    }
}
