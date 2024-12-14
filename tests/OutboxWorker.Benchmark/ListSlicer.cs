using BenchmarkDotNet.Attributes;
using OutboxWorker.WorkerService;

namespace OutboxWorker.Benchmark;

[MemoryDiagnoser]
public class ListSlicer
{
    private readonly List<int> _data = [..Enumerable.Range(0, 1_000)];

    [Benchmark]
    public void AsMemory()
    {
        _data.AsMemory();
    }
    
    [Benchmark]
    public void Slice()
    {
        _data.SliceInMemory(10);
    }
}