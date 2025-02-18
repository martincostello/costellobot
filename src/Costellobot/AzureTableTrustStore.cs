// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;

namespace MartinCostello.Costellobot;

public sealed class AzureTableTrustStore(TableServiceClient client) : ITrustStore
{
    private const string TableName = "TrustStore";

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
    public async Task<IReadOnlyList<KeyValuePair<string, string>>> GetTrustAsync(
        DependencyEcosystem ecosystem,
        CancellationToken cancellationToken = default)
    {
        var ecosystemName = ecosystem.ToString();

        var table = GetClient();
        var query = table.QueryAsync<TrustEntity>((p) => p.DependencyEcosystem == ecosystemName, cancellationToken: cancellationToken);

        var results = new List<KeyValuePair<string, string>>();

        await foreach (var page in query.AsPages().WithCancellation(cancellationToken))
        {
            foreach (var item in page.Values)
            {
                results.Add(KeyValuePair.Create(item.DependencyId, item.DependencyVersion));
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
        => (ecosystem.ToString().ToUpperInvariant(), $"{id.ToUpperInvariant()}@{version.ToUpperInvariant()}");

    private TableClient GetClient() => client.GetTableClient(TableName);

    /// <summary>
    /// A class representing an entity in the trust store. This class cannot be inherited.
    /// </summary>
    private sealed class TrustEntity : ITableEntity
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
