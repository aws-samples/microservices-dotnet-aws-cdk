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

            //Import Resources
            var importedSnsArn = Fn.ImportValue("DemoSnsTopicArn");
            var importedClusterName = Fn.ImportValue("DemoClusterName");
            var importedVpcId = System.Environment.GetEnvironmentVariable("DEMO_VPC_ID");

            var vpc = Vpc.FromLookup(this, "imported-vpc", new VpcLookupOptions
            {
                VpcId = importedVpcId
            });

            var cluster = Cluster.FromClusterAttributes(this, "imported-cluester", new ClusterAttributes
            {
                Vpc = vpc,
                ClusterName = importedClusterName, 
                SecurityGroups = new SecurityGroup[] { }
            });

            var topic = Topic.FromTopicArn(this, "imported-topic", importedSnsArn);

            //SQS for Worker APP that persist data on s3
            var workerDbQueue = new Queue(this, "worker-db-queue", new QueueProps
            {
                QueueName = "worker-db-queue",
                RemovalPolicy = cleanUpRemovePolicy
            });

            //Grant Permission & Subscribe
            topic.AddSubscription(new SqsSubscription(workerDbQueue));

            //DynamoDb Table
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

            //CloudWatch LogGroup and ECS LogDriver
            var logDriver = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                LogGroup = new LogGroup(this, "demo-log-group", new LogGroupProps
                {
                    LogGroupName = "/ecs/worker-db/ecs-fargate-cwagent",
                    Retention = RetentionDays.ONE_DAY,
                    RemovalPolicy = cleanUpRemovePolicy
                }),
                StreamPrefix = "ecs"
            });

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

            table.GrantWriteData(queueFargateSvc.TaskDefinition.TaskRole);
            table.Grant(queueFargateSvc.TaskDefinition.TaskRole, "dynamodb:DescribeTable");
            workerDbQueue.GrantConsumeMessages(queueFargateSvc.TaskDefinition.TaskRole);

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

            queueFargateSvc.Service.TaskDefinition.TaskRole
                .AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXRayDaemonWriteAccess"));

        }
    }
}
