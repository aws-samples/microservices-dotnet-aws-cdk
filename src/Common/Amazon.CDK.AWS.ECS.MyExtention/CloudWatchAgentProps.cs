// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
namespace Amazon.CDK.AWS.ECS.MyExtensions
{
    public class CloudWatchAgentProps
    {
        /// <summary>
        /// Default: cwagent
        /// </summary>
        public string AgentContainerName { get; set; } = "cwagent";

        /// <summary>
        /// Default: null
        /// </summary>
        public LogDriver LogDriver { get; set; } = null;
    }
}
