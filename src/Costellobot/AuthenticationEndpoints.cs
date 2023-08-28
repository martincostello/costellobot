// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

public static class AuthenticationEndpoints
{
    internal const string AdminPolicyName = "costellobot-admin";

    private const string ApplicationName = "costellobot";
    private const string CookiePrefix = ".costellobot.";
    private const string DeniedPath = "/denied";
    private const string ErrorPath = "/error";
    private const string ForbiddenPath = "/forbidden";
    private const string RootPath = "/";
    private const string SignInPath = "/sign-in";
    private const string SignOutPath = "/sign-out";

    private const string GitHubAvatarClaim = "urn:github:avatar";
    private const string GitHubProfileClaim = "urn:github:profile";

    public static IServiceCollection AddGitHubAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.AccessDeniedPath = DeniedPath;
                options.Cookie.Name = CookiePrefix + "authentication";
                options.ExpireTimeSpan = TimeSpan.FromDays(60);
                options.LoginPath = SignInPath;
                options.LogoutPath = SignOutPath;
                options.SlidingExpiration = true;
            })
            .AddGitHub()
            .Services
            .AddOptions<GitHubAuthenticationOptions>(GitHubAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<GitHubOptions>>((options, configuration) =>
            {
                options.AccessDeniedPath = DeniedPath;
                options.CallbackPath = SignInPath + "-github";
                options.ClientId = configuration.Value.ClientId;
                options.ClientSecret = configuration.Value.ClientSecret;
                options.CorrelationCookie.Name = CookiePrefix + "correlation";
                options.EnterpriseDomain = configuration.Value.EnterpriseDomain;

                foreach (string scope in configuration.Value.Scopes)
                {
                    options.Scope.Add(scope);
                }

                options.ClaimActions.MapJsonKey(GitHubProfileClaim, "html_url");

                if (string.IsNullOrEmpty(options.EnterpriseDomain))
                {
                    options.ClaimActions.MapJsonKey(GitHubAvatarClaim, "avatar_url");
                }

                options.Events.OnTicketReceived = (context) =>
                {
                    var timeProvider = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();

                    context.Properties!.ExpiresUtc = timeProvider.GetUtcNow().AddDays(60);
                    context.Properties.IsPersistent = true;

                    return Task.CompletedTask;
                };
            })
            .ValidateOnStart();

        services
            .AddAuthorization()
            .AddOptions<AuthorizationOptions>()
            .Configure<IOptions<SiteOptions>>((options, config) =>
            {
                options.AddPolicy(AdminPolicyName, (policy) =>
                {
                    policy.RequireAuthenticatedUser();

                    if (config.Value.AdminRoles.Count > 0)
                    {
                        policy.RequireRole(config.Value.AdminRoles);
                    }

                    if (config.Value.AdminUsers.Count > 0)
                    {
                        policy.RequireClaim(ClaimTypes.Name, config.Value.AdminUsers);
                    }
                });
            });

        services.AddAntiforgery((options) =>
        {
            options.Cookie.Name = CookiePrefix + "antiforgery";
            options.FormFieldName = "__antiforgery";
            options.HeaderName = "x-antiforgery";
        });

        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName(ApplicationName);

        string? connectionString = configuration["ConnectionStrings:AzureStorage"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
#pragma warning disable CA1308
            string relativePath = $"costellobot/{environment.EnvironmentName.ToLowerInvariant()}/keys.xml";
#pragma warning restore CA1308

            dataProtection.PersistKeysToAzureBlobStorage(
                connectionString,
                "data-protection",
                relativePath);
        }

        return services;
    }

    public static string GetAvatarUrl(this ClaimsPrincipal user)
        => user.FindFirst(GitHubAvatarClaim)?.Value ?? string.Empty;

    public static string GetProfileUrl(this ClaimsPrincipal user)
        => user.FindFirst(GitHubProfileClaim)!.Value;

    public static string GetUserName(this ClaimsPrincipal user)
        => user.FindFirst(GitHubAuthenticationConstants.Claims.Name)!.Value;

    public static IEndpointRouteBuilder MapAuthenticationRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(DeniedPath, () => Results.Redirect(SignInPath + "?denied=true"));
        builder.MapGet(ForbiddenPath, () => Results.Redirect(ErrorPath + "?id=403"));

        builder.MapGet(SignOutPath, () => Results.Redirect(RootPath));

        builder.MapPost(SignInPath, async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.Redirect(RootPath);
            }

            return Results.Challenge(
                new() { RedirectUri = RootPath },
                new[] { GitHubAuthenticationDefaults.AuthenticationScheme });
        });

        builder.MapPost(SignOutPath, async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.Redirect(RootPath);
            }

            return Results.SignOut(
                new() { RedirectUri = RootPath },
                new[] { CookieAuthenticationDefaults.AuthenticationScheme });
        });

        return builder;
    }
}
