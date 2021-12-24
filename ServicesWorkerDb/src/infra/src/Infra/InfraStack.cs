using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
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
            //Note: For demo' cleanup propose, this Sample Code will set RemovalPolicy == DESTROY
            var cleanUpRemovePolicy = RemovalPolicy.DESTROY;

            //Import Resources
            var importedSnsArn = Fn.ImportValue("DemoSnsTopicArn");
            var importedVpcId = "YOUR_VPC_ID";

            var vpc = Vpc.FromLookup(this, "imported-vpc", new VpcLookupOptions
            {
                VpcId = importedVpcId
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
            //Configure AutoScaling DynamoDb Table
            IScalableTableAttribute readScaling = table.AutoScaleReadCapacity(new EnableScalingProps { MinCapacity = 1, MaxCapacity = 50 });

            readScaling.ScaleOnUtilization(new UtilizationScalingProps
            {
                TargetUtilizationPercent = 75
            });

        
            var asset = new DockerImageAsset(this, "worker-db-image", new DockerImageAssetProps
            {
                Directory = Path.Combine(Directory.GetCurrentDirectory(), "../../src/apps/WorkerDb"),
                File = "Dockerfile",
                
            });

            var queueFargateSvc = new QueueProcessingFargateService(this, "queue-fargate-services-db", new QueueProcessingFargateServiceProps
            {
                Queue = workerDbQueue,
                Cpu = 256,
                MemoryLimitMiB = 512,
                Vpc = vpc,
                Image = ContainerImage.FromDockerImageAsset(asset),
                Environment = new Dictionary<string, string>()
                        {
                            {"WORKER_QUEUE_URL", workerDbQueue.QueueUrl },
                            {"AWS_REGION", this.Region},
                            {"ASPNETCORE_ENVIRONMENT","Development"}
                        }
            });

            table.GrantWriteData(queueFargateSvc.TaskDefinition.TaskRole);
            table.Grant(queueFargateSvc.TaskDefinition.TaskRole, "dynamodb:DescribeTable");
            workerDbQueue.GrantConsumeMessages(queueFargateSvc.TaskDefinition.TaskRole);
            
        }
    }
}
