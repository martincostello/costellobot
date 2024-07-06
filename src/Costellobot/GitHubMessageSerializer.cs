// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Primitives;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubMessageSerializer
{
    private const string Brotli = "br";
    private const long MaxLength = 256 * 1024;

    private static readonly string Publisher = $"costellobot/{GitMetadata.Version}";

    public static (IDictionary<string, StringValues> Headers, string Body) Deserialize(ServiceBusReceivedMessage message)
    {
        GitHubMessage payload;

        if (GetEncoding(message) is Brotli)
        {
            using var compressed = message.Body.ToStream();
            using var utf8Json = Decompress(compressed);

            payload = JsonSerializer.Deserialize(utf8Json, MessagingJsonSerializerContext.Default.GitHubMessage)!;
        }
        else
        {
            payload = JsonSerializer.Deserialize(message.Body, MessagingJsonSerializerContext.Default.GitHubMessage)!;
        }

        var headers = new Dictionary<string, StringValues>(payload.Headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((var key, var values) in payload.Headers)
        {
            headers[key] = new(values);
        }

        return (headers, payload.Body);

        static Stream Decompress(Stream input)
        {
            var output = new MemoryStream((int)input.Length);

            using (var decompressor = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true))
            {
                decompressor.CopyTo(output);
            }

            output.Seek(0, SeekOrigin.Begin);

            return output;
        }
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

        (var encoded, var encoding) = Encode(payload);
        var message = new ServiceBusMessage(encoded)
        {
            ContentType = GitHubMessage.ContentType,
            CorrelationId = Activity.Current?.Id,
            MessageId = deliveryId,
            Subject = GitHubMessage.Subject,
        };

        if (encoding is not null)
        {
            message.GetRawAmqpMessage().Properties.ContentEncoding = encoding;
        }

        message.ApplicationProperties["publisher"] = Publisher;

        return message;
    }

    private static (BinaryData Body, string? ContentEncoding) Encode(GitHubMessage payload)
    {
        using var utf8Json = new MemoryStream();

        JsonSerializer.Serialize(utf8Json, payload, MessagingJsonSerializerContext.Default.GitHubMessage);

        if (utf8Json.Length <= MaxLength)
        {
            return (BinaryData.FromBytes(utf8Json.ToArray()), null);
        }

        utf8Json.Seek(0, SeekOrigin.Begin);

        using var compressed = new MemoryStream();
        using (var compressor = new BrotliStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            utf8Json.CopyTo(compressor);
        }

        compressed.Seek(0, SeekOrigin.Begin);

        return (BinaryData.FromStream(compressed), Brotli);
    }

    private static string? GetEncoding(ServiceBusReceivedMessage message)
    {
        string? contentEncoding = message.GetRawAmqpMessage().Properties.ContentEncoding;

        if (string.IsNullOrEmpty(contentEncoding))
        {
            return null;
        }

        if (!string.Equals(contentEncoding, Brotli, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Message with ID {message.MessageId} has an invalid content encoding: {contentEncoding}.");
        }

        return Brotli;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(GitHubMessage))]
    [JsonSourceGenerationOptions(WriteIndented = false)]
    private sealed partial class MessagingJsonSerializerContext : JsonSerializerContext
    {
    }
}
