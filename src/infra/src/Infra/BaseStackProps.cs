// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0


namespace InfraSampleWebApp
{
    public class BaseStackProps : StackProps
    {
        /// <summary>
        /// VPC and all base network infrastructure
        /// </summary>
        public Vpc Vpc { get; set; }

        /// <summary>
        /// ECS Cluster
        /// </summary>
        public Cluster Cluster { get; set; }

        /// <summary>
        /// SNS Topic
        /// </summary> 
        public Topic Topic { get; internal set; }

        /// <summary>
        /// CW LogGroup name prefix
        /// </summary>
        public string LogGroupName { get; } = "/ecs/demo/ecs-fargate-dotnet-microservices";

        /// <summary>
        /// X-Ray Daemon Side Card Name
        /// </summary>
        public string XrayDaemonSideCardName { get; } = "xray-daemon";

        /// <summary>
        /// CloudWatch Agent Side Card Name
        /// </summary>
        public string CloudWatchAgentSideCardName { get; } = "cwagent";

        //Note: For demo' cleanup propose, this Sample Code will set RemovalPolicy == DESTROY
        //this will clean all resources when you cdk destroy
        public RemovalPolicy CleanUpRemovePolicy { get; } = RemovalPolicy.DESTROY;
        public ILogGroup LogGroup { get; internal set; }
    }
}
