// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

/// <summary>
/// A record representing a GitHub repository ID.
/// </summary>
/// <param name="Owner">The login of the owner of the repository.</param>
/// <param name="Name">The name of the repository.</param>
public sealed record RepositoryId(string Owner, string Name)
{
    /// <summary>
    /// Gets the full name of the repository.
    /// </summary>
    public string FullName => $"{Owner}/{Name}";

    /// <summary>
    /// Creates a new instance of <see cref="RepositoryId"/> for the specified repository.
    /// </summary>
    /// <param name="repository">The GitHub repository.</param>
    /// <returns>
    /// The <see cref="RepositoryId"/> associated with <paramref name="repository"/>.
    /// </returns>
    public static RepositoryId Create(Octokit.Webhooks.Models.Repository repository)
        => new(repository.Owner.Login, repository.Name);

    /// <inheritdoc/>
    public override string ToString() => FullName;
}
