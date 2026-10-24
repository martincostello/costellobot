// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Primitives;

namespace MartinCostello.Costellobot;

public static class GitHubMessageHeadersJsonConverterTests
{
    [Fact]
    public static void Read_Headers_With_Single_Values()
    {
        // Arrange
        string json =
            """
            {
              "Content-Type": ["application/json"],
              "X-GitHub-Delivery": ["9b5c943d-187a-4988-b3ca-9993d5268f85"],
              "X-GitHub-Event": ["ping"]
            }
            """;

        // Act
        var actual = Read(json);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(3);
        actual.ShouldContainKeyAndValue("Content-Type", new StringValues("application/json"));
        actual.ShouldContainKeyAndValue("X-GitHub-Delivery", new StringValues("9b5c943d-187a-4988-b3ca-9993d5268f85"));
        actual.ShouldContainKeyAndValue("X-GitHub-Event", new StringValues("ping"));
        actual["X-GitHub-Event"].Count.ShouldBe(1);
    }

    [Fact]
    public static void Read_Headers_Is_Case_Insensitive()
    {
        // Arrange
        string json = """{"X-GitHub-Event":["ping"]}""";

        // Act
        var actual = Read(json);

        // Assert
        actual.Comparer.ShouldBe(StringComparer.OrdinalIgnoreCase);
        actual.TryGetValue("x-github-event", out var value).ShouldBeTrue();
        value.ToString().ShouldBe("ping");
    }

    [Fact]
    public static void Read_Headers_With_Multiple_Values()
    {
        // Arrange
        string json = """{"Accept":["application/json","text/plain","*/*"]}""";

        // Act
        var actual = Read(json);

        // Assert
        actual.Count.ShouldBe(1);
        actual["Accept"].Count.ShouldBe(3);
        actual["Accept"].ToArray().ShouldBe(["application/json", "text/plain", "*/*"]);
    }

    [Theory]
    [InlineData("""{"Accept":[]}""")]
    [InlineData("""{"Accept":null}""")]
    public static void Read_Headers_With_No_Values(string json)
    {
        // Act
        var actual = Read(json);

        // Assert
        actual.Count.ShouldBe(1);
        actual.ShouldContainKey("Accept");
        actual["Accept"].Count.ShouldBe(0);
        actual["Accept"].ShouldBe(StringValues.Empty);
    }

    [Fact]
    public static void Read_Headers_With_Null_Values_In_Array()
    {
        // Arrange
        string json = """{"Accept":[null,"*/*"]}""";

        // Act
        var actual = Read(json);

        // Assert
        actual["Accept"].Count.ShouldBe(2);
        actual["Accept"].ToArray().ShouldBe([null, "*/*"]);
    }

    [Fact]
    public static void Read_Headers_With_String_Value()
    {
        // Arrange
        string json = """{"Accept":"*/*"}""";

        // Act
        var actual = Read(json);

        // Assert
        actual["Accept"].Count.ShouldBe(1);
        actual["Accept"].ToString().ShouldBe("*/*");
    }

    [Fact]
    public static void Read_Empty_Headers()
    {
        // Arrange
        string json = "{}";

        // Act
        var actual = Read(json);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""null""")]
    [InlineData(""""ping"""")]
    [InlineData("""{"Accept":42}""")]
    [InlineData("""{"Accept":{}}""")]
    [InlineData("""{"Accept":[42]}""")]
    [InlineData("""{"Accept":["*/*",true]}""")]
    [InlineData("""{"Accept":[["*/*"]]}""")]
    public static void Read_Throws_If_Json_Is_Invalid(string json)
    {
        // Act and Assert
        Should.Throw<JsonException>(() => Read(json));
    }

    [Fact]
    public static void Write_Headers()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues>()
        {
            ["Accept"] = new(["application/json", "*/*"]),
            ["Content-Type"] = new("application/json"),
            ["X-Empty"] = StringValues.Empty,
            ["X-Null"] = new((string?)null),
        };

        // Act
        string actual = Write(headers);

        // Assert
        actual.ShouldBe("""{"Accept":["application/json","*/*"],"Content-Type":["application/json"],"X-Empty":[],"X-Null":[]}""");
    }

    [Fact]
    public static void Write_Empty_Headers()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues>();

        // Act
        string actual = Write(headers);

        // Assert
        actual.ShouldBe("{}");
    }

    [Fact]
    public static void Headers_Can_Be_Roundtripped()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues>()
        {
            ["Accept"] = new(["application/json", "*/*"]),
            ["Content-Type"] = new("application/json"),
            ["X-GitHub-Delivery"] = new("9b5c943d-187a-4988-b3ca-9993d5268f85"),
        };

        // Act
        var actual = Read(Write(headers));

        // Assert
        actual.Count.ShouldBe(headers.Count);

        foreach ((var name, var values) in headers)
        {
            actual.ShouldContainKey(name);
            actual[name].ShouldBe(values);
        }
    }

    private static Dictionary<string, StringValues> Read(string json)
    {
        var target = new GitHubMessageHeadersJsonConverter();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        reader.Read();

        return target.Read(ref reader, typeof(Dictionary<string, StringValues>), JsonSerializerOptions.Default);
    }

    private static string Write(Dictionary<string, StringValues> headers)
    {
        var target = new GitHubMessageHeadersJsonConverter();
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            target.Write(writer, headers, JsonSerializerOptions.Default);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
