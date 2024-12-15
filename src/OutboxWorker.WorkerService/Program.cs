using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Core.Configuration;
using OutboxWorker.ServiceDefaults;
using OutboxWorker.WorkerService;
using OutboxWorker.WorkerService.Configurations;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Logging.ClearProviders();
builder.Logging. AddConsole();

using var loggerFactory = LoggerFactory.Create(b =>
{
    b.AddSimpleConsole();
    b.SetMinimumLevel(LogLevel.Debug);
});

builder.AddMongoDBClient("mongodb", configureClientSettings: settings =>
{
    //settings.LoggingSettings = new LoggingSettings(loggerFactory);
});
builder.AddAzureServiceBusClient("messaging");

//builder.Services.AddHostedService<MessageGenerateWorker>();
builder.Services.AddHostedService<MessageRelayWorker>();

#pragma warning disable 618
BsonDefaults.GuidRepresentation = GuidRepresentation.Standard;
BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

BsonClassMap.RegisterClassMap<OutboxMessage>(classMap =>
{
    classMap.AutoMap();
    classMap.SetIgnoreExtraElements(true);
});

BsonClassMap.RegisterClassMap<User>(classMap =>
{
    classMap.AutoMap();
    classMap.SetIgnoreExtraElements(true);
});

builder.Services.AddOptions<OutboxOptions>()
    .BindConfiguration(nameof(OutboxOptions))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ActivitySource>(x => new ActivitySource("OutboxWorker.DistributedTracing", "1.0.0"));
builder.Services.AddSingleton<OutboxMetrics>();
builder.Services.AddSingleton<IMessageRepository, MessageRepository>();
builder.Services.AddSingleton<IMessageProcessor, MessageProcessor>();

var host = builder.Build();
host.Run();