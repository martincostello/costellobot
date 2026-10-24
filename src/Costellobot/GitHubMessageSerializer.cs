// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Buffers;
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
    private const string Zstandard = "zstd";
    private const int MaxLength = 256 * 1024;

    private static readonly string Publisher = $"costellobot/{GitMetadata.Version}";

    public static (IDictionary<string, StringValues> Headers, string Body) Deserialize(ServiceBusReceivedMessage message)
    {
        GitHubMessage payload;

        if (GetEncoding(message) is Zstandard)
        {
            using var compressed = message.Body.ToStream();
            using var utf8Json = DecompressZstandard(compressed);

            payload = JsonSerializer.Deserialize(utf8Json, MessagingJsonSerializerContext.Default.GitHubMessage)!;
        }
        else if (GetEncoding(message) is Brotli)
        {
            using var compressed = message.Body.ToStream();
            using var utf8Json = DecompressBrotli(compressed);

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

        static Stream DecompressBrotli(Stream input)
        {
            var output = new MemoryStream((int)input.Length);

            using (var decompressor = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true))
            {
                decompressor.CopyTo(output);
            }

            output.Seek(0, SeekOrigin.Begin);

            return output;
        }

        static Stream DecompressZstandard(Stream input)
        {
            var output = new MemoryStream((int)input.Length);

            using (var decompressor = new ZstandardStream(input, CompressionMode.Decompress, leaveOpen: true))
            {
                decompressor.CopyTo(output);
            }

            output.Seek(0, SeekOrigin.Begin);

            return output;
        }
    }

    public static ServiceBusMessage? Serialize(string? deliveryId, IDictionary<string, string> headers, string body, string compression)
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

        (var encoded, var encoding) = Encode(payload, compression);

        if (encoded is null)
        {
            return null;
        }

        var message = new ServiceBusMessage(encoded)
        {
            ContentType = GitHubMessage.ContentType,
            CorrelationId = Activity.Current?.Id,
            Subject = GitHubMessage.Subject,
        };

        if (deliveryId is { Length: > 0 })
        {
            message.MessageId = deliveryId;
        }

        if (encoding is not null)
        {
            message.GetRawAmqpMessage().Properties.ContentEncoding = encoding;
        }

        message.ApplicationProperties["publisher"] = Publisher;

        return message;
    }

    private static (BinaryData? Body, string? ContentEncoding) Encode(GitHubMessage payload, string compression)
    {
        var utf8Json = JsonSerializer.SerializeToUtf8Bytes(payload, MessagingJsonSerializerContext.Default.GitHubMessage);

        if (utf8Json.Length <= MaxLength)
        {
            return (BinaryData.FromBytes(utf8Json), null);
        }

        return compression switch
        {
            Brotli => EncodeBrotli(utf8Json),
            Zstandard => EncodeZstandard(utf8Json),
            _ => (BinaryData.FromBytes(utf8Json), null),
        };
    }

    private static (BinaryData? Body, string? ContentEncoding) EncodeBrotli(ReadOnlySpan<byte> utf8Json)
    {
        var rented = ArrayPool<byte>.Shared.Rent(MaxLength);

        try
        {
            if (!BrotliEncoder.TryCompress(utf8Json, rented.AsSpan(0, MaxLength), out var bytesWritten))
            {
                return (null, null);
            }

            return (BinaryData.FromBytes(rented.AsSpan(0, bytesWritten).ToArray()), Brotli);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static (BinaryData? Body, string? ContentEncoding) EncodeZstandard(ReadOnlySpan<byte> utf8Json)
    {
        var rented = ArrayPool<byte>.Shared.Rent(MaxLength);

        try
        {
            if (!ZstandardEncoder.TryCompress(utf8Json, rented.AsSpan(0, MaxLength), out var bytesWritten))
            {
                return (null, null);
            }

            return (BinaryData.FromBytes(rented.AsSpan(0, bytesWritten).ToArray()), Zstandard);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static string? GetEncoding(ServiceBusReceivedMessage message)
    {
        string? contentEncoding = message.GetRawAmqpMessage().Properties.ContentEncoding;

        if (string.IsNullOrEmpty(contentEncoding))
        {
            return null;
        }

        if (string.Equals(contentEncoding, Brotli, StringComparison.OrdinalIgnoreCase))
        {
            return Brotli;
        }

        if (string.Equals(contentEncoding, Zstandard, StringComparison.OrdinalIgnoreCase))
        {
            return Zstandard;
        }

        throw new InvalidOperationException($"Message with ID {message.MessageId} has an invalid content encoding: {contentEncoding}.");
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(GitHubMessage))]
    [JsonSourceGenerationOptions(WriteIndented = false)]
    private sealed partial class MessagingJsonSerializerContext : JsonSerializerContext;
}
