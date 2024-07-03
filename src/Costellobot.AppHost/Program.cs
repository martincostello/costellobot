// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureServiceBus("AzureServiceBus")
    : builder.AddConnectionString("AzureServiceBus");

builder.AddProject<Projects.Costellobot>("Costellobot")
       .WithReference(serviceBus);

var app = builder.Build();

app.Run();
