# Code Snippet

## Logs

Worker Services: Logging the Trace ID in each Log

```c#
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    ...
    while (!stoppingToken.IsCancellationRequested)
    {
        ...
        try
        {
            var messageId = await ReceiveAndDeleteMessage(_sqsClient, queueUrl);
            _logger.LogInformation("Message ID: {messageId}, TraceId: {TraceId}", messageId, traceEntity.TraceId);
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
       ...
    }
}
```

Web API: Using Formatter to Log Trace ID

```c#
...
var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options => options.FormatterName = nameof(XrayCustomFormatter))
                .AddConsoleFormatter<XrayCustomFormatter, XrayCustomFormatterOptions>();
...
app.Run();

```

X-Ray Log Formatter

```c#
public class XrayCustomFormatter : ConsoleFormatter, IDisposable
{
    ...
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider scopeProvider,
        TextWriter textWriter)
    {
        ...
        WriteTraceIdSuffix(textWriter);
        ...
    }

    private void WriteTraceIdSuffix(TextWriter textWriter)
    {
        if (_formatterOptions.EnableTraceIdInjection && AWSXRayRecorder.Instance.IsEntityPresent())
        {
            textWriter.Write($"TraceId: {AWSXRayRecorder.Instance?.GetEntity()?.TraceId}");
        }
    }
    ...
}
```

Controller

```C#
// POST api/books
    [HttpPost]
    public async Task<string> Post([FromBody] Book book)
    {
        _logger.LogInformation("Teste Custom log");
        ...
        var traceId = AWSXRayRecorder.Instance?.GetEntity()?.TraceId;
        ...
        _logger.LogInformation("Message id: {MessageId}", result.MessageId);
        _logger.LogInformation("Book {Id} is added", book.Id);
        return $"TraceId: {traceId}";
    }
```

Open-Telemetry Logging

```c#
builder.Logging
    .Configure(options =>
    {
        //Automatically add TraceId, SpanId ,ParentId, Baggage, and Tags.
        options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId 
                                            | ActivityTrackingOptions.SpanId  
                                            | ActivityTrackingOptions.ParentId 
                                            | ActivityTrackingOptions.Baggage 
                                            | ActivityTrackingOptions.Tags;
    })
    .AddOpenTelemetry()
    .AddJsonConsole(cfn => cfn.IncludeScopes = true);
```

## Metrics

```c#
...
const string MY_SERVICE_NAME = "demo-web-api";
Environment.SetEnvironmentVariable("MY_SERVICES_INSTANCE", $"{MY_SERVICE_NAME}/{Guid.NewGuid()}");
var builder = WebApplication.CreateBuilder(args);
...
//Register CloudWatch EMF for ASP.NET Core
EMF.Config.EnvironmentConfigurationProvider.Config = new EMF.Config.Configuration
{
    ServiceName = MY_SERVICE_NAME,
    ServiceType = "WebApi",
    LogGroupName = Environment.GetEnvironmentVariable("AWS_EMF_LOG_GROUP_NAME"),
    EnvironmentOverride = EMF.Environment.Environments.ECS
};
builder.Services.AddEmf()
...
//Register CloudWatch EMF Middleware
app.UseEmfMiddleware((context, logger) =>
{
    if (logger == null)
    {
        return Task.CompletedTask;
    }
    logger.PutMetadata("MY_SERVICES_INSTANCE", Environment.GetEnvironmentVariable("MY_SERVICES_INSTANCE"));
    return Task.CompletedTask;
});
...
app.Run();
```

Controller

```c#
...
 public BooksController(IAmazonSimpleNotificationService client, ILogger<BooksController> logger, IMetricsLogger metrics)
    {
        ...
        _metrics = metrics;
    }
...
// POST api/books
[HttpPost]
public async Task<string> Post([FromBody] Book book)
{
    ...
    var traceId = AWSXRayRecorder.Instance?.GetEntity()?.TraceId;
    //Add custom business-specific metrics
    EmitMetrics(book, traceId, elapsedMs);
    ...
    return $"TraceId: {traceId}";
}
...
private void EmitMetrics(Book book, string traceId, long processingTimeMilliseconds)
{
    //Add Dimensions
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
```

Open-Telemetry Metrics + Native .NET Metrics

```c#
//Set OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(b =>
    {
        b.AddService(serviceName: "sample-web-api") //Set My services name
         .AddDetector(new AWSECSResourceDetector())  //Detect ECS Container Details
         .AddAttributes(new Dictionary<string, object>
         {
             ["environment.name"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
             ["team.name"] = "sample demo"
         })
         .AddTelemetrySdk();
    })
    .WithMetrics(b =>  //Add metrics
    {
        b.AddAspNetCoreInstrumentation()
         .AddMeter(MyBusinessMetrics.InstrumentationName)
         .AddMeter(InstrumentationOptions.MeterName) // MassTransit Meter
         .AddAspNetCoreInstrumentation() //Automatic Metrics for ASP.NET Core Requests
         .AddOtlpExporter(options =>
         {
             options.Endpoint = new Uri(exporterEndpoint);
         });
    });
```

My Custom Metric

```c#
public sealed class MyBusinessMetrics : IDisposable
{
    ...
    public MyBusinessMetrics()
    {
        meter = new Meter(InstrumentationName, InstrumentationVersion);
        BookCounter = meter.CreateCounter<int>(
            "custom.success_book_counter",
            "Count", "A count successful book stored");
        InvalidPayload = meter.CreateCounter<int>(
            "custom.invalid_payload_counter",
            "Count", "A count of invalid payload received");
    }

    public Counter<int> BookCounter { get; private set; }
    public Counter<int> InvalidPayload { get; private set; }
}
```

Controller

```c#
public class BookController : ControllerBase
{   ...
    private readonly MyBusinessMetrics _businessMetrics;
    public BookController(ILogger<BookController> logger, 
        IPublishEndpoint publishEndpoint, 
        MyBusinessMetrics businessMetrics)
    {
        ...
        _businessMetrics = businessMetrics;
    }

    [HttpPost]
    public async Task<ActionResult> Post([FromBody] Book book)
    {
        if (book == null
            || book.Id == Guid.Empty
            || string.IsNullOrEmpty(book.Title)
            || book.Genre == BookGenre.UnSpecified)
        {
            const string msg = "Invalid payload. The book must have Id, Title and Genre";
            _businessMetrics.InvalidPayload.Add(1); //Increment failure metric
            _logger.LogInformation(msg);
            return Problem(detail: msg, statusCode: 422, title: "UnProcessable Entity");
        }
        ...
        var tags = new TagList //dimensions
        {
            { "BookGenre", Enum.GetName(typeof(BookGenre), book.Genre) },
            { "Year", book.Year }
        };
        _businessMetrics.BookCounter.Add(1, tags); //Increment Successful Book Counter with some Tags

        return Ok($"TraceId: {Activity.Current.TraceId}");
    }
}
```

## X-Ray

X-Ray WEB API setup

```c#
var builder = WebApplication.CreateBuilder(args);
...
//Register X-Ray
AWSSDKHandler.RegisterXRayForAllServices();

//Register Services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
...
//Initialize X-Ray
app.UseXRay(MY_SERVICE_NAME);
...
app.Run();

```

X-Ray Worker Services

```c#
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    ...
    while (!stoppingToken.IsCancellationRequested)
    {
        AWSXRayRecorder.Instance.BeginSegment(MY_SERVICE_NAME);
        ...
        try
        {
            var messageId = await ReceiveAndDeleteMessage(_sqsClient, queueUrl);
            var traceEntity = AWSXRayRecorder.Instance.TraceContext.GetEntity();
            _logger.LogInformation("Message ID: {messageId}, TraceId: {TraceId}", messageId, traceEntity.TraceId);
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
        ...
    }
}

public async Task<string[]> ReceiveAndDeleteMessage(IAmazonSQS client, string queueUrl)
{   
    ...
    var receivedMessageResponse = await client.ReceiveMessageAsync(receiveMessageRequest);
    foreach (var msgItem in receivedMessageResponse.Messages)
    {
       ...
        //Create Segment with Propagated TraceId
        var tracerAtt = msgItem.Attributes.GetValueOrDefault("AWSTraceHeader");
        TraceHeader traceInfo = TraceHeader.FromString(tracerAtt);
        AWSXRayRecorder.Instance.BeginSegment(MY_SERVICE_NAME, new SamplingResponse(traceInfo.Sampled));

        var propagatedSegment = AWSXRayRecorder.Instance.GetEntity();
        propagatedSegment.TraceId = traceInfo.RootTraceId;
        propagatedSegment.ParentId = traceInfo.ParentId;
        AWSXRayRecorder.Instance.SetEntity(propagatedSegment);

        await PerformCRUDOperations(book);
        ...
        //Close/Submmit Segment with Propagated TraceId
        AWSXRayRecorder.Instance.EndSegment(DateTime.UtcNow);
        AWSXRayRecorder.Instance.Emitter.Send(propagatedSegment);
        ...
    }
    return receivedMessageResponse?.Messages?.Select(s => s.MessageId).ToArray();
}
```

Trace Open-Telemetry

```c#
//Set OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(b =>
    {
        b.AddService(serviceName: "sample-web-api") //Set My services name
         .AddDetector(new AWSECSResourceDetector())  //Detect ECS Container Details
         .AddAttributes(new Dictionary<string, object>
         {
             ["environment.name"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
             ["team.name"] = "sample demo"
         })
         .AddTelemetrySdk();
    })
    .WithTracing(b => //Add Tracing
    {
        b.AddXRayTraceId()                  //for generating AWS X-Ray compliant trace IDs
         .AddAWSInstrumentation()           //for tracing calls to AWS services via AWS SDK for .Net
         .AddAspNetCoreInstrumentation()    //AspNet Instrumentation
         .AddMassTransitInstrumentation()   //Instrument MassTransit
         .AddSource(DiagnosticHeaders.DefaultListenerName) // MassTransit ActivitySource
         .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(exporterEndpoint); //Add endpoint to the side-car OTEL collector exporter
            });
    });
```

## Prompt

```txt
    colima start -a x86_64
    Generate JSON payload for the open class
```

```JSON
{
  "Title": "The Hitchhiker's Guide to the Galaxy",
  "ISBN": "978034",
  "Authors": [
    "Douglas Adams"
  ],
  "CoverPage": "https://example.com/book-cover.jpg",
  "Year": 1979
}

```
