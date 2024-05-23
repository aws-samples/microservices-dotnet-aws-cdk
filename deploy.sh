echo "This deployment will use the following User Id and Account: "
aws sts get-caller-identity
##############################################################
#               Deploy All the CDK Stacks                    #
##############################################################
# 1.	Navigate to the CDK project folder;
# 2.	Bootstrap your environment, account, and region to run the CDK project; 
# 3.	Synthesize the project to validate the implementation and produce the CloudFormation template to be deployed; 
# 4.	Deploy the CloudFormation Stack after your confirmation;
cd src/infra \
&& cdk bootstrap \
&& cdk synth --all \
&& cdk deploy --require-approval never --all

#############################################################
#                   Echo the WebAPI URL                     #
#############################################################
echo -e "\n\n"
echo "#############################################################"
echo "#                        the WebAPI URL                     #"
echo "#############################################################"
echo -e "\n"
echo $(aws cloudformation describe-stacks  --stack-name WebAppStack --output text --query 'Stacks[0].Outputs[?contains(OutputKey,`DemoServiceServiceURLEndpoint`)].OutputValue  | [0]')
echo -e "\n\n"
