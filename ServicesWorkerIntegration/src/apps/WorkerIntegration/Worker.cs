// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Sampling;
using Amazon.CloudWatch.EMF.Model;
using Amazon.CloudWatch.EMF.Logger;

namespace WorkerIntegration;
public class Worker : BackgroundService
{
    public const string MY_SERVICE_NAME = "worker-integration";
    private readonly string _workerId;
    private readonly ILogger<Worker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonS3 _s3Client;
    private readonly IMetricsLogger _metrics;

    public Worker(ILogger<Worker> logger, IMetricsLogger metricsLogger, IAmazonSQS sqsClient, IAmazonS3 s3Client)
    {
        _workerId = $"{MY_SERVICE_NAME}/{Guid.NewGuid()}";
        _metrics = metricsLogger;
        _logger = logger;
        _sqsClient = sqsClient;
        _s3Client = s3Client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = Environment.GetEnvironmentVariable("WORKER_QUEUE_URL");

        while (!stoppingToken.IsCancellationRequested)
        {
            AWSXRayRecorder.Instance.BeginSegment(MY_SERVICE_NAME);
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
                _logger.LogInformation("Trace sent {TraceId}", traceEntity.TraceId);
            }

            await Task.Delay(1000 * 5, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // EMF Graceful Shutdown
        // to learn more read: https://github.com/awslabs/aws-embedded-metrics-dotnet#graceful-shutdown
        await _metrics.ShutdownAsync();
        await base.StopAsync(cancellationToken);
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
            //Add business-specific tracking to measure the execution time for each
            // messages after receiving it from the queue
            // Start timer
            var watch = System.Diagnostics.Stopwatch.StartNew();

            //Create Segment with Propagated TraceId
            var tracerAtt = msgItem.Attributes.GetValueOrDefault("AWSTraceHeader");
            TraceHeader traceInfo = TraceHeader.FromString(tracerAtt);
            AWSXRayRecorder.Instance.BeginSegment(MY_SERVICE_NAME, traceInfo.RootTraceId, traceInfo.ParentId, new SamplingResponse(traceInfo.Sampled));

            await PerformCRUDOperations(msgItem);

            // Delete the received message from the queue.
            await client.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = msgItem.ReceiptHandle
            });

            //Stop timer
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;

            //Close/Submmit Segment with Propagated TraceId
            var propagatedSegment = AWSXRayRecorder.Instance.GetEntity();
            AWSXRayRecorder.Instance.EndSegment(DateTime.UtcNow);
            AWSXRayRecorder.Instance.Emitter.Send(propagatedSegment);

            //Log some informations for traceability
            _logger.LogInformation("SQS Messages received id:{MessageId} recived TraceId: {TraceId}", msgItem.MessageId, propagatedSegment.TraceId);

            EmitMetrics(msgItem.Attributes, propagatedSegment.TraceId, elapsedMs);
        }

        return receivedMessageResponse?.Messages?.Select(s => s.MessageId).ToArray();
    }

    public async Task PerformCRUDOperations(Amazon.SQS.Model.Message message)
    {
        var snsMsg = JsonSerializer.Deserialize<PaylaodMsg>(message.Body);
        Book myBook = JsonSerializer.Deserialize<Book>(snsMsg.Message);
        var bucketName = Environment.GetEnvironmentVariable("WORKER_BUCKET_NAME");

        var putRequest1 = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = $"books/{myBook.Id}.json",
            ContentBody = snsMsg.Message
        };
        _ = await _s3Client.PutObjectAsync(putRequest1);

        var putRequest2 = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = $"sns_metadata/{message.MessageId}.json",
            ContentBody = message.Body
        };
        _ = await _s3Client.PutObjectAsync(putRequest2);

        _logger.LogInformation("Messages saved on S3 Bucket {Key} metadata seved on {Key} SQS Attr {}", putRequest1.Key, putRequest2.Key, JsonSerializer.Serialize(message.Attributes));

    }

    private void EmitMetrics(Dictionary<string, string> msgAttributes, string traceId, long processingTimeMilliseconds)
    {
        //Add dimentions
        var dimensionSet = new DimensionSet();
        dimensionSet.AddDimension("WorkerId", _workerId);
        _metrics.SetDimensions(dimensionSet);

        //Add custom business-specific metrics
        _metrics.PutMetric("ProcessedMessageCount", 1, Unit.COUNT);
        _metrics.PutMetric("ProcessingTime", processingTimeMilliseconds, Unit.MILLISECONDS);

        //Add some properties
        _metrics.PutProperty("TraceId", traceId);
        _metrics.PutProperty("MessageAttributes", msgAttributes);

        _logger.LogInformation("Flushing");
        _metrics.Flush();
    }
}