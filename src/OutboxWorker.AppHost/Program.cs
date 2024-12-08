var builder = DistributedApplication.CreateBuilder(args);

var mongodb = builder.AddConnectionString("mongodb");

var serviceBus = builder.AddConnectionString("messaging");

var apiService = builder.AddProject<Projects.OutboxWorker_WorkerService>("worker-service")
    .WithReference(mongodb)
    .WithReference(serviceBus);

builder.Build().Run();
