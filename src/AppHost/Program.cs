var builder = DistributedApplication.CreateBuilder(args);

// Configure Agent project
var agent = builder
    .AddProject<Projects.Agent>("agent")
    .WithExternalHttpEndpoints()
    .WithEnvironment("EnableSwagger", "true");

// Configure WebChat project
var webChat = builder
    .AddProject<Projects.WebChat>("webchat")
    .WithExternalHttpEndpoints()
    .WithEnvironment("EnableSwagger", "true")
    .WithReference(agent)
    .WaitFor(agent);

builder.Build().Run();
