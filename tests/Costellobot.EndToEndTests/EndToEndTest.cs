// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

[Collection(AppCollection.Name)]
[Trait("Category", "EndToEnd")]
public abstract class EndToEndTest
{
    protected EndToEndTest(AppFixture fixture, ITestOutputHelper outputHelper)
    {
        Fixture = fixture;
        OutputHelper = outputHelper;
    }

    protected AppFixture Fixture { get; }

    protected ITestOutputHelper OutputHelper { get; }

    protected Uri ServerAddress => Fixture.ServerAddress!;
}
