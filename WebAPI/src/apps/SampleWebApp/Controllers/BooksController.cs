
using Microsoft.AspNetCore.Mvc;
using SampleWebApp.Entities;
using Amazon.SimpleNotificationService;
using System.Text.Json;
using Amazon.SimpleNotificationService.Model;

namespace SampleWebApp.Controllers;
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IAmazonSimpleNotificationService client;
    private readonly ILogger _logger;

    public BooksController(IAmazonSimpleNotificationService client, ILogger<BooksController> logger)
    {
        this.client = client;
        _logger = logger;
    }

    // POST api/books
    [HttpPost]
    public async Task<string> Post([FromBody] Book book)
    {
        if (book == null)
        {
            throw new ArgumentException("Invalid input!");
        }

        var request = new PublishRequest
        {
            Message = JsonSerializer.Serialize(book),
            TopicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN")
        };

        var result = await client.PublishAsync(request);

        _logger.LogInformation($"Message id: {result.MessageId}");
        _logger.LogInformation($"Book {book.Id} is added");
        return $"Message id: {result.MessageId}";
    }

}
