using System.Reflection;
using System.Runtime.CompilerServices;
using MongoDB.Bson.IO;

namespace OutboxWorker.WorkerService;

public static class CollectionsExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsMemory<T>(this List<T> list)
    {
        var items =
            (T[])list.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(list)!;

        return new ReadOnlyMemory<T>(items, 0, list.Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T>[] SliceInMemory<T>(this List<T> list, int sliceSize)
    {
        var messages = list.AsMemory();
        var sliceCount = (messages.Length + sliceSize - 1) / sliceSize;
        int start;
        var slices = new ReadOnlyMemory<T>[sliceCount];

        for (var i = 0; i < sliceCount; i++)
        {
            start = i * sliceSize;
            if (start + sliceSize > messages.Length)
            {
                slices[i] = messages.Slice(start);
                break;
            }

            slices[i] = messages.Slice(start, sliceSize);
        }

        return slices;
    }
}