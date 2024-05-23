// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
global using Amazon.CDK;
global using System.Collections.Generic;
global using System.IO;
global using Amazon.CDK.AWS.EC2;
global using Amazon.CDK.AWS.Ecr.Assets;
global using Amazon.CDK.AWS.ECS;
global using Amazon.CDK.AWS.ECS.MyExtensions;
global using Amazon.CDK.AWS.ECS.Patterns;
global using Amazon.CDK.AWS.Logs;
global using Amazon.CDK.AWS.SNS;
global using Constructs;
global using Amazon.CDK.AWS.ApplicationAutoScaling;


namespace InfraSampleWebApp
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            var baseInfra = new BaseInfraStack(app, "BaseInfraStack", new StackProps { });

            //Web App Stack
            _ = new WebAppStack(app, "WebAppStack", baseInfra.BaseStackProps);

            //Worker Db Stack
            _ = new WorkerDbStack(app, "WorkerDbStack", baseInfra.BaseStackProps);

            //Worker Integration Stack
            _ = new WorkerIntegrationStack(app, "WorkerIntegrationStack", baseInfra.BaseStackProps);

            app.Synth();
        }
    }
}
