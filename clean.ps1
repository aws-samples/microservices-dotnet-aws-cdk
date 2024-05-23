##########################################################
# Clean resources created for all the CDK Stacks         #
##########################################################
#   1. Navigate to the CDK project folder
#   2. Destroy all resources without ask for confirmation before destroying the stacks
Set-Location src/infra
    cdk destroy -f --all
    Set-Location ../../