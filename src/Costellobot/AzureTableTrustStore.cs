// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using Azure;
using Azure.Data.Tables;
using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot;

public sealed class AzureTableTrustStore(TableServiceClient client) : ITrustStore
{
    private const string TableName = "TrustStore";

    /// <inheritdoc/>
    public async Task DistrustAllAsync(CancellationToken cancellationToken = default)
    {
        const int BatchSize = 100;
        const int PageSize = 1_000;

        var table = GetClient();

        // Adapted from https://medium.com/medialesson/deleting-all-rows-from-azure-table-storage-as-fast-as-possible-79e03937c331
        var query = table.QueryAsync<TrustEntity>(
            select: ["PartitionKey", "RowKey"],
            maxPerPage: PageSize,
            cancellationToken: cancellationToken);

        await foreach (var page in query.AsPages().WithCancellation(cancellationToken))
        {
            foreach (var items in page.Values.GroupBy((p) => p.PartitionKey))
            {
                foreach (var chunk in items.Chunk(BatchSize))
                {
                    var actions = chunk.Select((p) => new TableTransactionAction(TableTransactionActionType.Delete, p));
                    await table.SubmitTransactionAsync(actions, cancellationToken);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task DistrustAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default)
    {
        (string partition, string row) = GetKeys(ecosystem, id, version);

        var table = GetClient();
        await table.DeleteEntityAsync(partition, row, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TrustedDependency>> GetTrustAsync(
        DependencyEcosystem ecosystem,
        CancellationToken cancellationToken = default)
    {
        var ecosystemName = ecosystem.ToString();

        var table = GetClient();
        var query = table.QueryAsync<TrustEntity>((p) => p.DependencyEcosystem == ecosystemName, cancellationToken: cancellationToken);

        var results = new List<TrustedDependency>();

        await foreach (var page in query.AsPages().WithCancellation(cancellationToken))
        {
            foreach (var item in page.Values)
            {
                var dependency = new TrustedDependency(item.DependencyId, item.DependencyVersion)
                {
                    TrustedAt = item.Timestamp,
                };

                results.Add(dependency);
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<bool> IsTrustedAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default)
    {
        (string partition, string row) = GetKeys(ecosystem, id, version);

        var table = GetClient();
        var trust = await table.GetEntityIfExistsAsync<TrustEntity>(partition, row, cancellationToken: cancellationToken);

        return trust.HasValue;
    }

    /// <inheritdoc/>
    public async Task TrustAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{id}@{version}"));
        var etag = Convert.ToBase64String(hash);

        (string partition, string row) = GetKeys(ecosystem, id, version);

        var entity = new TrustEntity()
        {
            DependencyEcosystem = ecosystem.ToString(),
            DependencyId = id,
            DependencyVersion = version,
            ETag = new(etag),
            PartitionKey = partition,
            RowKey = row,
        };

        var table = GetClient();
        await table.UpsertEntityAsync(entity, cancellationToken: cancellationToken);
    }

    private static (string PartitionKey, string RowKey) GetKeys(DependencyEcosystem ecosystem, string id, string version)
    {
        var partitionKey = ecosystem.ToString().ToUpperInvariant();

        var normalizedId = id.ToUpperInvariant().Replace('/', '~');
        var normalizedVersion = version.ToUpperInvariant();

        return (partitionKey, $"{normalizedId}@{normalizedVersion}");
    }

    private TableClient GetClient() => client.GetTableClient(TableName);

    /// <summary>
    /// A class representing an entity in the trust store. This class cannot be inherited.
    /// </summary>
    public sealed class TrustEntity : ITableEntity
    {
        public string DependencyEcosystem { get; set; } = default!;

        public string DependencyId { get; set; } = default!;

        public string DependencyVersion { get; set; } = default!;

        /// <inheritdoc/>
        public string PartitionKey { get; set; } = default!;

        /// <inheritdoc/>
        public string RowKey { get; set; } = default!;

        /// <inheritdoc/>
        public DateTimeOffset? Timestamp { get; set; } = default!;

        /// <inheritdoc/>
        public ETag ETag { get; set; } = default!;
    }
}
