using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Sampling;

namespace WorkerIntegration;
public class Worker : BackgroundService
{
    private const string XRAY_SERVICE_NAME = "worker-integration";
    private readonly ILogger<Worker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonS3 _s3Client;

    public Worker(ILogger<Worker> logger, IAmazonSQS sqsClient, IAmazonS3 s3Client)
    {
        _logger = logger;
        _sqsClient = sqsClient;
        _s3Client = s3Client;
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
            //Create Segment with Propagated TraceId
            var tracerAtt = msgItem.Attributes.GetValueOrDefault("AWSTraceHeader");
            TraceHeader traceInfo = TraceHeader.FromString(tracerAtt);
            AWSXRayRecorder.Instance.BeginSegment(XRAY_SERVICE_NAME, traceInfo.RootTraceId, traceInfo.ParentId, new SamplingResponse(traceInfo.Sampled));

            await PerformCRUDOperations(msgItem);

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
            _logger.LogInformation($"SQS Message Attributes {JsonSerializer.Serialize(msgItem.Attributes)}");
        }

        return receivedMessageResponse?.Messages?.Select(s => s.MessageId).ToArray();
    }

    public async Task PerformCRUDOperations(Amazon.SQS.Model.Message message)
    {
        var snsMsg = JsonSerializer.Deserialize<PaylaodMsg>(message.Body);
        Book myBook = JsonSerializer.Deserialize<Book>(snsMsg.Message);

        var putRequest1 = new PutObjectRequest
        {
            BucketName = Environment.GetEnvironmentVariable("WORKER_BUCKET_NAME"),
            Key = $"books/{myBook.Id}.json",
            ContentBody = snsMsg.Message
        };

        PutObjectResponse response1 = await _s3Client.PutObjectAsync(putRequest1);

        var putRequest2 = new PutObjectRequest
        {
            BucketName = Environment.GetEnvironmentVariable("WORKER_BUCKET_NAME"),
            Key = $"sns_metadata/{message.MessageId}.json",
            ContentBody = message.Body
        };

        PutObjectResponse response2 = await _s3Client.PutObjectAsync(putRequest2);

        _logger.LogInformation($"Messages saved on S3 Bucket {putRequest1.Key} metadata seved on {putRequest2.Key} SQS Attr {JsonSerializer.Serialize(message.Attributes)}");
    }
}