using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.S3;
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
            var importedVpcId = "YOUR_VPC_ID"; //Fn.ImportValue("DemoVpcId");

            var vpc = Vpc.FromLookup(this, "imported-vpc", new VpcLookupOptions
            {
                VpcId = importedVpcId
            });

            var topic = Topic.FromTopicArn(this, "imported-topic", importedSnsArn);

            //SQS for Worker APP that persist data on s3
            var workerIntegrationQueue = new Queue(this, "worker-integration-queue", new QueueProps
            {
                QueueName = "worker-integration-queue",
                RemovalPolicy = cleanUpRemovePolicy
            });

            //Grant Permission & Subscribe
            topic.AddSubscription(new SqsSubscription(workerIntegrationQueue));

            //S3 Bucket
            var bucket = new Bucket(this, "demo-bucket", new BucketProps
            {
                Encryption = BucketEncryption.S3_MANAGED,
                EnforceSSL = true,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                RemovalPolicy = cleanUpRemovePolicy,
                AutoDeleteObjects = true //Set to false for Real Env, this is only set for demo cleanup propose
            });

        
            var asset = new DockerImageAsset(this, "worker-integration-image", new DockerImageAssetProps
            {
                Directory = Path.Combine(Directory.GetCurrentDirectory(), "../../src/apps/WorkerIntegration"),
                File = "Dockerfile",
            });

            var queueFargateSvc = new QueueProcessingFargateService(this, "queue-fargate-services", new QueueProcessingFargateServiceProps
            {
                Queue = workerIntegrationQueue,
                Cpu = 256,
                MemoryLimitMiB = 512,
                Vpc = vpc,
                Image = ContainerImage.FromDockerImageAsset(asset),
                Environment = new Dictionary<string, string>()
                        {
                            {"WORKER_QUEUE_URL", workerIntegrationQueue.QueueUrl },
                            {"WORKER_BUCKET_NAME", bucket.BucketName},
                            {"AWS_REGION", this.Region},
                            {"ASPNETCORE_ENVIRONMENT","Development"}
                        }
            });

            bucket.GrantWrite(queueFargateSvc.TaskDefinition.TaskRole);
            
            workerIntegrationQueue.GrantConsumeMessages(queueFargateSvc.TaskDefinition.TaskRole);

        }
    }
}
