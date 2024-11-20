using System.Reflection;
using System.Runtime.CompilerServices;

namespace OutboxWorker.WorkerService;

public static class CollectionsExtensions
{
    public static Memory<T> AsMemory<T>(this List<T> list)
    {
        var items = (T[])list.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(list)!;
        
        return new Memory<T>(items, 0, list.Count);
    }
}