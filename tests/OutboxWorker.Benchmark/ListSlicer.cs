using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using OutboxWorker.WorkerService;

namespace OutboxWorker.Benchmark;

[MemoryDiagnoser]
public class ListSlicer
{
    private readonly ReadOnlyMemory<int> _data;
    
    [Params(50, 100, 200)]
    public int ChunkSize { get; set; }

    public ListSlicer()
    {
        int[] items = [..Enumerable.Range(0, 10_000)];
        _data = items.AsMemory();
    }
    
    [Benchmark(Baseline = true)]
    public void V1()
    {
        var chunkSize = ChunkSize;
        var sliceCount = (_data.Length + chunkSize - 1) / chunkSize;
        var start = 0;
        var chunks = new ReadOnlyMemory<int>[sliceCount];

        for (var i = 0; i < sliceCount; i++)
        {
            if (start + chunkSize > _data.Length)
            {
                chunks[i] = _data.Slice(start);
                break;
            }

            chunks[i] = _data.Slice(start, chunkSize);
            start += chunkSize;
        }
    }

    [Benchmark]
    public void V2()
    {
        var chunkSize = ChunkSize;
        var length = _data.Length;
        var sliceCount = (length + chunkSize - 1) / chunkSize;
        var start = 0;
        var chunks = new ReadOnlyMemory<int>[sliceCount];

        for (var i = 0; i < sliceCount; i++)
        {
            var remaining = length - start;
            var currentChunkSize = remaining < chunkSize ? remaining : chunkSize;

            chunks[i] = _data.Slice(start, currentChunkSize);
            start += chunkSize;
        }
    }
}