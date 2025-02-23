// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

/// <summary>
/// A class containing endpoints for redirects. This class cannot be inherited.
/// </summary>
public static class RedirectEndpoints
{
    private static readonly string[] HttpMethods = ["GET", "HEAD", "POST"];

    /// <summary>
    /// Gets a random set of annoying YouTube videos. This field is read-only.
    /// </summary>
    /// <remarks>
    /// Inspired by <c>https://gist.github.com/NickCraver/c9458f2e007e9df2bdf03f8a02af1d13</c>.
    /// </remarks>
    private static ReadOnlySpan<string> Videos => new[]
    {
        "https://www.youtube.com/watch?v=wbby9coDRCk",
        "https://www.youtube.com/watch?v=nb2evY0kmpQ",
        "https://www.youtube.com/watch?v=z9Uz1icjwrM",
        "https://www.youtube.com/watch?v=Sagg08DrO5U",
        "https://www.youtube.com/watch?v=jScuYd3_xdQ",
        "https://www.youtube.com/watch?v=S5PvBzDlZGs",
        "https://www.youtube.com/watch?v=9UZbGgXvCCA",
        "https://www.youtube.com/watch?v=O-dNDXUt1fg",
        "https://www.youtube.com/watch?v=MJ5JEhDy8nE",
        "https://www.youtube.com/watch?v=VnnWp_akOrE",
        "https://www.youtube.com/watch?v=sCNrK-n68CM",
        "https://www.youtube.com/watch?v=hgwpZvTWLmE",
        "https://www.youtube.com/watch?v=jAckVuEY_Rc",
    };

    /// <summary>
    /// Maps the redirection routes.
    /// </summary>
    /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to use.</param>
    /// <returns>
    /// The value of <paramref name="app"/>.
    /// </returns>
    public static IEndpointRouteBuilder MapRedirects(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<SiteOptions>>();

        foreach (string path in options.Value.CrawlerPaths)
        {
            app.MapMethods(path, HttpMethods, RandomYouTubeVideo);
        }

        return app;

        static IResult RandomYouTubeVideo()
            => Results.Redirect(Videos[RandomNumberGenerator.GetInt32(0, Videos.Length)]);
    }
}
