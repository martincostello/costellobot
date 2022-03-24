// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Mime;

namespace MartinCostello.Costellobot;

public class ResourceTests : EndToEndTest
{
    public ResourceTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [SkippableTheory(Skip = "Not implemented yet.")]
    [InlineData("/bad-request.html", MediaTypeNames.Text.Html)]
    [InlineData("/error.html", MediaTypeNames.Text.Html)]
    [InlineData("/favicon.png", "image/png")]
    [InlineData("/humans.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    [InlineData("/not-found.html", MediaTypeNames.Text.Html)]
    [InlineData("/robots.txt", MediaTypeNames.Text.Plain)]
    public async Task Can_Load_Resource_As_Get(string requestUri, string contentType)
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync(requestUri);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.ShouldNotBeNull();
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe(contentType);
    }

    [SkippableFact]
    public async Task Root_Redirects_To_Website()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.OriginalString.ShouldBe("https://martincostello.com/");
    }

    [SkippableFact(Skip = "Not implemented yet.")]
    public async Task Response_Headers_Contains_Expected_Headers()
    {
        // Arrange
        string[] expectedHeaders =
        {
            "Content-Security-Policy",
            "Expect-CT",
            "Feature-Policy",
            "Permissions-Policy",
            "Referrer-Policy",
            "X-Content-Type-Options",
            "X-Download-Options",
            "X-Frame-Options",
            "X-Request-Id",
            "X-XSS-Protection",
        };

        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync("/");

        // Assert
        foreach (string expected in expectedHeaders)
        {
            response.Headers.Contains(expected).ShouldBeTrue($"The '{expected}' response header was not found.");
        }
    }

    [SkippableFact(Skip = "Not implemented yet.")]
    public async Task Response_Headers_Does_Not_Contain_Unexpected_Headers()
    {
        // Arrange
        string[] expectedHeaders =
        {
            "Server",
            "X-Powered-By",
        };

        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync("/");

        // Assert
        foreach (string expected in expectedHeaders)
        {
            response.Headers.Contains(expected).ShouldBeFalse($"The '{expected}' response header was found.");
        }
    }
}
