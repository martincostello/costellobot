// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Xunit.Sdk;

[assembly: TestFramework("MartinCostello.Costellobot.DotNetTraceFixture", "Costellobot.Tests")]

namespace MartinCostello.Costellobot;

public sealed class DotNetTraceFixture : XunitTestFramework
{
    private static Process? _process;

    public DotNetTraceFixture(IMessageSink messageSink)
      : base(messageSink)
    {
        if (OperatingSystem.IsMacOS())
        {
            using var self = Process.GetCurrentProcess();
            _process ??= Process.Start(
                "dotnet-trace",
                [
                    "--providers",
                    "System.Net.Http,Private.InternalDiagnostics.System.Net.Http,System.Net.Security,System.Net.Sockets,System.Net.NameResolution,System.Threading.Tasks.TplEventSource:0x80:4",
                    "--process-id",
                    self.Id.ToString(CultureInfo.InvariantCulture),
                ]);
        }
    }

    ~DotNetTraceFixture()
    {
        try
        {
            _process?.Kill();
            _process = null;
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}
