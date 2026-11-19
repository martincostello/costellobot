// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public static class DependencyHelpersTests
{
    [Theory]
    [InlineData(DependencyEcosystem.GitHubActions, "martincostello/github-automation/actions/get-github-token", "4.3.3", "GitHub Actions", "https://github.com/martincostello/github-automation/tree/HEAD/actions/get-github-token", "fa-brands fa-square-github text-dark")]
    [InlineData(DependencyEcosystem.Maven, "io.opentelemetry.javaagent:opentelemetry-javaagent", "2.34.0", "Maven", "https://central.sonatype.com/artifact/io.opentelemetry.javaagent/opentelemetry-javaagent/2.34.0", "fa-brands fa-java text-java")]
    [InlineData(DependencyEcosystem.Maven, "io.opentelemetry.javaagent:opentelemetry-javaagent", "", "Maven", "https://central.sonatype.com/artifact/io.opentelemetry.javaagent/opentelemetry-javaagent", "fa-brands fa-java text-java")]
    [InlineData(DependencyEcosystem.Maven, "^io\\.opentelemetry\\..*$", "", "Maven", "https://central.sonatype.com/search", "fa-brands fa-java text-java")]
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
