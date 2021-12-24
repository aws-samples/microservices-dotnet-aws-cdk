using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.SNS;
using Constructs;

namespace Infra
{
    public class InfraStack : Stack
    {
        internal InfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            const string XRAY_DEAMON = "xray-daemon";
            // VPC
            var vpc = new Vpc(this, "demo-vpc", new VpcProps
            {
                Cidr = "172.30.0.0/16",
                MaxAzs = 3,
            });

            var cluster = new Cluster(this, "demo-cluster", new ClusterProps
            {
                Vpc = vpc,
            });

            //ECR
            //Build docker image and publish on ECR Repository
            var asset = new DockerImageAsset(this, "web-app-image", new DockerImageAssetProps
            {
                Directory = Path.Combine(Directory.GetCurrentDirectory(), "../../src/apps/SampleWebApp"),
                File = "Dockerfile"
            });

            //SNS Topic
            Topic topic = new Topic(this, "Topic", new TopicProps
            {
                DisplayName = "Customer subscription topic",
                TopicName = "demo-web-app-topic"
            });

            var albFargateSvc = new ApplicationLoadBalancedFargateService(this, "demo-service", new ApplicationLoadBalancedFargateServiceProps
            {
                Cluster = cluster,
                MemoryLimitMiB = 1024,
                Cpu = 512,
                TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
                {
                    Image = ContainerImage.FromDockerImageAsset(asset),
                    ContainerName = "web",
                    EnableLogging = true,
                    Environment = new Dictionary<string, string>()
                        {
                            {"SNS_TOPIC_ARN", topic.TopicArn },
                            {"AWS_REGION", this.Region},
                            {"ASPNETCORE_ENVIRONMENT","Development"},
                            {"ASPNETCORE_URLS","http://+:80"},
                            // {"AWS_XRAY_DAEMON_ADDRESS",$"{XRAY_DEAMON}:2000" }
                        }
                },
            });  

            albFargateSvc.Service.TaskDefinition
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
                });

            topic.GrantPublish(albFargateSvc.Service.TaskDefinition.TaskRole);

            albFargateSvc.Service.TaskDefinition.TaskRole
                .AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXRayDaemonWriteAccess"));

            new CfnOutput(this, "DemoSnsTopicArn", new CfnOutputProps { Value = topic.TopicArn, ExportName = "DemoSnsTopicArn" });
            new CfnOutput(this, "DemoVpcId", new CfnOutputProps { Value = vpc.VpcId, ExportName = "DemoVpcId" });
            
        }
    }
}