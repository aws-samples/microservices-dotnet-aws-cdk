// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
namespace Amazon.CDK.AWS.ECS.MyExtensions
{
    public class XRayDeamonProps
    {
        /// <summary>
        /// Default: xray-daemon
        /// </summary>
        public string XRayDeamonContainerName { get; set; } = "xray-daemon";

        /// <summary>
        /// Default: null
        /// </summary>
        public LogDriver LogDriver { get; set; }  = null;
    }
}
