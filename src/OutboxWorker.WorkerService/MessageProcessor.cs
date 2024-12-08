namespace OutboxWorker.WorkerService;

public class MessageProcessor : IMessageProcessor
{
    public async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        /*using var session = await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();

        try
        {
            var messages = await _repository.GetMessagesAsync(session, cancellationToken);
            var slices = messages.SliceInMemory(_options.SliceSize);

            await Parallel.ForEachAsync(
                slices,
                cancellationToken,
                async (memory, token) => await ProcessSliceAsync(memory, session, token));

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await session.AbortTransactionAsync(cancellationToken);
            throw;
        }*/
    }
}