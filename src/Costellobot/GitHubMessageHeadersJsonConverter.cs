// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace MartinCostello.Costellobot;

/// <summary>
/// A JSON converter for the HTTP headers of a <see cref="GitHubMessage"/>.
/// </summary>
/// <remarks>
/// Headers are serialized as an object whose values are arrays of strings. Reading
/// them directly into <see cref="StringValues"/> avoids allocating an intermediate
/// array for every header, and creates the dictionary with a case-insensitive comparer.
/// </remarks>
internal sealed class GitHubMessageHeadersJsonConverter : JsonConverter<Dictionary<string, StringValues>>
{
    public override Dictionary<string, StringValues> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected {JsonTokenType.StartObject} but found {reader.TokenType}.");
        }

        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;

            reader.Read();

            headers[name] = ReadValues(ref reader);
        }

        if (reader.TokenType is not JsonTokenType.EndObject)
        {
            throw new JsonException($"Expected {JsonTokenType.EndObject} but found {reader.TokenType}.");
        }

        return headers;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, StringValues> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach ((var name, var values) in value)
        {
            writer.WritePropertyName(name);
            writer.WriteStartArray();

            foreach (string? item in values)
            {
                writer.WriteStringValue(item);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static StringValues ReadValues(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return StringValues.Empty;
        }

        if (reader.TokenType is JsonTokenType.String)
        {
            return new(reader.GetString());
        }

        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected {JsonTokenType.StartArray} but found {reader.TokenType}.");
        }

        reader.Read();

        if (reader.TokenType is JsonTokenType.EndArray)
        {
            return StringValues.Empty;
        }

        string? first = ReadString(ref reader);
        reader.Read();

        if (reader.TokenType is JsonTokenType.EndArray)
        {
            // The common case of a header with a single value
            return new(first);
        }

        var values = new List<string?>(2) { first };

        do
        {
            values.Add(ReadString(ref reader));
            reader.Read();
        }
        while (reader.TokenType is not JsonTokenType.EndArray);

        return new([.. values]);

        static string? ReadString(ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Expected {JsonTokenType.String} but found {reader.TokenType}."),
            };
        }
    }
}
