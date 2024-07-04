// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;

namespace MartinCostello.Costellobot;

public static class GitHubMessageSerializerTests
{
    [Fact]
    public static void GitHub_Payload_Can_Be_Roundtripped()
    {
        // Arrange
        var deliveryId = "9b5c943d-187a-4988-b3ca-9993d5268f85";
        var headers = new Dictionary<string, string>()
        {
            ["Accept"] = "*/*",
            ["Content-Type"] = "application/json",
            ["User-Agent"] = "GitHub-Hookshot/f22e2d9",
            ["X-GitHub-Delivery"] = deliveryId,
            ["X-GitHub-Event"] = "ping",
            ["X-GitHub-Hook-ID"] = "456789",
            ["X-GitHub-Hook-Installation-Target-ID"] = "123456",
            ["X-GitHub-Hook-Installation-Target-Type"] = "integration",
            ["X-Hub-Signature"] = "sha1=db2e04e6528d52be37e76cc047b50213d5d99de5",
            ["X-Hub-Signature-256"] = "sha256=687f2fd74fc04b0ed1d34477cd4915ced4803b91a22422445c36853f11c5d99b",
        };

        string body =
            /*lang=json,strict*/
            """
            {
              "zen": "Responsive is better than fast.",
              "hook_id": 109948940,
              "hook": {
                "type": "App",
                "id": 109948940,
                "name": "web",
                "active": true,
                "events": ["*"]
              },
              "config": {
                "content_type": "json",
                "insecure_ssl": "0",
                "secret": "my-secret",
                "url": "https://costellobot.martincostello.local/github-webhook"
              },
              "updated_at": "2022-03-23T23:13:43Z",
              "created_at": "2022-03-23T23:13:43Z",
              "app_id": 349596565,
              "deliveries_url": "https://api.github.com/app/hook/deliveries"
            }
            """;

        // Act
        var serialized = GitHubMessageSerializer.Serialize(deliveryId, headers, body);

        // Assert
        serialized.ShouldNotBeNull();
        serialized.Body.ShouldNotBeNull();
        serialized.ContentType.ShouldBe("application/json");
        serialized.MessageId.ShouldBe(deliveryId);
        serialized.Subject.ShouldBe("github-webhook");
        serialized.ApplicationProperties.ShouldContainKey("publisher");

        var amqp = new AmqpAnnotatedMessage(AmqpMessageBody.FromData([serialized.Body]));
        var message = ServiceBusReceivedMessage.FromAmqpMessage(amqp, BinaryData.FromBytes([]));

        (var actualHeaders, var actualBody) = GitHubMessageSerializer.Deserialize(message);

        // Assert
        actualHeaders.ShouldNotBeNull();
        actualBody.ShouldNotBeNull();
        actualBody.ShouldBe(body);
        actualHeaders.Keys.ToArray().ShouldBeEquivalentTo(headers.Keys.ToArray());
        actualHeaders.Keys.ShouldAllBe((key) => actualHeaders[key].ToArray().SequenceEqual(new[] { headers[key] }));
    }
}
