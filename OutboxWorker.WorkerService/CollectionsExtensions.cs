using System.Runtime.CompilerServices;

namespace OutboxWorker.WorkerService;

public static class CollectionsExtensions
{
    public static Memory<T> AsMemory<T>(this List<T> _list)
    {
#if NET9_0_OR_GREATER
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_items")]
        extern static ref T[] GetListItems(List<T> list);

        var items = GetListItems(_list);
#else
    var items = (T[])_list.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_list)!;
#endif

        return new Memory<T>(items, 0, _list.Count);
    }
}