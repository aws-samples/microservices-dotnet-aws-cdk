// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.MyExtensions;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Constructs;

namespace InfraSampleWebApp
{
    public class InfraStack : Stack
    {
        internal InfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            const string XRAY_DEAMON = "xray-daemon";
            const string CW_AGET = "cwagent";

            //Note: For demo' cleanup propose, this Sample Code will set RemovalPolicy == DESTROY
            //this will clean all resources when you cdk destroy
            var cleanUpRemovePolicy = RemovalPolicy.DESTROY;
            // VPC
            var vpc = new Vpc(this, "demo-vpc", new VpcProps
            {
                Cidr = "172.30.0.0/16",
                MaxAzs = 3,
            });

            var cluster = new Cluster(this, "demo-cluster", new ClusterProps
            {
                Vpc = vpc,
                ContainerInsights = true,
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

            //CloudWatch LogGroup and ECS LogDriver
            var logGroupName = "/ecs/demo/ecs-fargate-dotnet-microservices";
            var logDriver = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                LogGroup = new LogGroup(this, "demo-log-group", new LogGroupProps
                {
                    LogGroupName = logGroupName,
                    Retention = RetentionDays.ONE_DAY,
                    RemovalPolicy = cleanUpRemovePolicy
                }),
                StreamPrefix = "ecs/web-api"
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
                            {"ASPNETCORE_URLS","http://+:80"},
                            {"EMF_LOG_GROUP_NAME", logGroupName }
                        },
                    LogDriver = logDriver
                },
            });

            //Grant permission to Publish on the SNS Topic
            topic.GrantPublish(albFargateSvc.Service.TaskDefinition.TaskRole);


            //Custom shared C# Library (reusability of code)
            albFargateSvc.Service.TaskDefinition
                .AddXRayDeamon(new XRayDeamonProps
                {
                    XRayDeamonContainerName = XRAY_DEAMON,
                    LogDriver = logDriver
                }).AddCloudWatchAgent(new CloudWatchAgentProps
                {
                    AgentContainerName = CW_AGET,
                    LogDriver = logDriver,
                });

            //Level 1 Cfn Output
            _ = new CfnOutput(this, "DemoSnsTopicArn", new CfnOutputProps { Value = topic.TopicArn, ExportName = "DemoSnsTopicArn" });
            _ = new CfnOutput(this, "DemoClusterName", new CfnOutputProps { Value = cluster.ClusterName, ExportName = "DemoClusterName" });
            _ = new CfnOutput(this, "DemoLogGroupName", new CfnOutputProps { Value = logGroupName, ExportName = "DemoLogGroupName" });
            _ = new CfnOutput(this, "DemoVpcId", new CfnOutputProps { Value = vpc.VpcId, ExportName = "DemoVpcId" });
            _ = new CfnOutput(this, "DemoDeployRegion", new CfnOutputProps { Value = this.Region, ExportName = "DemoDeployRegion" });

        }
    }
}
