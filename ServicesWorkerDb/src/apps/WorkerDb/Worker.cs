// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2;
using System.Text.Json;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Sampling;
using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;

namespace WorkerDb;

public class Worker : BackgroundService
{
    public const string MY_SERVICE_NAME = "worker-db";
    private readonly string _workerId;
    private readonly ILogger<Worker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IMetricsLogger _metrics;
    public Worker(ILogger<Worker> logger, IMetricsLogger metricsLogger, IAmazonSQS sqsClient, IAmazonDynamoDB dynamoDbClient)
    {
        _workerId = $"{MY_SERVICE_NAME}/{Guid.NewGuid()}";
        _metrics = metricsLogger;
        _logger = logger;
        _sqsClient = sqsClient;
        _dynamoDbClient = dynamoDbClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = Environment.GetEnvironmentVariable("WORKER_QUEUE_URL");

        while (!stoppingToken.IsCancellationRequested)
        {
            AWSXRayRecorder.Instance.BeginSegment(MY_SERVICE_NAME);
            _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
            _logger.LogDebug("The SQS queue's URL is {queueUrl}", queueUrl);

            try
            {
                var messageId = await ReceiveAndDeleteMessage(_sqsClient, queueUrl);
                _logger.LogDebug("Message ID: {messageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error consuming SQS Queue");
                AWSXRayRecorder.Instance.AddException(ex);
            }
            finally
            {
                var traceEntity = AWSXRayRecorder.Instance.TraceContext.GetEntity();
                AWSXRayRecorder.Instance.EndSegment();
                AWSXRayRecorder.Instance.Emitter.Send(traceEntity);
                _logger.LogDebug("Trace sent {TraceId}", traceEntity.TraceId);
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
            MaxNumberOfMessages = 10,
            MessageAttributeNames = { "All" },
            QueueUrl = queueUrl,
            VisibilityTimeout = 120,
            WaitTimeSeconds = 20
        };

        var receivedMessageResponse = await client.ReceiveMessageAsync(receiveMessageRequest);

        foreach (var msgItem in receivedMessageResponse.Messages)
        {
            //Add business-specific tracking to measure the time execution for each
            // messages after receiving it from the queue
            // Start timer
            var watch = System.Diagnostics.Stopwatch.StartNew();

            var sqsMsg = JsonSerializer.Deserialize<PaylaodMsg>(msgItem.Body);
            var book = JsonSerializer.Deserialize<Book>(sqsMsg.Message);

            //Create Segment with Propagated TraceId
            var tracerAtt = msgItem.Attributes.GetValueOrDefault("AWSTraceHeader");
            TraceHeader traceInfo = TraceHeader.FromString(tracerAtt);
            AWSXRayRecorder.Instance.BeginSegment(MY_SERVICE_NAME, samplingResponse: new SamplingResponse(traceInfo.Sampled));
            var propagatedSegment = AWSXRayRecorder.Instance.GetEntity();
            propagatedSegment.TraceId = traceInfo.RootTraceId;
            propagatedSegment.ParentId = traceInfo.ParentId;
            AWSXRayRecorder.Instance.SetEntity(propagatedSegment);

            await PerformCRUDOperations(book);

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
            // var propagatedSegment = AWSXRayRecorder.Instance.GetEntity();
            AWSXRayRecorder.Instance.EndSegment(DateTime.UtcNow);
            AWSXRayRecorder.Instance.Emitter.Send(propagatedSegment);

            //Log some informations for traceability
            _logger.LogInformation("SQS Messages received id:{MessageId} recived TraceId: {TraceId}", msgItem.MessageId, propagatedSegment.TraceId);
            _logger.LogInformation("Book saved id:{Id} recived TraceId: {TraceId}", book.Id, propagatedSegment.TraceId);

            EmitMetrics(msgItem.Attributes, propagatedSegment.TraceId, elapsedMs);
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

