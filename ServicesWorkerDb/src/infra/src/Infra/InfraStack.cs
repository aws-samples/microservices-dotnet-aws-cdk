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
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace Infra
{
    public class InfraStack : Stack
    {
        internal InfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            const string XRAY_DEAMON = "xray-daemon";

            //Note: For demo' cleanup propose, this Sample Code will set RemovalPolicy == DESTROY
            //this will clean all resources when you cdk destroy
            var cleanUpRemovePolicy = RemovalPolicy.DESTROY;

            //Import Resources from other Stack and Local env
            var importedSnsArn = Fn.ImportValue("DemoSnsTopicArn");
            var importedClusterName = Fn.ImportValue("DemoClusterName");
            var importedLogGroupName = Fn.ImportValue("DemoLogGroupName");
            var importedVpcId = System.Environment.GetEnvironmentVariable("DEMO_VPC_ID");

            //Import VPC using the value from env variable DEMO_VPC_ID
            var vpc = Vpc.FromLookup(this, "imported-vpc", new VpcLookupOptions
            {
                VpcId = importedVpcId
            });

            //Import ECS Cluster using VPC and the imported ClusterName
            var cluster = Cluster.FromClusterAttributes(this, "imported-cluester", new ClusterAttributes
            {
                Vpc = vpc,
                ClusterName = importedClusterName, 
                SecurityGroups = new SecurityGroup[] { }
            });

            //Import SNS Topic created from other Stack
            var topic = Topic.FromTopicArn(this, "imported-topic", importedSnsArn);

            //Create SQS for Worker APP that persist data on DynamoDb
            var workerDbQueue = new Queue(this, "worker-db-queue", new QueueProps
            {
                QueueName = "worker-db-queue",
                RemovalPolicy = cleanUpRemovePolicy
            });

            //Grant Permission & Subscribe SNS Topic
            topic.AddSubscription(new SqsSubscription(workerDbQueue));

            //Create DynamoDb Table
            Table table = new Table(this, "Table", new TableProps
            {
                RemovalPolicy = cleanUpRemovePolicy,
                TableName = "BooksCatalog",
                PartitionKey = new Attribute { Name = "Id", Type = AttributeType.STRING }
            });
            //Configure AutoScaling for DynamoDb Table
            IScalableTableAttribute readScaling = table.AutoScaleReadCapacity(new EnableScalingProps { MinCapacity = 1, MaxCapacity = 50 });
            readScaling.ScaleOnUtilization(new UtilizationScalingProps
            {
                TargetUtilizationPercent = 75
            });

            //Build docker container and publish to ECR
            var asset = new DockerImageAsset(this, "worker-db-image", new DockerImageAssetProps
            {
                Directory = Path.Combine(Directory.GetCurrentDirectory(), "../../src/apps/WorkerDb"),
                File = "Dockerfile",
            });
            
            //Create logDrive to reuse the same AWS CloudWatch Log group created from the other Stack
            var logDriver = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                LogGroup = LogGroup.FromLogGroupName(this, "imported-loggroup", importedLogGroupName),
                StreamPrefix = "ecs"
            });

            //Level 3 Construct for SQS Queue processing
            var queueFargateSvc = new QueueProcessingFargateService(this, "queue-fargate-services-db", new QueueProcessingFargateServiceProps
            {
                Cluster = cluster,
                Queue = workerDbQueue,
                Cpu = 256,
                MemoryLimitMiB = 512,
                Image = ContainerImage.FromDockerImageAsset(asset),
                Environment = new Dictionary<string, string>()
                        {
                            {"WORKER_QUEUE_URL", workerDbQueue.QueueUrl },
                            {"AWS_REGION", this.Region},
                            {"ASPNETCORE_ENVIRONMENT","Development"},
                            {"AWS_XRAY_DAEMON_ADDRESS",$"{XRAY_DEAMON}:2000" }
                        },
                LogDriver = logDriver
            });

            //Grant permission to DynamoDB table and SQS to consume message from the Queue
            table.GrantWriteData(queueFargateSvc.TaskDefinition.TaskRole);
            table.Grant(queueFargateSvc.TaskDefinition.TaskRole, "dynamodb:DescribeTable");
            workerDbQueue.GrantConsumeMessages(queueFargateSvc.TaskDefinition.TaskRole);

            //Sidecar container with X-Ray deamon to 
            //gathers raw segment data, and relays it to the AWS X-Ray API
            //learn more at https://docs.aws.amazon.com/xray/latest/devguide/xray-daemon.html
            queueFargateSvc.Service.TaskDefinition
                .AddContainer("x-ray-deamon", new ContainerDefinitionOptions
                {
                    ContainerName = XRAY_DEAMON,
                    Cpu = 32,
                    MemoryLimitMiB = 256,
                    PortMappings = new PortMapping[]{
                    new PortMapping{
                        ContainerPort = 2000,
                        Protocol = Amazon.CDK.AWS.ECS.Protocol.UDP
                    }},
                    Image = ContainerImage.FromRegistry("public.ecr.aws/xray/aws-xray-daemon:latest"),
                    Logging = logDriver
                });

            //Grant permission to write X-Ray segments
            queueFargateSvc.Service.TaskDefinition.TaskRole
                .AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXRayDaemonWriteAccess"));

        }
    }
}
