using System.Runtime.Serialization;

namespace OutboxWorker.WorkerService;

public class NonExistentTransactionException : Exception
{
    public NonExistentTransactionException()
    {
    }

    public NonExistentTransactionException(string? message) : base(message)
    {
    }

    public NonExistentTransactionException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}