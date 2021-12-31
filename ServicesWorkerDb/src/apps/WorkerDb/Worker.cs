using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2;
using System.Text.Json;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Sampling;

namespace WorkerDb;

public class Worker : BackgroundService
{
    private const string XRAY_SERVICE_NAME = "worker-db";
    private readonly ILogger<Worker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonDynamoDB _dynamoDbClient;

    public Worker(ILogger<Worker> logger, IAmazonSQS sqsClient, IAmazonDynamoDB dynamoDbClient)
    {
        _logger = logger;
        _sqsClient = sqsClient;
        _dynamoDbClient = dynamoDbClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = Environment.GetEnvironmentVariable("WORKER_QUEUE_URL");

        while (!stoppingToken.IsCancellationRequested)
        {
            AWSXRayRecorder.Instance.BeginSegment(XRAY_SERVICE_NAME);
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            _logger.LogInformation("The SQS queue's URL is {queueUrl}", queueUrl);

            try
            {
                var messageId = await ReceiveAndDeleteMessage(_sqsClient, queueUrl);
                _logger.LogInformation("Message ID: {messageId}", messageId);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "error consuming SQS Queue");
                AWSXRayRecorder.Instance.AddException(ex);
            }
            finally
            {
                var traceEntity = AWSXRayRecorder.Instance.TraceContext.GetEntity();
                AWSXRayRecorder.Instance.EndSegment();
                AWSXRayRecorder.Instance.Emitter.Send(traceEntity);
                _logger.LogInformation($"Trace sent {traceEntity.TraceId}");
            }

            await Task.Delay(1000 * 5, stoppingToken);
        }
    }

    /// <summary>
    /// Retrieves the message from the SQS queue preserve 
    /// the propagated Trace Id from SNS 
    /// and persisted the Book into DynamoDB
    /// </summary>
    /// <param name="client">The SQS client used to retrieve a message.</param>
    /// <param name="queueUrl">The URL of the queue from which to retrieve
    /// a message.</param>
    /// <returns>MessageIds processed</returns>
    public async Task<string[]> ReceiveAndDeleteMessage(IAmazonSQS client, string queueUrl)
    {
        // Receive a single message from the queue. 
        var receiveMessageRequest = new ReceiveMessageRequest
        {
            AttributeNames = { "All" },
            MaxNumberOfMessages = 1,
            MessageAttributeNames = { "All" },
            QueueUrl = queueUrl,
            VisibilityTimeout = 120,
            WaitTimeSeconds = 20
        };

        var receivedMessageResponse = await client.ReceiveMessageAsync(receiveMessageRequest);

        foreach (var msgItem in receivedMessageResponse.Messages)
        {
            var sqsMsg = JsonSerializer.Deserialize<PaylaodMsg>(msgItem.Body);
            var book = JsonSerializer.Deserialize<Book>(sqsMsg.Message);

            //Create Segment with Propagated TraceId
            var tracerAtt = msgItem.Attributes.GetValueOrDefault("AWSTraceHeader");
            TraceHeader traceInfo = TraceHeader.FromString(tracerAtt);
            AWSXRayRecorder.Instance.BeginSegment(XRAY_SERVICE_NAME, traceInfo.RootTraceId, traceInfo.ParentId, new SamplingResponse(traceInfo.Sampled));

            await PerformCRUDOperations(book);

            // Delete the received message from the queue.
            await client.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = msgItem.ReceiptHandle
            });

            //Close/Submmit Segment with Propagated TraceId
            var propagatedSegment = AWSXRayRecorder.Instance.GetEntity();
            AWSXRayRecorder.Instance.EndSegment(DateTime.UtcNow);
            AWSXRayRecorder.Instance.Emitter.Send(propagatedSegment);

            //Log some informations for traceability
            _logger.LogInformation($"SQS Messages received id:{msgItem.MessageId} recived TraceId: {propagatedSegment.TraceId}");
            _logger.LogInformation($"Book saved id:{book.Id} recived TraceId: {propagatedSegment.TraceId}");
            _logger.LogInformation($"SQS Message Attributes {JsonSerializer.Serialize(msgItem.Attributes)}");
        }

        return receivedMessageResponse?.Messages?.Select(s => s.MessageId).ToArray();
    }

    /// <summary>
    /// Performe Inser or Update book 
    /// </summary>
    /// <param name="book"></param>
    /// <returns></returns>
    public async Task PerformCRUDOperations(Book book)
    {
        DynamoDBContext context = new DynamoDBContext(_dynamoDbClient);
        await context.SaveAsync(book);
    }
}

