#########################################################
# Clean resources created for the first Worker Services #
#########################################################
#   1. Navigate to the CDK project folder for the Worker Service that persists into DynamoDB 
#   2. Destroy all resources without ask for confirmation before destroying the stacks
cd ServicesWorkerDb/src/infra/ \
&& cdk destroy -f \
&& cd -

##########################################################
# Clean resources created for the second Worker Services #
##########################################################
#   1. Navigate to the CDK project folder for the Worker Service that persists into S3 
#   2. Destroy all resources without ask for confirmation before destroying the stacks
cd ServicesWorkerIntegration/src/infra/ \
&& cdk destroy -f \
&& cd -

############################################################
#          Clean resources created for the WebAPI          #
############################################################
#   1. Navigate to the CDK project folder for the WebAPI project
#   2. Destroy all resources without ask for confirmation before destroying the stacks
cd WebAPI/src/infra/ \
&& cdk destroy -f \
&& cd -