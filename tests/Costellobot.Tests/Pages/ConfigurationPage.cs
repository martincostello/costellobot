// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public sealed class ConfigurationPage(IPage page) : AppPage(page)
{
    public async Task<IReadOnlyList<string>> GetGitHubTokenBrokerRepositoriesAsync()
    {
        var elements = await Page.QuerySelectorAllAsync(Selectors.GitHubTokenBrokerRepositoryTab);
        return [.. await Task.WhenAll(elements.Select((p) => p.InnerTextAsync()))];
    }

    public async Task<IReadOnlyList<TokenBrokerProfileItem>> GetGitHubTokenBrokerProfilesAsync(string repository)
    {
        await SelectGitHubTokenBrokerRepositoryAsync(repository);

        var elements = await Page.QuerySelectorAllAsync($"{TokenBrokerRepositoryPaneSelector(repository)} {Selectors.TokenBrokerProfile}");
        return [.. elements.Select((p) => new TokenBrokerProfileItem(p, Page))];
    }

    public async Task<IReadOnlyList<(string Ecosystem, string Publisher, string? Url)>> GetTrustedPublishersAsync()
    {
        var items = await Page.QuerySelectorAllAsync(Selectors.TrustedPublisherItem);
        var publishers = new List<(string Ecosystem, string Publisher, string? Url)>(items.Count);

        foreach (var item in items)
        {
            var ecosystem = await (await item.QuerySelectorAsync(Selectors.TrustedPublisherEcosystem))!.GetAttributeAsync("title");
            var publisher = await (await item.QuerySelectorAsync(Selectors.TrustedPublisher))!.InnerTextAsync();
            var link = await item.QuerySelectorAsync(Selectors.TrustedPublisherLink);
            var url = link is null ? null : await link.GetAttributeAsync("href");

            publishers.Add((ecosystem!, publisher.Trim(), url));
        }

        return publishers;
    }

    public async Task<bool> GitHubTokenBrokerEnabledAsync()
        => await Page.IsCheckedAsync(Selectors.GitHubTokenBrokerEnabled);

    public async Task<string> GitHubTokenBrokerVaultUriAsync()
        => await Page.InnerTextAsync(Selectors.GitHubTokenBrokerVaultUri);

    public async Task OpenGitHubTokenBrokerAsync()
    {
        await Page.ClickAsync(Selectors.GitHubTokenBrokerTab);
        await Assertions.Expect(Page.Locator(Selectors.GitHubTokenBrokerPane))
                        .ToBeVisibleAsync();
    }

    public async Task OpenTrustedPublishersAsync()
    {
        await Page.ClickAsync(Selectors.TrustedEntitiesTab);
        await Assertions.Expect(Page.Locator(Selectors.TrustedEntitiesPane))
                        .ToBeVisibleAsync();

        await Page.ClickAsync(Selectors.TrustedPublishersTab);
        await Assertions.Expect(Page.Locator(Selectors.TrustedPublishersPane))
                        .ToBeVisibleAsync();
    }

    public async Task SelectGitHubTokenBrokerRepositoryAsync(string repository)
    {
        await Page.ClickAsync(TokenBrokerRepositoryTabSelector(repository));
        await Assertions.Expect(Page.Locator(TokenBrokerActiveRepositoryPaneSelector(repository)))
                        .ToBeVisibleAsync();
    }

    public async Task WaitForContentAsync()
        => await Page.WaitForSelectorAsync(Selectors.ConfigurationContent);

    private static string TokenBrokerActiveRepositoryPaneSelector(string repository)
        => $"{TokenBrokerRepositoryPaneSelector(repository)}.active";

    private static string TokenBrokerRepositoryPaneSelector(string repository)
        => $"{Selectors.TokenBrokerRepositoryPane}[data-repository=\"{repository}\"]";

    private static string TokenBrokerRepositoryTabSelector(string repository)
        => $"{Selectors.GitHubTokenBrokerRepositoryTab}[data-repository=\"{repository}\"]";

    private sealed class Selectors
    {
        internal const string ConfigurationContent = "id=configuration-content";
        internal const string GitHubTokenBrokerEnabled = "id=token-broker-enabled";
        internal const string GitHubTokenBrokerPane = "id=tab-github-token-broker";
        internal const string GitHubTokenBrokerRepositoryTab = ".token-broker-repository-tab";
        internal const string GitHubTokenBrokerTab = "id=settings-tab-github-token-broker";
        internal const string GitHubTokenBrokerVaultUri = "id=token-broker-vault-uri";
        internal const string TokenBrokerProfile = ".token-broker-profile";
        internal const string TokenBrokerProfileName = ".token-broker-profile-name";
        internal const string TokenBrokerProfileOptionName = "tbody > tr > td:first-child";
        internal const string TokenBrokerProfileTokenId = ".token-broker-profile-token-id";
        internal const string TokenBrokerProfileType = ".token-broker-profile-type";
        internal const string TokenBrokerRepositoryPane = ".token-broker-repository-pane";
        internal const string TrustedEntitiesPane = "id=tab-trusted-entities";
        internal const string TrustedEntitiesTab = "id=settings-tab-trusted-entities";
        internal const string TrustedPublisher = ".trusted-publisher";
        internal const string TrustedPublisherEcosystem = ".dependency-ecosystem";
        internal const string TrustedPublisherItem = ".trusted-publisher-item";
        internal const string TrustedPublisherLink = ".trusted-publisher-link";
        internal const string TrustedPublishersPane = "id=tab-trusted-entities-publishers";
        internal const string TrustedPublishersTab = "id=settings-tab-trusted-entities-publishers";
    }
}
