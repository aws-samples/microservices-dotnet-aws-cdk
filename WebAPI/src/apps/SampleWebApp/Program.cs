// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
// #pragma warning disable CA1506
using Amazon.CloudWatch.EMF.Web;
using Amazon.SimpleNotificationService;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Microsoft.OpenApi.Models;
using SampleWebApp.AppLogger;
using EMF = Amazon.CloudWatch.EMF;

const string MY_SERVICE_NAME = "demo-web-api";
Environment.SetEnvironmentVariable("MY_SERVICES_INSTANCE", $"{MY_SERVICE_NAME}/{Guid.NewGuid()}");

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options => options.FormatterName = nameof(XrayCustomFormatter))
                .AddConsoleFormatter<XrayCustomFormatter, XrayCustomFormatterOptions>();
//.AddJsonConsole();

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

//Register CloudWatch EMF for ASP.NET Core
EMF.Config.EnvironmentConfigurationProvider.Config = new EMF.Config.Configuration
{
    ServiceName = MY_SERVICE_NAME,
    ServiceType = "WebApi",
    LogGroupName = Environment.GetEnvironmentVariable("EMF_LOG_GROUP_NAME"),
    EnvironmentOverride = EMF.Environment.Environments.ECS
};
builder.Services.AddEmf();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SampleWebApp v1"));
}

//X-Ray
app.UseXRay(MY_SERVICE_NAME);

app.UseRouting();

//Register CloudWatch EMF Middleware
app.UseEmfMiddleware();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapGet("/", async context =>
    {
        await context.Response.WriteAsync("Demo .NET Microservices v2").ConfigureAwait(true);
    });
});

app.Run();
