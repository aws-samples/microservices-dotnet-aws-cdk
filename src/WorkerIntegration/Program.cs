// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
global using Amazon.SQS;
global using Amazon.S3;
global using Amazon.CloudWatch.EMF.Logger;
global using Amazon.XRay.Recorder.Core;

using Amazon.XRay.Recorder.Handlers.AwsSdk;
using EMF = Amazon.CloudWatch.EMF;
using WorkerIntegration;



IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(builder =>
    {
        //Set the log level
        builder.SetMinimumLevel(LogLevel.Information)
               .AddJsonConsole();
    })
    .ConfigureServices((hostContext, services) =>
    {
        //Register X-Ray
        AWSXRayRecorder.InitializeInstance(hostContext.Configuration);
        AWSSDKHandler.RegisterXRayForAllServices();

        //Register Services
        services.AddDefaultAWSOptions(hostContext.Configuration.GetAWSOptions());
        services.AddAWSService<IAmazonS3>();
        services.AddAWSService<IAmazonSQS>();

        //Register EMF
        EMF.Config.EnvironmentConfigurationProvider.Config = new EMF.Config.Configuration
        {
            ServiceName = Worker.MY_SERVICE_NAME,
            ServiceType = "WorkerServices",
            LogGroupName = Environment.GetEnvironmentVariable("EMF_LOG_GROUP_NAME"),
            EnvironmentOverride = EMF.Environment.Environments.ECS
        };
        services.AddScoped<IMetricsLogger, MetricsLogger>();
        services.AddSingleton<EMF.Environment.IEnvironmentProvider, EMF.Environment.EnvironmentProvider>();
        services.AddSingleton<EMF.Environment.IResourceFetcher, EMF.Environment.ResourceFetcher>();
        services.AddSingleton(EMF.Config.EnvironmentConfigurationProvider.Config);

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
