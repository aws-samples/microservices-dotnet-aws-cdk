﻿// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using Amazon.CDK.AWS.IAM;
using System.Collections.Generic;

namespace Amazon.CDK.AWS.ECS.MyExtensions
{
    public static class TaskDefinitionExtensions
    {

        /// <summary>
        /// Add Sidecar container with X-Ray daemon 
        /// to gathers raw segment data, and relays it to the AWS X-Ray API.
        /// learn more at <see href="https://docs.aws.amazon.com/xray/latest/devguide/xray-daemon.html" />
        /// </summary>
        /// <param name="taskDefinition"></param>
        /// <param name="xRayDaemonProps"></param>
        /// <returns></returns>
        public static TaskDefinition AddXRayDaemon(this TaskDefinition taskDefinition, XRayDaemonProps xRayDaemonProps)
        {

            taskDefinition.AddContainer("x-ray-daemon", new ContainerDefinitionOptions
            {
                ContainerName = xRayDaemonProps.XRayDaemonContainerName,
                Cpu = 32,
                MemoryLimitMiB = 256,
                PortMappings = new PortMapping[]{
                    new PortMapping{
                        ContainerPort = 2000,
                        Protocol = Protocol.UDP
                    }},
                Image = ContainerImage.FromRegistry("public.ecr.aws/xray/aws-xray-daemon:latest"),
                Logging = xRayDaemonProps.LogDriver
            });

            //Grant permission to write X-Ray segments
            taskDefinition.TaskRole
                .AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXRayDaemonWriteAccess"));

            return taskDefinition;
        }


        /// <summary>
        /// Add Sidecar container with CloudWatch agent
        /// to send embedded metrics format logs. 
        /// Learn more at <see href="https://docs.aws.amazon.com/AmazonCloudWatch/latest/monitoring/CloudWatch_Embedded_Metric_Format_Generation_CloudWatch_Agent.html"/>
        /// </summary>
        /// <param name="taskDefinition"></param>
        /// <param name="agentProps"></param>
        /// <returns></returns>
        public static TaskDefinition AddCloudWatchAgent(this TaskDefinition taskDefinition, CloudWatchAgentProps agentProps)
        {

            //Sidecar container with CloudWatch agent
            // to send embedded metrics format logs
            // learn more at https://docs.aws.amazon.com/AmazonCloudWatch/latest/monitoring/CloudWatch_Embedded_Metric_Format_Generation_CloudWatch_Agent.html
            taskDefinition
                .AddContainer("cwagent", new ContainerDefinitionOptions
                {
                    ContainerName = agentProps.AgentContainerName,
                    Cpu = 32,
                    MemoryLimitMiB = 256,
                    PortMappings = new PortMapping[]{
                    new PortMapping{
                        ContainerPort = 25888,
                        Protocol = Amazon.CDK.AWS.ECS.Protocol.TCP
                    },
                    new PortMapping{
                        ContainerPort = 25888,
                        Protocol = Amazon.CDK.AWS.ECS.Protocol.UDP
                    }},
                    Image = ContainerImage.FromRegistry("public.ecr.aws/cloudwatch-agent/cloudwatch-agent:latest"),
                    Environment = new Dictionary<string, string>()
                        {
                            { "CW_CONFIG_CONTENT", "{\"logs\":{\"metrics_collected\":{\"emf\":{}}}}" }
                        },
                    Logging = agentProps.LogDriver
                });

            //Grant permission to the cw agent 
            taskDefinition.TaskRole
                .AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("CloudWatchAgentServerPolicy"));

            return taskDefinition;
        }

    }
}
