using FluentAssertions;
using OutboxWorker.MessageRelay.Extensions;

namespace OutboxWorker.WokerService.Tests;

public class CollectionsExtensionsTest
{

    [Fact]
    public void AsMemory_WhenInvoked_ShouldWork()
    {
        var list = new List<int>() { 1, 2, 3 };

        var memory = list.AsMemory();

        memory.Length.Should().Be(3);
    }

    [Fact]
    public void ChunkInMemory_WhenInvoked_ShouldWork()
    {
        List<int> list = [..Enumerable.Range(0, 1_001)];

        var chunks = list.ChunkInMemory(100);

        chunks.Length.Should().Be(11);
        chunks[^1].Length.Should().Be(1);
    }
}