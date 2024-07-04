// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Primitives;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubMessageSerializer
{
    private static readonly string Publisher = $"costellobot/{GitMetadata.Version}";

    public static (IDictionary<string, StringValues> Headers, string Body) Deserialize(ServiceBusReceivedMessage message)
    {
        var payload = JsonSerializer.Deserialize(message.Body, MessagingJsonSerializerContext.Default.GitHubMessage)!;

        var headers = new Dictionary<string, StringValues>(payload.Headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((var key, var values) in payload.Headers)
        {
            headers[key] = new(values);
        }

        return (headers, payload.Body);
    }

    public static ServiceBusMessage Serialize(string? deliveryId, IDictionary<string, string> headers, string body)
    {
        var messageHeaders = new Dictionary<string, string?[]?>(headers.Count);

        foreach ((var key, var value) in headers)
        {
            messageHeaders[key] = [value];
        }

        var payload = new GitHubMessage()
        {
            Headers = messageHeaders,
            Body = body,
        };

        var utf8Json = JsonSerializer.SerializeToUtf8Bytes(payload, MessagingJsonSerializerContext.Default.GitHubMessage);
        var message = new ServiceBusMessage(utf8Json)
        {
            ContentType = GitHubMessage.ContentType,
            CorrelationId = Activity.Current?.Id,
            MessageId = deliveryId,
            Subject = GitHubMessage.Subject,
        };

        message.ApplicationProperties["publisher"] = Publisher;

        return message;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(GitHubMessage))]
    private sealed partial class MessagingJsonSerializerContext : JsonSerializerContext
    {
    }
}
