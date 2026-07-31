var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var featureFlagsDb = postgres.AddDatabase("featureflagsdb");

var server = builder.AddProject<Projects.FeatureFlags_Server>("server")
    .WithReference(cache)
    .WithReference(featureFlagsDb)
    .WaitFor(cache)
    .WaitFor(featureFlagsDb)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithPnpm()
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
