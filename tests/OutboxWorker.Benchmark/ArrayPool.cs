using System.Buffers;
using BenchmarkDotNet.Attributes;

namespace OutboxWorker.Benchmark;

[MemoryDiagnoser]
public class ArrayPool
{
    private ArrayPool<Task> _pool = ArrayPool<Task>.Shared;
    
    [Benchmark(Baseline = true)]
    public void Base()
    {
        var tasks = new Task[25];
    }
    
    [Benchmark]
    public void Poll()
    {
        var memory = _pool.Rent(25);
        _pool.Return(memory);
    }
}