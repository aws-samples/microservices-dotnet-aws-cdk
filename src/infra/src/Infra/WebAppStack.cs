// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
namespace InfraSampleWebApp
{
    public class WebAppStack : Stack
    {
        internal WebAppStack(
            Construct scope,
            string id,
            BaseStackProps props)
            : base(scope, id, props)
        {
            //ECR
            //Build docker image and publish on ECR Repository
            var asset = new DockerImageAsset(
                this,
                "web-app-image", new DockerImageAssetProps
                {
                    Directory = Path.Combine(Directory.GetCurrentDirectory(), "../SampleWebApp"),
                    File = "Dockerfile.arm",
                    Platform = Platform_.LINUX_ARM64,
                }
            );

            //CloudWatch LogGroup and ECS LogDriver
            var logDriver = LogDriver.AwsLogs(new AwsLogDriverProps
            {
                LogGroup = props.LogGroup,
                StreamPrefix = "ecs/web-api"
            });

            var albFargateSvc = new ApplicationLoadBalancedFargateService(
                this,
                "demo-service",
                new ApplicationLoadBalancedFargateServiceProps
                {
                    Cluster = props.Cluster,
                    MemoryLimitMiB = 1024,
                    Cpu = 512,
                    DesiredCount = 3,
                    RuntimePlatform = new RuntimePlatform
                    {
                        CpuArchitecture = CpuArchitecture.ARM64
                    },
                    TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
                    {
                        Image = ContainerImage.FromDockerImageAsset(asset),
                        ContainerName = "web",
                        EnableLogging = true,
                        Environment = new Dictionary<string, string>()
                        {
                            {"SNS_TOPIC_ARN", props.Topic.TopicArn },
                            {"AWS_EMF_LOG_GROUP_NAME", props.LogGroupName },
                            {"ASPNETCORE_HTTP_PORTS","80,443"},
                        },
                        LogDriver = logDriver,
                    },
                }
            );

            //Autoscaling
            var scalableTarget = albFargateSvc.Service.AutoScaleTaskCount(new EnableScalingProps
            {
                MinCapacity = 1,
                MaxCapacity = 10,
            });

            scalableTarget.ScaleOnCpuUtilization("CpuScaling", new Amazon.CDK.AWS.ECS.CpuUtilizationScalingProps
            {
                TargetUtilizationPercent = 60
            });

            //Grant permission to Publish on the SNS Topic
            props.Topic.GrantPublish(albFargateSvc.Service.TaskDefinition.TaskRole);

            //Custom shared C# Library (reusability of code)
            albFargateSvc.Service.TaskDefinition
                .AddXRayDaemon(new XRayDaemonProps
                {
                    XRayDaemonContainerName = props.XrayDaemonSideCardName,
                    LogDriver = logDriver
                }).AddCloudWatchAgent(new CloudWatchAgentProps
                {
                    AgentContainerName = props.CloudWatchAgentSideCardName,
                    LogDriver = logDriver,
                });

            //Level 1 Cfn Output
            _ = new CfnOutput(this, "DemoServiceServiceURLEndpoint", new CfnOutputProps { Value = $"http://{albFargateSvc.LoadBalancer.LoadBalancerDnsName}/api/Books", ExportName = "DemoServiceServiceURLEndpoint" });

        }
    }
}
