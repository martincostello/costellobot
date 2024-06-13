// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

/// <summary>
/// A record representing a GitHub repository issue (or pull request) ID.
/// </summary>
/// <param name="Repository">The repository associated with the issue.</param>
/// <param name="Number">The number of the issue or pull request.</param>
public sealed record IssueId(RepositoryId Repository, int Number)
{
    /// <summary>
    /// Gets the repository owner.
    /// </summary>
    public string Owner => Repository.Owner;

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string Name => Repository.Name;

    /// <summary>
    /// Creates a new instance of <see cref="IssueId"/> for the specified repository and issue number.
    /// </summary>
    /// <param name="repository">The GitHub repository.</param>
    /// <param name="number">The number of the issue or pull request.</param>
    /// <returns>
    /// The <see cref="IssueId"/> associated with <paramref name="repository"/> and <paramref name="number"/>.
    /// </returns>
    public static IssueId Create(Octokit.Webhooks.Models.Repository repository, long number)
        => new(RepositoryId.Create(repository), (int)number);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"{Repository.FullName}#{Number}");
}
