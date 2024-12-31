using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OutboxWorker.MessageRelay.Metrics;
using OutboxWorker.MessageRelay.Options;
using OutboxWorker.MessageRelay.Processor;
using OutboxWorker.MessageRelay.Repository;

namespace OutboxWorker.MessageRelay.Configurations;

public static class ServiceCollectionExtensions
{
    public static void AddOutbox(this IServiceCollection services)
    {
        services
            .AddOptions<OutboxOptions>()
            .BindConfiguration(nameof(OutboxOptions))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddSingleton<ActivitySource>(x => new ActivitySource("OutboxWorker.DistributedTracing", "1.0.0"));
        services.AddSingleton<OutboxMetrics>();
        services.AddSingleton<IMessageRepository, MessageRepository>();
        services.AddSingleton<IMessageProcessor, MessageProcessor>();
        
        services.AddHostedService<MessageRelayWorker>();
    }
}