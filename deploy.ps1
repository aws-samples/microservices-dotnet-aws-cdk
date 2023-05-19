Write-Output "This deployment will use the following User Id and Account: `r`n"
aws sts get-caller-identity
#############################################################
#               Deploy WebAPI microservice:                 #
#############################################################
# 1.	Navigate to the CDK project folder for the WebAPI; 
# 2.	Bootstrap your environment, account, and region to run the CDK project; 
# 3.	Synthesize the project to validate the implementation and produce the CloudFormation template to be deployed; 
# 4.	Deploy the CloudFormation Stack after your confirmation;
# 5.    Query the VPC ID created from the first Stack and export it to a local environment variable DEMO_VPC_ID 
# 6.    Query the DemoDeployRegion created from the first Stack and export it to a local environment variable CDK_DEPLOY_REGION 
# 7.    Navigate back to the root folder 
Set-Location WebAPI/src/infra/
cdk bootstrap
cdk synth
cdk deploy --require-approval never
$Env:DEMO_VPC_ID = $(aws cloudformation describe-stacks  --stack-name WebAppInfraStack --output text --query 'Stacks[0].Outputs[?OutputKey==`DemoVpcId`].OutputValue  | [0]')
$Env:CDK_DEPLOY_REGION = $(aws cloudformation describe-stacks  --stack-name WebAppInfraStack --output text --query 'Stacks[0].Outputs[?OutputKey==`DemoDeployRegion`].OutputValue  | [0]')
Set-Location ../../../


#############################################################
#       Deploy the first Worker Services microservices      #
#############################################################
# 1.	Navigate to the CDK project folder for the Worker Service that persists into DynamoDB 
# 2.	Then synthesize 
# 3.    and deploy the Stack.
# 4.    Navigate back to the root folder 
Set-Location ServicesWorkerDb/src/infra/
cdk synth
cdk deploy --require-approval never
Set-Location ../../../


#############################################################
#       Deploy the second Worker Services microservices     #
#############################################################
# 1.	Navigate to the CDK project folder for the Worker Service that persists into S3 Bucket 
# 2.	Then synthesize 
# 3.    and deploy the Stack.
# 4.    Navigate back to the root folder 
Set-Location ServicesWorkerIntegration/src/infra/ 
cdk synth 
cdk deploy --require-approval never
Set-Location ../../../


#############################################################
#                   Echo the WebAPI URL                     #
#############################################################
Write-Output "`r`n"
Write-Output "#############################################################"
Write-Output "#                        the WebAPI URL                     #"
Write-Output "#############################################################"
Write-Output "`r`n"
aws cloudformation describe-stacks  --stack-name WebAppInfraStack --output text --query 'Stacks[0].Outputs[?contains(OutputKey,`DemoServiceServiceURLEndpoint`)].OutputValue  | [0]'
Write-Output "`r`n"
