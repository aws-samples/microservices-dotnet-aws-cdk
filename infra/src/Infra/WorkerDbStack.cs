// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Amazon.CDK.AWS.ECS.MyExtensions;
using System;

namespace InfraSampleWebApp;

public class WorkerDbStack : Stack
{
    public WorkerDbStack(
        Construct scope,
        string id,
        BaseStackProps props = null)
        : base(scope, id, props)
    {

        //Create SQS for Worker APP that persist data on DynamoDb
        var workerDbQueue = new Queue(this, "worker-db-queue", new QueueProps
        {
            QueueName = "worker-db-queue",
            RemovalPolicy = props.CleanUpRemovePolicy
        });

        //Grant Permission & Subscribe SNS Topic
        props.Topic.AddSubscription(new SqsSubscription(workerDbQueue));

        //Create DynamoDb Table
        var table = new Table(this, "Table", new TableProps
        {
            RemovalPolicy = props.CleanUpRemovePolicy,
            TableName = "BooksCatalog",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "Id",
                Type = AttributeType.STRING
            },
            Encryption = TableEncryption.AWS_MANAGED
        });

        //Configure AutoScaling for DynamoDb Table
        IScalableTableAttribute readScaling = table.AutoScaleReadCapacity(
            new Amazon.CDK.AWS.DynamoDB.EnableScalingProps
            {
                MinCapacity = 1,
                MaxCapacity = 50
            }
        );
        readScaling.ScaleOnUtilization(new UtilizationScalingProps
        {
            TargetUtilizationPercent = 75
        });

        //Build docker container and publish to ECR
        var asset = new DockerImageAsset(this, "worker-db-image", new DockerImageAssetProps
        {
            Directory = Path.Combine(Directory.GetCurrentDirectory(), "../WorkerDb"),
            File = "Dockerfile",
            Platform = Platform_.LINUX_ARM64,
        });

        //Create logDrive to reuse the same AWS CloudWatch Log group created from the other Stack
        var logDriver = LogDriver.AwsLogs(new AwsLogDriverProps
        {
            LogGroup = props.LogGroup,
            StreamPrefix = "ecs/worker-db"
        });

        //Autoscaling
        // See: https://docs.aws.amazon.com/autoscaling/ec2/userguide/as-scaling-simple-step.html
        var autoscalingSteps = new Amazon.CDK.AWS.ApplicationAutoScaling.ScalingInterval[]{
            //Step adjustments for scale-out policy
            new (){
                Lower = 0,
                Upper = 10,
                Change = 0
            },
            new (){
                Lower = 20,
                Upper = null,
                Change = 3
            },
             new (){
                Lower = null,
                Upper = -20,
                Change = -3
            },
        };

        //Level 3 Construct for SQS Queue processing
        var queueFargateSvc = new QueueProcessingFargateService(
            this, "queue-fargate-services-db",
            new QueueProcessingFargateServiceProps
            {
                Cluster = props.Cluster,
                Queue = workerDbQueue,
                MinScalingCapacity = 1,
                MaxScalingCapacity = 100,
                ScalingSteps = autoscalingSteps,
                Cpu = 256,
                MemoryLimitMiB = 512,
                RuntimePlatform = new RuntimePlatform
                {
                    CpuArchitecture = CpuArchitecture.ARM64
                },
                Image = ContainerImage.FromDockerImageAsset(asset),
                Environment = new Dictionary<string, string>()
                {
                    {"WORKER_QUEUE_URL", workerDbQueue.QueueUrl },
                    {"AWS_REGION", this.Region},
                    {"AWS_XRAY_DAEMON_ADDRESS",$"{props.XrayDaemonSideCardName}:2000" },
                    {"EMF_LOG_GROUP_NAME", props.LogGroup.LogGroupName }
                },
                LogDriver = logDriver,
            });

        //Grant permission to DynamoDB table and SQS to consume message from the Queue
        table.GrantWriteData(queueFargateSvc.TaskDefinition.TaskRole);
        table.Grant(queueFargateSvc.TaskDefinition.TaskRole, "dynamodb:DescribeTable");
        workerDbQueue.GrantConsumeMessages(queueFargateSvc.TaskDefinition.TaskRole);

        //Custom shared C# Library (reusability of code)
        queueFargateSvc.Service.TaskDefinition
            .AddXRayDaemon(new XRayDaemonProps
            {
                XRayDaemonContainerName = props.XrayDaemonSideCardName,
                LogDriver = logDriver
            }).AddCloudWatchAgent(new CloudWatchAgentProps
            {
                AgentContainerName = props.CloudWatchAgentSideCardName,
                LogDriver = logDriver,
            });
    }
}
