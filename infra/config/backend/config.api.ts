import { sharedConfig } from "../shared";

export const apiConfig = {
    // env vars needed by asp.net
    DynamoDB__Region: 'ap-southeast-2',
    DynamoDB__UseLocal: false,
    Frontend__Url: `https://${sharedConfig.domain}`,
    Cognito__Region: 'ap-southeast-2',
    Cognito__UserPoolId: 'filled-by-cdk-deployment',
    Cognito__ClientId: 'filled-by-cdk-deployment',
    // needs to be .net project name
    Handler__Name: 'DoWeHaveItApp',
    Ssm__BasePath: '/dwhi/base',
    Domain: `api.${sharedConfig.domain}`
}
