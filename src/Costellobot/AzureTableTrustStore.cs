// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using Azure;
using Azure.Data.Tables;
using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot;

public sealed class AzureTableTrustStore(TableServiceClient client) : ITrustStore
{
    private const string DenyTableName = "DenyStore";
    private const string TrustTableName = "TrustStore";

    /// <inheritdoc/>
    public async Task AllowAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default) =>
        await DeleteAsync(DenyTableName, ecosystem, id, version, cancellationToken);

    /// <inheritdoc/>
    public async Task DenyAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default) =>
        await UpsertAsync(DenyTableName, ecosystem, id, version, cancellationToken);

    /// <inheritdoc/>
    public async Task DistrustAllAsync(CancellationToken cancellationToken = default)
    {
        const int BatchSize = 100;
        const int PageSize = 1_000;

        var table = GetClient(TrustTableName);

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
        CancellationToken cancellationToken = default) =>
        await DeleteAsync(TrustTableName, ecosystem, id, version, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeniedDependency>> GetDeniedAsync(
        DependencyEcosystem ecosystem,
        CancellationToken cancellationToken = default) =>
        await GetAsync(
            DenyTableName,
            ecosystem,
            (item) =>
            {
                return new DeniedDependency(item.DependencyId, item.DependencyVersion)
                {
                    DeniedAt = item.Timestamp,
                };
            },
            cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TrustedDependency>> GetTrustAsync(
        DependencyEcosystem ecosystem,
        CancellationToken cancellationToken = default) =>
        await GetAsync(
            TrustTableName,
            ecosystem,
            (item) =>
            {
                return new TrustedDependency(item.DependencyId, item.DependencyVersion)
                {
                    TrustedAt = item.Timestamp,
                };
            },
            cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> IsDeniedAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default) =>
        await ExistsAsync(DenyTableName, ecosystem, id, version, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> IsTrustedAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default) =>
        await ExistsAsync(TrustTableName, ecosystem, id, version, cancellationToken);

    /// <inheritdoc/>
    public async Task TrustAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default) =>
        await UpsertAsync(TrustTableName, ecosystem, id, version, cancellationToken);

    private static (string PartitionKey, string RowKey) GetKeys(DependencyEcosystem ecosystem, string id, string version)
    {
        var partitionKey = ecosystem.ToString().ToUpperInvariant();

        var normalizedId = id.ToUpperInvariant().Replace('/', '~').Trim();
        var normalizedVersion = version.ToUpperInvariant().Trim();

        return (partitionKey, $"{normalizedId}@{normalizedVersion}");
    }

    private TableClient GetClient(string tableName) => client.GetTableClient(tableName);

    private async Task DeleteAsync(
        string tableName,
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        (string partition, string row) = GetKeys(ecosystem, id, version);

        var table = GetClient(tableName);
        await table.DeleteEntityAsync(partition, row, cancellationToken: cancellationToken);
    }

    private async Task<bool> ExistsAsync(
        string tableName,
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        (string partition, string row) = GetKeys(ecosystem, id, version);

        var table = GetClient(tableName);
        var deny = await table.GetEntityIfExistsAsync<TrustEntity>(partition, row, cancellationToken: cancellationToken);

        return deny.HasValue;
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(
        string tableName,
        DependencyEcosystem ecosystem,
        Func<TrustEntity, T> selector,
        CancellationToken cancellationToken)
    {
        var ecosystemName = ecosystem.ToString();

        var table = GetClient(tableName);
        var query = table.QueryAsync<TrustEntity>((p) => p.DependencyEcosystem == ecosystemName, cancellationToken: cancellationToken);

        var results = new List<T>();

        await foreach (var page in query.AsPages().WithCancellation(cancellationToken))
        {
            foreach (var item in page.Values)
            {
                results.Add(selector(item));
            }
        }

        return results;
    }

    private async Task UpsertAsync(
        string tableName,
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken)
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

        var table = GetClient(tableName);
        await table.UpsertEntityAsync(entity, cancellationToken: cancellationToken);
    }

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
        public DateTimeOffset? Timestamp { get; set; }

        /// <inheritdoc/>
        public ETag ETag { get; set; }
    }
}
