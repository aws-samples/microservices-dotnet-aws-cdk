// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using Amazon.S3;
using Amazon.SQS;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using WorkerIntegration;
using emf = Amazon.CloudWatch.EMF;


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
        services.AddSingleton<emf.Logger.IMetricsLogger, emf.Logger.MetricsLogger>(f =>
        {
            // Manually setup the configuration for the library
            var configuration = new emf.Config.Configuration
            {
                ServiceName = "worker-db",
                ServiceType = "WorkerServices",
                LogGroupName = Environment.GetEnvironmentVariable("EMF_LOG_GROUP_NAME"),
                EnvironmentOverride = emf.Environment.Environments.ECS
            };

            var loggerFactory = f.GetService<ILoggerFactory>();
            // create the logger using a DefaultEnvironment which will write over TCP
            var _emfEnv = new emf.Environment.DefaultEnvironment(configuration, loggerFactory);

            return new emf.Logger.MetricsLogger(_emfEnv, loggerFactory);
        });

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
