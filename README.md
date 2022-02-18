## Sample .NET6 Worker Services with AWS CDK and AWS Fargate

This repository contains a sample implementation of Fanout Architecture using ".NET6" Worker Services to process messages from SNS Topic and SQS Queue. Since the Worker Services would have no UI, to operate this solution, you need Observability implemented. In this repository, you can also find sample .NET Observability implementation using the combination of AWS X-Ray and AWS CloudWatch. To provision this solution, you can use AWS CDK to implement your modern Infrastructure as Code, using .NET C# to provision all AWS Resources your application needs.

### Architecture
[TODO: add Image here]

[TODO: Describe the Architecture componets]

### Guide to deploy and test the sample

This repository contains two paths for deployment: **Script deployment**"** and **Step-by-step deployment**"

#### **Script deployment**

This path is for those not interested in the details of the steps executed to deploy the solution. You can run the script as instructed below and jump into the test.

- Note: if your are using windows, you can run .sh scripts using [Git Bash](https://git-scm.com/downloads) 

```bash
./deploy.sh
```

After completing the deployment, you can copy the printed URL like http://WebAp-demos-XXXXXXXXXXXX-9999999999.us-west-2.elb.amazonaws.com and jumpt to test

#### **Step-by-Step deployment**

This path is for those who want to execute step-by-step to learn and see each step's results before continuing to the topic [**Test the Solution**](#test-the-solution). 

##### Deploy WebAPI microservice

```bash
cd WebAPI/src/infra/ 
cdk bootstrap 
cdk synth 
cdk deploy
export DEMO_VPC_ID=$(aws cloudformation describe-stacks  --stack-name WebAppInfraStack --output text --query 'Stacks[0].Outputs[?OutputKey==`DemoVpcId`].OutputValue  | [0]') 
cd -
```
1.	Navigate to the CDK project folder for the WebAPI; 
2.	Bootstrap your environment, account, and region to run the CDK project; 
3.	Synthesize the project to validate the implementation and produce the CloudFormation template to be deployed; 
4.	Deploy the CloudFormation Stack after your confirmation;
5.  Query the VPC ID created from the first Stack and export it to a local environment variable DEMO_VPC_ID 
6.   Navigate back to the root folder 

##### Deploy the first Worker Services that persist on DynamoDb Table

```bash
cd ServicesWorkerDb/src/infra/ 
cdk synth
cdk deploy
cd -
```
1.  Navigate to the CDK project folder for the Worker Service that persists into DynamoDB 
2.  Then synthesize 
3.  and deploy the Stack.
4.  Navigate back to the root folder 

##### Deploy the second Worker Services that persist on S3 Bucket
```bash
cd ServicesWorkerIntegration/src/infra/
cdk synth
cdk deploy
cd -
```
1.  Navigate to the CDK project folder for the Worker Service that persists into S3 Bucket 
2.  Then synthesize 
3.  and deploy the Stack.
4.  Navigate back to the root folder 

##### Print the URL for testing
```bash
aws cloudformation describe-stacks  --stack-name WebAppInfraStack --output text --query 'Stacks[0].Outputs[?contains(OutputKey,`demoserviceServiceURL`)].OutputValue  | [0]'
```
1. This command will show the URL you'll use for testing. 


### Test the Solution
[TODO: add guide]
### .NET Observability with X-Ray and CloudWatch

[TODO: Add Observability details]

### Clean up Resources
After exploring this solution, please remember to clean up, here's the script to help cleaning up.

- Note: if your are using windows, you can run .sh scripts using [Git Bash](https://git-scm.com/downloads) 
```bash
./clean.sh
```
### Related content

To learn more about this implementation see the following contents

## Security

See [CONTRIBUTING](CONTRIBUTING.md#security-issue-notifications) for more information.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.

