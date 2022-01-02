using Amazon.SimpleNotificationService;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Microsoft.OpenApi.Models;
using SampleWebApp.AppLogger;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options => options.FormatterName = nameof(XrayCustomFormatter))
                .AddConsoleFormatter<XrayCustomFormatter, XrayCustomFormatterOptions>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "SampleWebApp", Version = "v1" });
    });

//Register X-Ray
AWSSDKHandler.RegisterXRayForAllServices();

//Register Services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SampleWebApp v1"));
}

//X-Ray
app.UseXRay("demo-web-api");

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapGet("/", async context =>
    {
        await context.Response.WriteAsync("Demo .NET Microservices v2");
    });
});

app.Run();
