// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Costellobot>("Costellobot");

var app = builder.Build();

app.Run();
