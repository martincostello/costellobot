// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

var builder = DistributedApplication.CreateBuilder(args);

const string KeyVault = "AzureKeyVault";
const string ServiceBus = "AzureServiceBus";
const string Storage = "AzureStorage";

var secrets = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault(KeyVault)
    : builder.AddConnectionString(KeyVault);

var serviceBus = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureServiceBus(ServiceBus)
    : builder.AddConnectionString(ServiceBus);

var storage = builder.AddConnectionString(Storage);

builder.AddProject<Projects.Costellobot>("Costellobot")
       .WithReference(secrets)
       .WithReference(serviceBus)
       .WithReference(storage);

var app = builder.Build();

app.Run();
