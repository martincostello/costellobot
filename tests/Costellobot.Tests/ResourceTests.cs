// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Mime;
using System.Text.Json;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot;

[Collection<AppCollection>]
public sealed class ResourceTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Theory]
    [InlineData("/bad-request.html", MediaTypeNames.Text.Html)]
    [InlineData("/error.html", MediaTypeNames.Text.Html)]
    [InlineData("/favicon.png", "image/png")]
    [InlineData("/forbidden.html", MediaTypeNames.Text.Html)]
    [InlineData("/health/liveness", MediaTypeNames.Application.Json)]
    [InlineData("/health/readiness", MediaTypeNames.Application.Json)]
    [InlineData("/health/startup", MediaTypeNames.Application.Json)]
    [InlineData("/humans.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    [InlineData("/not-found.html", MediaTypeNames.Text.Html)]
    [InlineData("/robots.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/robots933456.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/sign-in", MediaTypeNames.Text.Html)]
    [InlineData("/static/css/main.css", "text/css")]
    [InlineData("/static/css/main.css.map", MediaTypeNames.Text.Plain)]
    [InlineData("/static/js/main.js", "text/javascript")]
    [InlineData("/static/js/main.js.map", MediaTypeNames.Text.Plain)]
    [InlineData("/unauthorized.html", MediaTypeNames.Text.Html)]
    [InlineData("/version", MediaTypeNames.Application.Json)]
    public async Task Can_Get_Resource_Unauthenticated(string requestUri, string contentType)
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Failed to get {requestUri}. {await response.Content!.ReadAsStringAsync(CancellationToken)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe(contentType);
        response.Content.Headers.ContentLength.ShouldNotBeNull();
        response.Content.Headers.ContentLength.ShouldNotBe(0);
    }

    [Theory]
    [InlineData("/", MediaTypeNames.Text.Html)]
    [InlineData("/bad-request.html", MediaTypeNames.Text.Html)]
    [InlineData("/error.html", MediaTypeNames.Text.Html)]
    [InlineData("/favicon.png", "image/png")]
    [InlineData("/forbidden.html", MediaTypeNames.Text.Html)]
    [InlineData("/github-webhook", MediaTypeNames.Text.Html)]
    [InlineData("/humans.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    [InlineData("/not-found.html", MediaTypeNames.Text.Html)]
    [InlineData("/robots.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/robots933456.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/static/css/main.css", "text/css")]
    [InlineData("/static/css/main.css.map", MediaTypeNames.Text.Plain)]
    [InlineData("/static/js/main.js", "text/javascript")]
    [InlineData("/static/js/main.js.map", MediaTypeNames.Text.Plain)]
    [InlineData("/unauthorized.html", MediaTypeNames.Text.Html)]
    public async Task Can_Get_Resource_Authenticated(string requestUri, string contentType)
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Failed to get {requestUri}. {await response.Content!.ReadAsStringAsync(CancellationToken)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe(contentType);
        response.Content.Headers.ContentLength.ShouldNotBeNull();
        response.Content.Headers.ContentLength.ShouldNotBe(0);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/deliveries")]
    [InlineData("/delivery/1")]
    [InlineData("/github-webhook")]
    public async Task Cannot_Get_Resource_Unauthenticated(string requestUri)
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.PathAndQuery.ShouldBe($"/sign-in?ReturnUrl={Uri.EscapeDataString(requestUri)}");
    }

    [Fact]
    public async Task Sign_In_Redirects_If_Authenticated()
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync("/sign-in", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.OriginalString.ShouldBe("/");
    }

    [Fact]
    public async Task Sign_In_Redirects_To_Return_Url_If_Authenticated()
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync("/sign-in?ReturnUrl=%2Fdebug", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.OriginalString.ShouldBe("/debug");
    }

    [Fact]
    public async Task Sign_In_Does_Not_Open_Redirect()
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync("/sign-in?ReturnUrl=http%3A%2F%2Fcostellobot.local%2Fdebug", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.OriginalString.ShouldBe("/");
    }

    [Theory]
    [InlineData("/sign-in")]
    [InlineData("/sign-out")]
    public async Task Cannot_Post_Resource_Without_Antiforgery_Tokens(string requestUri)
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync(setAntiforgeryTokenHeader: false);
        using var content = new FormUrlEncodedContent([]);

        // Act
        using var response = await client.PostAsync(requestUri, content, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Manifest_Is_Valid_Json()
    {
        // Arrange
        using var client = Fixture.CreateClient();
        using var response = await client.GetAsync("/manifest.webmanifest", CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();

        string json = await response.Content!.ReadAsStringAsync(CancellationToken);
        var manifest = JsonDocument.Parse(json);

        manifest.RootElement.GetProperty("name").GetString().ShouldBe("costellobot");
    }

    [Theory]
    [InlineData("/error", HttpStatusCode.InternalServerError)]
    [InlineData("/error?id=200", HttpStatusCode.InternalServerError)]
    [InlineData("/error?id=400", HttpStatusCode.BadRequest)]
    [InlineData("/error?id=401", HttpStatusCode.Unauthorized)]
    [InlineData("/error?id=403", HttpStatusCode.Forbidden)]
    [InlineData("/error?id=404", HttpStatusCode.NotFound)]
    [InlineData("/error?id=405", HttpStatusCode.MethodNotAllowed)]
    [InlineData("/error?id=408", HttpStatusCode.RequestTimeout)]
    [InlineData("/error?id=500", HttpStatusCode.InternalServerError)]
    [InlineData("/error?id=599", HttpStatusCode.InternalServerError)]
    [InlineData("/error?id=600", HttpStatusCode.InternalServerError)]
    [InlineData("/foo", HttpStatusCode.NotFound)]
    public async Task Error_Page_Returns_Correct_Status_Code(string requestUri, HttpStatusCode expected)
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(expected);
        response.Content.ShouldNotBeNull();
        response.Content.Headers.ShouldNotBeNull();
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task Response_Headers_Contains_Expected_Headers()
    {
        // Arrange
        string[] expectedHeaders =
        [
            "Content-Security-Policy",
            "Cross-Origin-Embedder-Policy",
            "Cross-Origin-Opener-Policy",
            "Cross-Origin-Resource-Policy",
            "Expect-CT",
            "Permissions-Policy",
            "Referrer-Policy",
            "X-Content-Type-Options",
            "X-Download-Options",
            "X-Frame-Options",
            "X-Request-Id",
            "X-XSS-Protection",
        ];

        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync("/", CancellationToken);

        // Assert
        foreach (string expected in expectedHeaders)
        {
            response.Headers.Contains(expected).ShouldBeTrue($"The '{expected}' response header was not found.");
        }
    }

    [Fact]
    public async Task Response_Headers_Does_Not_Contain_Unexpected_Headers()
    {
        // Arrange
        string[] expectedHeaders =
        [
            "Server",
            "X-Powered-By",
        ];

        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync("/", CancellationToken);

        // Assert
        foreach (string expected in expectedHeaders)
        {
            response.Headers.Contains(expected).ShouldBeFalse($"The '{expected}' response header was found.");
        }
    }

    [Theory]
    [InlineData("/.env")]
    [InlineData("/.git")]
    [InlineData("/.git/head")]
    [InlineData("/admin.php")]
    [InlineData("/admin")]
    [InlineData("/admin/")]
    [InlineData("/admin/index.php")]
    [InlineData("/administration")]
    [InlineData("/administration/")]
    [InlineData("/administration/index.php")]
    [InlineData("/administrator")]
    [InlineData("/administrator/")]
    [InlineData("/administrator/index.php")]
    [InlineData("/appsettings.json")]
    [InlineData("/bin/site.dll")]
    [InlineData("/foo.asp")]
    [InlineData("/foo.ASP")]
    [InlineData("/foo.aspx")]
    [InlineData("/foo.ASPX")]
    [InlineData("/foo.jsp")]
    [InlineData("/foo.JSP")]
    [InlineData("/foo.php")]
    [InlineData("/foo.PHP")]
    [InlineData("/obj/site.dll")]
    [InlineData("/package.json")]
    [InlineData("/package-lock.json")]
    [InlineData("/parameters.xml")]
    [InlineData("/perl.alfa")]
    [InlineData("/web.config")]
    [InlineData("/wp-admin/blah")]
    [InlineData("/wp-json")]
    [InlineData("/xmlrpc.php")]
    public async Task Crawler_Spam_Is_Redirected_To_YouTube(string requestUri)
    {
        // Arrange
        HttpMethod[] methods = [HttpMethod.Get, HttpMethod.Head, HttpMethod.Post];

        using var client = Fixture.CreateClient();

        foreach (var method in methods)
        {
            // Arrange
            using var message = new HttpRequestMessage(method, requestUri);

            if (method.Equals(HttpMethod.Post))
            {
                message.Content = new StringContent(string.Empty);
            }

            // Act
            using var response = await client.SendAsync(message, CancellationToken);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location.ShouldNotBeNull();
            response.Headers.Location.Host.ShouldBe("www.youtube.com");
        }
    }
}
