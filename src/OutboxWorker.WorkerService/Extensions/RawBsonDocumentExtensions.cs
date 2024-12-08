using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using MongoDB.Bson;

namespace OutboxWorker.WorkerService.Extensions;

public static class RawBsonDocumentExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ServiceBusMessage ToServiceBusMessage(this RawBsonDocument message)
    {
        return new ServiceBusMessage(message["Body"].AsBsonDocument.ToJson())
        {
            MessageId = message["_id"].AsGuid.ToString(),
            CorrelationId = message["CorrelationId"].AsGuid.ToString(),
            Subject = message["Subject"].AsString,
            ContentType = "application/json"
        };
    }
}