using System.Diagnostics;
using MongoDB.Bson.Serialization;
using OutboxWorker.WorkerService;
using OutboxWorker.WorkerService.Configurations;
using WorkerService1;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.AddMongoDBClient("mongodb");
builder.AddAzureServiceBusClient("messaging");

builder.Services.AddHostedService<MessageRelayWorker>();

BsonClassMap.RegisterClassMap<OutboxMessage>(classMap =>
{
    classMap.AutoMap();
    classMap.SetIgnoreExtraElements(true);
});

builder.Services.AddOptions<OutboxOptions>().BindConfiguration(nameof(OutboxOptions));
builder.Services.AddSingleton<ActivitySource>(x => new ActivitySource("OutboxWorker.DistributedTracing", "1.0.0"));

var host = builder.Build();
host.Run();