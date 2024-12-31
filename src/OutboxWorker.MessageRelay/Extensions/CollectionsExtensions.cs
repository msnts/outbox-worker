using System.Runtime.CompilerServices;

namespace OutboxWorker.MessageRelay.Extensions;

public static class ArrayAccessor<T>
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_items")]
    public static extern ref T[] GetItems(List<T> list);
}

public static class CollectionsExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsMemory<T>(this List<T> list) => new(ArrayAccessor<T>.GetItems(list), 0, list.Count);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T>[] ChunkInMemory<T>(this List<T> list, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        if (list.Count == 0) return [];
        
        var messages = list.AsMemory();
        var length = messages.Length;
        var chunkCount = (length + chunkSize - 1) / chunkSize;
        var start = 0;
        var chunks = new ReadOnlyMemory<T>[chunkCount];

        for (var i = 0; i < chunkCount; i++)
        {
            var remaining = length - start;
            var currentChunkSize = remaining < chunkSize ? remaining : chunkSize;

            chunks[i] = messages.Slice(start, currentChunkSize);
            start += chunkSize;
        }

        return chunks;
    }
}