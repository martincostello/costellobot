// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace MartinCostello.Costellobot;

public static class ConfigurationSchemaTests
{
    [Fact]
    public static async Task Application_Configuration_Is_Valid()
    {
        // Arrange
        var schemaJson = await GetResourceAsStringAsync("appsettings.schema.json", TestContext.Current.CancellationToken);
        var configurationJson = await GetResourceAsStringAsync("appsettings.json", TestContext.Current.CancellationToken);

        var schema = JSchema.Parse(
            schemaJson,
            new JSchemaReaderSettings() { ValidateVersion = true });

        var configuration = JToken.Parse(configurationJson);

        // Act
        var actual = configuration.IsValid(schema, out IList<ValidationError> errors);

        // Assert
        errors.ShouldNotBeNull();
        errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors.Select((p) => FormatValidationError(p))));
        actual.ShouldBeTrue();

        AssertJson(configurationJson);

        static string FormatValidationError(ValidationError error, string indent = "")
        {
            var builder = new StringBuilder();

            Format(error, builder, indent);

            return builder.ToString();

            static void Format(ValidationError error, StringBuilder builder, string indent = "")
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"{indent} Message: {error.Message}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}    Path: {error.Path}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}SchemaId: {error.SchemaId}");

                if (error.Value != null)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"{indent}   Value: {error.Value}");
                }

                foreach (var child in error.ChildErrors)
                {
                    Format(child, builder, indent + "  ");
                }
            }
        }

        static async Task<string> GetResourceAsStringAsync(string fileName, CancellationToken cancellationToken)
        {
            var assembly = typeof(ConfigurationSchemaTests).Assembly;
            await using var stream = assembly.GetManifestResourceStream($"MartinCostello.Costellobot.{fileName}")!;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }

    private static void AssertJson(string json)
        => Should.NotThrow(() => System.Text.Json.JsonDocument.Parse(json), "Invalid JSON configuration.");
}
