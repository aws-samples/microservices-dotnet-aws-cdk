// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.MyExtensions;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace InfraWorkerIntegration
{
    public class InfraStackDemo : Stack
    {
        internal InfraStackDemo(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            const string XRAY_DAEMON = "xray-daemon";
            const string CW_AGET = "cwagent";

            //Note: For demo' cleanup propose, this Sample Code will set RemovalPolicy == DESTROY
            //this will clean all resources when you cdk destroy
            var cleanUpRemovePolicy = RemovalPolicy.DESTROY;

            //Import Resources
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
                SecurityGroups = System.Array.Empty<SecurityGroup>()
            });

            //Import SNS Topic created from other Stack
            var topic = Topic.FromTopicArn(this, "imported-topic", importedSnsArn);

            //SQS for Worker APP that persist data on s3
            var workerIntegrationQueue = new Queue(this, "worker-integration-queue", new QueueProps
            {
                QueueName = "worker-integration-queue",
                RemovalPolicy = cleanUpRemovePolicy,
                Encryption = QueueEncryption.KMS
            });

            //Grant Permission & Subscribe
            topic.AddSubscription(new SqsSubscription(workerIntegrationQueue));

            //S3 Bucket
            var bucket = new Bucket(this, "demo-bucket", new BucketProps
            {
                Encryption = BucketEncryption.KMS,
                EnforceSSL = true,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                RemovalPolicy = cleanUpRemovePolicy,
                AutoDeleteObjects = true //Set to false for Real Env, this is only set for demo cleanup propose
            });

            //Build docker container and publish to ECR
            var asset = new DockerImageAsset(this, "worker-integration-image", new DockerImageAssetProps
            {
                Directory = Path.Combine(Directory.GetCurrentDirectory(), "../../src/apps/WorkerIntegration"),
                File = "Dockerfile",
                Platform = Platform_.LINUX_ARM64,
            });

            //Create logDrive to reuse the same AWS CloudWatch Log group created from the other Stack
            var logDriver = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                LogGroup = LogGroup.FromLogGroupName(this, "imported-loggroup", importedLogGroupName),
                StreamPrefix = "ecs/worker-integration"
            });

            //Autoscaling
            // See: https://docs.aws.amazon.com/autoscaling/ec2/userguide/as-scaling-simple-step.html
            var autoscalingSteps = new Amazon.CDK.AWS.ApplicationAutoScaling.ScalingInterval[]{
                    //Step adjustments for scale-out policy
                    new Amazon.CDK.AWS.ApplicationAutoScaling.ScalingInterval{
                        Lower = 0,
                        Upper = 10,
                        Change = 0
                    },
                    new Amazon.CDK.AWS.ApplicationAutoScaling.ScalingInterval{
                        Lower = 20,
                        Upper = null,
                        Change = 3
                    },
                    //Step adjustments for scale-in policy
                    new Amazon.CDK.AWS.ApplicationAutoScaling.ScalingInterval{
                        Lower = null,
                        Upper = -20,
                        Change = -3
                    }
                };

            //Level 3 Construct for SQS Queue processing
            var queueFargateSvc = new QueueProcessingFargateService(this, "queue-fargate-services", new QueueProcessingFargateServiceProps
            {
                Queue = workerIntegrationQueue,
                MinScalingCapacity = 1,
                MaxScalingCapacity = 100,
                ScalingSteps = autoscalingSteps,
                Cpu = 256,
                MemoryLimitMiB = 512,
                Cluster = cluster,
                Image = ContainerImage.FromDockerImageAsset(asset),
                Environment = new Dictionary<string, string>()
                        {
                            {"WORKER_QUEUE_URL", workerIntegrationQueue.QueueUrl },
                            {"WORKER_BUCKET_NAME", bucket.BucketName},
                            {"AWS_XRAY_DAEMON_ADDRESS",$"{XRAY_DAEMON}:2000" }
                        },
                LogDriver = logDriver
            });



            //Grant permission to S3 Bucket and SQS to consume message from the Queue
            bucket.GrantWrite(queueFargateSvc.TaskDefinition.TaskRole);
            workerIntegrationQueue.GrantConsumeMessages(queueFargateSvc.TaskDefinition.TaskRole);

            //Custom shared C# Library (reusability of code)
            queueFargateSvc.Service.TaskDefinition
                .AddXRayDaemon(new XRayDaemonProps
                {
                    XRayDaemonContainerName = XRAY_DAEMON,
                    LogDriver = logDriver
                }).AddCloudWatchAgent(new CloudWatchAgentProps
                {
                    AgentContainerName = CW_AGET,
                    LogDriver = logDriver,
                });

            //Grant permission to write X-Ray segments
            queueFargateSvc.Service.TaskDefinition.TaskRole
                .AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXRayDaemonWriteAccess"));

        }
    }
}
