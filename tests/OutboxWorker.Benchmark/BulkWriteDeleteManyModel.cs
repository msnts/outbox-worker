using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Mathematics;
using BenchmarkDotNet.Order;
using MongoDB.Bson;
using MongoDB.Driver;

namespace OutboxWorker.Benchmark;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn(NumeralSystem.Arabic)]
public class BulkWriteDeleteManyModel
{
    private ReadOnlyMemory<RawBsonDocument> _messages;
    
    [Params(50, 100, 200)]
    public int ChunkSize { get; set; }

    public BulkWriteDeleteManyModel()
    {
        var messages = new RawBsonDocument[1_000];
        _messages = messages.AsMemory();
    }
    
    private static FilterDefinition<RawBsonDocument> CreateDeleteFilter(in RawBsonDocument first, in RawBsonDocument last)
    {
        return Builders<RawBsonDocument>.Filter.Empty;
    }

    [Benchmark(Baseline = true)]
    public void V1()
    {
        var chunkSize = ChunkSize;
        var chunkCount = (_messages.Length + chunkSize - 1) / chunkSize;
        var deleteManyModels = new DeleteManyModel<RawBsonDocument>[chunkCount];
        var start = 0;
        
        for (var i = 0; i < chunkCount; i++)
        {
            var chunk = _messages.Slice(start, Math.Min(chunkSize, _messages.Length - start));

            deleteManyModels[i] = new DeleteManyModel<RawBsonDocument>(CreateDeleteFilter(chunk.Span[0], chunk.Span[^1]));
            start += chunkSize;
        }
    }
    
    [Benchmark]
    public void V2()
    {
        var chunkSize = ChunkSize;
        var chunkCount = ((_messages.Length + chunkSize - 1) / chunkSize);
        var deleteManyModels = new DeleteManyModel<RawBsonDocument>[chunkCount];
        ReadOnlyMemory<RawBsonDocument> chunk;
        var i = 0;
        chunkCount--;
        
        while (i < chunkCount)
        {
            chunk = _messages.Slice(i * chunkSize, chunkSize);
            deleteManyModels[i] = new DeleteManyModel<RawBsonDocument>(CreateDeleteFilter(chunk.Span[0], chunk.Span[^1]));
            i++;
        }
    
        chunk = _messages.Slice(i * chunkSize);
        deleteManyModels[i] = new DeleteManyModel<RawBsonDocument>(CreateDeleteFilter(chunk.Span[0], chunk.Span[^1]));
    }
    
    [Benchmark]
    public void V3()
    {
        var chunkSize = ChunkSize;
        var length = _messages.Length;
        var chunkCount = (length + chunkSize - 1) / chunkSize;
        var deleteManyModels = new DeleteManyModel<RawBsonDocument>[chunkCount];
        var start = 0;
        
        for (var i = 0; i < chunkCount; i++)
        {
            var remaining = length - start;
            var currentChunkSize = remaining < chunkSize ? remaining : chunkSize;
            var chunk = _messages.Slice(start, currentChunkSize).Span;

            deleteManyModels[i] = new DeleteManyModel<RawBsonDocument>(CreateDeleteFilter(chunk[0], chunk[^1]));
            start += chunkSize;
        }
    }
}