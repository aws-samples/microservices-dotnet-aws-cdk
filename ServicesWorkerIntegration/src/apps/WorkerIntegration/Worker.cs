using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;

namespace WorkerIntegration;
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
            MaxNumberOfMessages = 10,
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
        var client = new AmazonS3Client(region: RegionEndpoint.GetBySystemName(_region));

        var snsMsg = JsonSerializer.Deserialize<PaylaodMsg>(message.Body);
        Book myBook = JsonSerializer.Deserialize<Book>(snsMsg.Message);

        var putRequest1 = new PutObjectRequest
        {
            BucketName = Environment.GetEnvironmentVariable("WORKER_BUCKET_NAME"),
            Key = $"books/{myBook.Id}.json",
            ContentBody = snsMsg.Message
        };

        PutObjectResponse response1 = await client.PutObjectAsync(putRequest1);


        var putRequest2 = new PutObjectRequest
        {
            BucketName = Environment.GetEnvironmentVariable("WORKER_BUCKET_NAME"),
            Key = $"sns_metadata/{message.MessageId}.json",
            ContentBody = message.Body
        };

        PutObjectResponse response2 = await client.PutObjectAsync(putRequest2);

        _logger.LogInformation("Messages added to S3 Bucket", response1);
        _logger.LogInformation("Messages added to S3 Bucket", response2);
    }
}