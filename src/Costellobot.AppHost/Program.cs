// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

var builder = DistributedApplication.CreateBuilder(args);

const string BlobStorage = "AzureBlobStorage";
const string KeyVault = "AzureKeyVault";
const string ServiceBus = "AzureServiceBus";
const string Storage = "AzureStorage";
const string TableStorage = "AzureTableStorage";

var blobStorage = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureStorage(Storage).AddBlobs(BlobStorage)
    : builder.AddConnectionString(BlobStorage);

var tableStorage = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureStorage(Storage).AddTables(TableStorage)
    : builder.AddConnectionString(TableStorage);

var secrets = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault(KeyVault)
    : builder.AddConnectionString(KeyVault);

var serviceBus = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureServiceBus(ServiceBus)
    : builder.AddConnectionString(ServiceBus);

builder.AddProject<Projects.Costellobot>("Costellobot")
       .WithReference(secrets)
       .WithReference(blobStorage)
       .WithReference(tableStorage)
       .WithReference(serviceBus);

var app = builder.Build();

app.Run();
