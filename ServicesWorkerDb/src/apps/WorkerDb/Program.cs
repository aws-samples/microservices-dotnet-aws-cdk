// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using Amazon.DynamoDBv2;
using Amazon.SQS;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using WorkerDb;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {

        //Register X-Ray
        AWSXRayRecorder.InitializeInstance(hostContext.Configuration);
        AWSSDKHandler.RegisterXRayForAllServices();

        //Register DI Services
        services.AddDefaultAWSOptions(hostContext.Configuration.GetAWSOptions());
        services.AddAWSService<IAmazonDynamoDB>();
        services.AddAWSService<IAmazonSQS>();

        //Register Worker Service
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
