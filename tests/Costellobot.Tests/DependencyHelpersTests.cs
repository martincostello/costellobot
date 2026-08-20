// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public static class DependencyHelpersTests
{
    [Theory]
    [InlineData(DependencyEcosystem.GitHubActions, "martincostello/github-automation/actions/get-github-token", "4.3.3", "GitHub Actions", "https://github.com/martincostello/github-automation/tree/HEAD/actions/get-github-token", "fa-brands fa-square-github text-dark")]
    public static void GetPackageMetadata_Returns_Expected_Values(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        string expectedName,
        string expectedUrl,
        string expectedCssClasses)
    {
        // Act
        var (actualName, actualUrl, actualCssClasses) = DependencyHelpers.GetPackageMetadata(ecosystem, id, version);

        // Assert
        Assert.Equal(expectedName, actualName);
        Assert.Equal(expectedUrl, actualUrl);
        Assert.Equal(expectedCssClasses, actualCssClasses);
    }
}
