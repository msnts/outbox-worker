using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Driver;

namespace OutboxWorker.WorkerService.Extensions;

public static class MongoClientExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IMongoCollection<RawBsonDocument> GetCollection(this IMongoClient client, string databaseName, string collectionName)
    {
        var database = client.GetDatabase(databaseName);

        return database.GetCollection<RawBsonDocument>(collectionName);
    }
}