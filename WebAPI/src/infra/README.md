# Welcome to your CDK C# project!

This is a blank project for C# development with CDK.

The `cdk.json` file tells the CDK Toolkit how to execute your app.

It uses the [.NET Core CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project.

## Useful commands

* `dotnet build src` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template


```bash
export DEMO_VPC_ID=$(aws cloudformation describe-stacks --output text --query 'Stacks[0].Outputs[?OutputKey==`DemoVpcId`].OutputValue  | [0]')
```