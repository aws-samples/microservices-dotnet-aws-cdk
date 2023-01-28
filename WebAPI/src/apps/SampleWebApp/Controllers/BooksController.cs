// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
#pragma warning disable CA2007
using Microsoft.AspNetCore.Mvc;
using SampleWebApp.Entities;
using Amazon.SimpleNotificationService;
using System.Text.Json;
using Amazon.SimpleNotificationService.Model;
using Amazon.XRay.Recorder.Core;
using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;

namespace SampleWebApp.Controllers;


[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IAmazonSimpleNotificationService _client;
    private readonly ILogger _logger;
    private readonly IMetricsLogger _metrics;

    public BooksController(IAmazonSimpleNotificationService client, ILogger<BooksController> logger, IMetricsLogger metrics)
    {
        _client = client;
        _logger = logger;
        _metrics = metrics;
    }

    // POST api/books
    [HttpPost]
    public async Task<string> Post([FromBody] Book book)
    {
        _logger.LogInformation("Teste Custom log");
        if (book == null)
        {
            throw new ArgumentException("Invalid input!");
        }

        //Add business-specific tracking to measure the execution time for each Post
        // exluding the http request latency
        // Start timer
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var request = new PublishRequest
        {
            Message = JsonSerializer.Serialize(book),
            TopicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN")
        };

        var result = await _client.PublishAsync(request);

        //Stop timer
        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;

        var traceId = AWSXRayRecorder.Instance?.GetEntity()?.TraceId;
        //Add custom business-specific metrics
        EmitMetrics(book, traceId, elapsedMs);

        //Some logs
        _logger.LogInformation("Message id: {MessageId}", result.MessageId);
        _logger.LogInformation("Book {Id} is added", book.Id);
        return $"TraceId: {traceId}";
    }

    private void EmitMetrics(Book book, string traceId, long processingTimeMilliseconds)
    {
        //Add dimentions
        var dimensionSet = new DimensionSet();
        //Unique Id for this WebAPI Instance
        dimensionSet.AddDimension("WebApiInstanceId", Environment.GetEnvironmentVariable("MY_SERVICES_INSTANCE"));
        //Book's Authors
        dimensionSet.AddDimension("Authors", string.Join(",", book.BookAuthors));
        //Book's Year
        dimensionSet.AddDimension("Year", $"{book.Year}");
        _metrics.SetDimensions(dimensionSet);

        _metrics.PutMetric("PublishedMessageCount", 1, Unit.COUNT);
        _metrics.PutMetric("ProcessingTime", processingTimeMilliseconds, Unit.MILLISECONDS);

        //Add some properties
        _metrics.PutProperty("TraceId", traceId);

        _logger.LogInformation("Flushing");
        _metrics.Flush();
    }
}
