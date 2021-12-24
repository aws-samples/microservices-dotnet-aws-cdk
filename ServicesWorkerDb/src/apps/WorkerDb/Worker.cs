using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2;
using System.Text.Json;

namespace WorkerDb
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private string _region;
        private IAmazonSQS _client;

        public Worker(ILogger<Worker> logger)
        {
            _region = Environment.GetEnvironmentVariable("AWS_REGION") ?? RegionEndpoint.USWest2.SystemName;
            _logger = logger;
            _client = new AmazonSQSClient(RegionEndpoint.GetBySystemName(_region));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queueUrl = Environment.GetEnvironmentVariable("WORKER_QUEUE_URL"); 

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                _logger.LogInformation($"The SQS queue's URL is {queueUrl}");
                try
                {
                    var response = await ReceiveAndDeleteMessage(_client, queueUrl);
                    _logger.LogInformation($"Message: ", response);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "error consuming msg");
                }

                await Task.Delay(1000 * 5, stoppingToken);
            }
        }

        /// <summary>
        /// Retrieves the message from the quque at the URL passed in the
        /// queueURL parameters using the client.
        /// </summary>
        /// <param name="client">The SQS client used to retrieve a message.</param>
        /// <param name="queueUrl">The URL of the queue from which to retrieve
        /// a message.</param>
        /// <returns></returns>
        public async Task<ReceiveMessageResponse> ReceiveAndDeleteMessage(IAmazonSQS client, string queueUrl)
        {
            // Receive a single message from the queue. 
            var receiveMessageRequest = new ReceiveMessageRequest
            {
                AttributeNames = { "SentTimestamp" },
                MaxNumberOfMessages = 1,
                MessageAttributeNames = { "All" },
                QueueUrl = queueUrl,
                VisibilityTimeout = 120,
                WaitTimeSeconds = 0
            };

            var receiveMessageResponse = await client.ReceiveMessageAsync(receiveMessageRequest);

            foreach (var item in receiveMessageResponse.Messages)
            {
                _logger.LogInformation("SQS Messages received:", item);
                await PerformCRUDOperations(item);

                // Delete the received message from the queue.
                var deleteMessageRequest = new DeleteMessageRequest
                {
                    QueueUrl = queueUrl,
                    ReceiptHandle = item.ReceiptHandle
                };

                await client.DeleteMessageAsync(deleteMessageRequest);
            }

            return receiveMessageResponse;
        }

        public async Task PerformCRUDOperations(Amazon.SQS.Model.Message message)
        {
            var client = new AmazonDynamoDBClient();
            DynamoDBContext context = new DynamoDBContext(client);

            var snsMsg = JsonSerializer.Deserialize<PaylaodMsg>(message.Body);
            Book myBook = JsonSerializer.Deserialize<Book>(snsMsg.Message);

            await context.SaveAsync(myBook);


            _logger.LogInformation("Book Saved:", myBook);
        }
    }
}
