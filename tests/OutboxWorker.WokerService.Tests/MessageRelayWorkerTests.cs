using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using OutboxWorker.WorkerService;
using OutboxWorker.WorkerService.Configurations;

namespace OutboxWorker.WorkerService.Tests
{
    public class MessageRelayWorkerTests
    {
        private readonly Mock<IOptions<OutboxOptions>> _optionsMock;
        private readonly Mock<IMessageRepository> _messageRepositoryMock;
        private readonly Mock<ServiceBusClient> _serviceBusClientMock;
        private readonly Mock<ActivitySource> _activitySourceMock;
        private readonly Mock<ILogger<MessageRelayWorker>> _loggerMock;
        private readonly Mock<OutboxMetrics> _metricsMock;
        private readonly Mock<ServiceBusSender> _senderMock;
        private readonly Mock<IMongoCollection<RawBsonDocument>> _collectionMock;
        private readonly Mock<IClientSessionHandle> _sessionMock;

        public MessageRelayWorkerTests()
        {
            // Setup basic mocks
            _optionsMock = new Mock<IOptions<OutboxOptions>>();
            _messageRepositoryMock = new();
            _serviceBusClientMock = new Mock<ServiceBusClient>();
            _activitySourceMock = new Mock<ActivitySource>("test");
            _loggerMock = new Mock<ILogger<MessageRelayWorker>>();
            _metricsMock = new Mock<OutboxMetrics>();
            _senderMock = new Mock<ServiceBusSender>();
            _collectionMock = new Mock<IMongoCollection<RawBsonDocument>>();
            _sessionMock = new Mock<IClientSessionHandle>();

            // Setup default options
            _optionsMock.Setup(x => x.Value).Returns(new OutboxOptions
            {
                Delay = 1000,
                MaxDegreeOfParallelism = 4,
                BrokerOptions = new BrokerOptions
                {
                    EntityName = "test-queue",
                    BatchSize = 10
                },
                MongoOptions = new MongoOptions
                {
                    DatabaseName = "test-db",
                    BatchSize = 100,
                    Limit = 1000
                }
            });

            // Setup ServiceBusClient
            _serviceBusClientMock
                .Setup(x => x.CreateSender(It.IsAny<string>()))
                .Returns(_senderMock.Object);

            var databaseMock = new Mock<IMongoDatabase>();
            
            databaseMock.Setup(x => x.GetCollection<RawBsonDocument>(It.IsAny<string>(), null))
                .Returns(_collectionMock.Object);
        }

        [Fact]
        public async Task ProcessSliceAsync_WithValidMessages_SendsMessagesInBatches()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var messageBatch = new Mock<ServiceBusMessageBatch>();
            var messages = CreateTestMessages(15); // Create 15 test messages

            _senderMock
                .Setup(x => x.CreateMessageBatchAsync(cancellationToken))
                .ReturnsAsync(messageBatch.Object);

            messageBatch
                .Setup(x => x.TryAddMessage(It.IsAny<ServiceBusMessage>()))
                .Returns(true);

            var worker = CreateWorker();

            //new ReadOnlyMemory<RawBsonDocument>(messages)
            // Act
            await worker.StartAsync(cancellationToken);

            // Assert
            _senderMock.Verify(x => x.CreateMessageBatchAsync(cancellationToken), Times.AtLeast(2));
            _metricsMock.Verify(x => x.IncrementMessageCount(It.IsAny<int>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task ProcessSliceAsync_WhenBatchFull_CreatesNewBatch()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var messageBatch = new Mock<ServiceBusMessageBatch>();
            var messages = CreateTestMessages(5);

            _senderMock
                .Setup(x => x.CreateMessageBatchAsync(cancellationToken))
                .ReturnsAsync(messageBatch.Object);

            var tryAddMessageCalls = 0;
            messageBatch
                .Setup(x => x.TryAddMessage(It.IsAny<ServiceBusMessage>()))
                .Returns(() => {
                    tryAddMessageCalls++;
                    return tryAddMessageCalls <= 3; // First 3 messages succeed, then batch is "full"
                });

            var worker = CreateWorker();

            //new ReadOnlyMemory<RawBsonDocument>(messages)
            // Act
            await worker.StartAsync(cancellationToken);

            // Assert
            _senderMock.Verify(x => x.CreateMessageBatchAsync(cancellationToken), Times.AtLeast(2));
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelled_StopsProcessing()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var worker = CreateWorker();

            // Act
            cts.Cancel();
            await worker.StartAsync(cts.Token);

            // Assert
            _messageRepositoryMock.Verify(x => x.StartTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        
        [Fact]
        public async Task ProcessSliceAsync_WithEmptyMessages_HandlesGracefully()
        {
            // Implement test for empty message array
        }

        [Fact]
        public async Task ProcessSliceAsync_WithLargeMessage_HandlesCorrectly()
        {
            // Implement test for messages that exceed batch size
        }

        [Fact]
        public async Task ExecuteAsync_WhenMongoDbUnavailable_HandlesError()
        {
            // Implement test for MongoDB connection failure
        }

        [Fact]
        public async Task ExecuteAsync_WhenServiceBusUnavailable_HandlesError()
        {
            // Implement test for Service Bus connection failure
        }

        private MessageRelayWorker CreateWorker()
        {
            return new MessageRelayWorker(
                _optionsMock.Object,
                _messageRepositoryMock.Object,
                _serviceBusClientMock.Object,
                _activitySourceMock.Object,
                _loggerMock.Object,
                _metricsMock.Object
            );
        }

        private RawBsonDocument[] CreateTestMessages(int count)
        {
            var messages = new RawBsonDocument[count];
            for (var i = 0; i < count; i++)
            {
                var message = new
                {
                    _id = Guid.NewGuid(),
                    message = $"Test message {i}"
                };
                
                messages[i] = new RawBsonDocument(message.ToBson());
            }
            return messages;
        }
    }
}