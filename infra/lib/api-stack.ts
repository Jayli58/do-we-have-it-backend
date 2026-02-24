import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as apigwv2 from 'aws-cdk-lib/aws-apigatewayv2';
import * as integrations from 'aws-cdk-lib/aws-apigatewayv2-integrations';
import * as path from 'path';
import { HttpJwtAuthorizer } from 'aws-cdk-lib/aws-apigatewayv2-authorizers';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as ssm from "aws-cdk-lib/aws-ssm";
import { ddbParam } from "./dynamodb-param-helper";
import { s3Param } from "./s3-param-helper";
import * as acm from "aws-cdk-lib/aws-certificatemanager";
import { apiConfig } from '../config/backend/config.api';


export class ApiStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props?: cdk.StackProps) {
        super(scope, id, {
            ...props,
            description: "DWHI App backend api"
        });

        // fetch cognito params deployed in base stack
        const userPoolId = ssm.StringParameter.valueForStringParameter(this, `${apiConfig.Ssm__BasePath}/cognito/userPoolId`);
        const appClientId = ssm.StringParameter.valueForStringParameter(this, `${apiConfig.Ssm__BasePath}/cognito/clientId`);
        const cognitoRegion = ssm.StringParameter.valueForStringParameter(this, `${apiConfig.Ssm__BasePath}/cognito/region`);

        // lambda
        const myAppRoot = process.env.MYAPP_ROOT ?? '../backend';
        const apiFn = new lambda.Function(this, 'DWHIApiFn', {
            runtime: lambda.Runtime.DOTNET_8,
            handler: apiConfig.Handler__Name,
            // needed for quick response in lambda
            memorySize: 1536,
            timeout: cdk.Duration.seconds(10),

            // integrate with CI later
            code: lambda.Code.fromAsset(
                path.resolve(process.cwd(), myAppRoot, 'bin/lambda-publish')
            ),

            environment: {
                ...apiConfig,
                DynamoDB__UseLocal: String(apiConfig.DynamoDB__UseLocal),
                S3__UseLocal: 'false',
                S3__ImageJpegQuality: '50',
                // override cognito params
                Cognito__Region: cognitoRegion,
                Cognito__UserPoolId: userPoolId,
                Cognito__ClientId: appClientId,
                ASPNETCORE_ENVIRONMENT: 'Production',
            }
        });

        // allow lambda to access dynamodb
        const inventoryArn = ssm.StringParameter.valueForStringParameter(this, ddbParam("inventory", "arn"));
        const inventoryTable = dynamodb.Table.fromTableAttributes(this, "InventoryTable", {
            tableArn: inventoryArn
        });

        inventoryTable.grantReadWriteData(apiFn);

        // allow lambda to access s3 (images bucket)
        const imageBucketArn = ssm.StringParameter.valueForStringParameter(this, s3Param("images", "arn"));
        const imageBucketName = ssm.StringParameter.valueForStringParameter(this, s3Param("images", "name"));
        const imageBucket = s3.Bucket.fromBucketAttributes(this, "DWHIImageBucket", {
            bucketArn: imageBucketArn,
            bucketName: imageBucketName,
        });

        imageBucket.grantReadWrite(apiFn);
        apiFn.addEnvironment('S3__ImageBucket', imageBucketName);
        apiFn.addEnvironment('S3__Region', this.region);

        apiFn.addToRolePolicy(new iam.PolicyStatement({
            actions: ['dynamodb:DescribeTable'],
            resources: [inventoryTable.tableArn],
        }));

        // allow lambda to query GSI
        apiFn.addToRolePolicy(new iam.PolicyStatement({
            actions: ['dynamodb:Query', 'dynamodb:Scan'],
            resources: [`${inventoryTable.tableArn}/index/GSI1`],
        }));

        // api gtw
        const issuer = `https://cognito-idp.${cognitoRegion}.amazonaws.com/${userPoolId}`;

        const authorizer = new HttpJwtAuthorizer('CognitoJwtAuthorizer', issuer, {
            jwtAudience: [appClientId],
        });

        // cert
        const cert = new acm.Certificate(this, "DWHIApiCert", {
            domainName: apiConfig.Domain,
            validation: acm.CertificateValidation.fromDns(),
        });

        const apiDomainName = new apigwv2.DomainName(this, 'DWHIApiDomainName', {
            domainName: apiConfig.Domain,
            certificate: cert,
        });

        const lambdaIntegration = new integrations.HttpLambdaIntegration(
            'DWHILambdaIntegration',
            apiFn
        );

        const httpApi = new apigwv2.HttpApi(this, 'DWHIApi', {
            // underneath setting is not necessary as api gtw does not enforce CORS by default
            // corsPreflight: {
            //     allowOrigins: [apiConfig.Frontend__Url],
            //     allowMethods: [apigwv2.CorsHttpMethod.ANY],
            //     allowHeaders: ['authorization', 'content-type'],
            //     allowCredentials: true,
            // },
            defaultIntegration: lambdaIntegration,
            defaultAuthorizer: authorizer,
            // https://${domainName}/api goes to prodApi $default stage
            defaultDomainMapping: {
                domainName: apiDomainName,
            },
        });

        // allow all preflight requests without JWT
        httpApi.addRoutes({
            path: '/{proxy+}',
            methods: [apigwv2.HttpMethod.OPTIONS],
            integration: lambdaIntegration,
            // turn off authorizer
            authorizer: new apigwv2.HttpNoneAuthorizer(),
        });

        // health check
        // httpApi.addRoutes({
        //     path: '/health',
        //     methods: [apigwv2.HttpMethod.GET],
        //     integration: lambdaIntegration,
        //     authorizer: new apigwv2.HttpNoneAuthorizer(),
        // });

        // enable logs
        const accessLogGroup = new logs.LogGroup(this, 'DWHIApiAccessLogs', {
            retention: logs.RetentionDays.ONE_WEEK,
        });

        const cfnStage = httpApi.defaultStage?.node.defaultChild as apigwv2.CfnStage;

        cfnStage.accessLogSettings = {
            destinationArn: accessLogGroup.logGroupArn,
            format: JSON.stringify({
                requestId: '$context.requestId',
                ip: '$context.identity.sourceIp',
                requestTime: '$context.requestTime',
                httpMethod: '$context.httpMethod',
                routeKey: '$context.routeKey',
                status: '$context.status',
                integrationError: '$context.integrationErrorMessage',
                responseLength: '$context.responseLength',
                path: '$context.path',
            }),
        };

        // throttling
        cfnStage.defaultRouteSettings = {
            throttlingRateLimit: 10,
            throttlingBurstLimit: 10,
        }

        new ssm.StringParameter(this, "DWHIApiUrlParam", {
            parameterName: "/dwhiapp/api/url",
            stringValue: `https://${apiConfig.Domain}`,
        });
    }
}
