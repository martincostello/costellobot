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
    }
}
