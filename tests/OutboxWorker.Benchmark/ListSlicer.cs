using BenchmarkDotNet.Attributes;
using OutboxWorker.WorkerService;

namespace OutboxWorker.Benchmark;

[MemoryDiagnoser]
public class ListSlicer
{
    private readonly List<int> data;

    public ListSlicer()
    {
        data = new(Enumerable.Range(0, 1_000));
    }
    
    [Benchmark]
    public void AsMemory()
    {
        data.AsMemory();
    }
    
    [Benchmark]
    public void Slice()
    {
        data.SliceInMemory(10);
    }
}