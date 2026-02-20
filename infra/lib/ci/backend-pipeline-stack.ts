import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as codepipeline from 'aws-cdk-lib/aws-codepipeline';
import * as codepipelineActions from 'aws-cdk-lib/aws-codepipeline-actions';
import * as codebuild from 'aws-cdk-lib/aws-codebuild';
import * as events from 'aws-cdk-lib/aws-events';
import * as targets from 'aws-cdk-lib/aws-events-targets';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as iam from 'aws-cdk-lib/aws-iam';
import { ciConfig } from '../../config/backend/config.ci';

export class BackendPipelineStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props?: cdk.StackProps) {
        super(scope, id, {
            ...props,
            description: "DWHI App backend CI"
        });

        const sourceBucket = new s3.Bucket(this, 'DWHIBackendSourceBucket', {
            versioned: true,
            blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
            encryption: s3.BucketEncryption.S3_MANAGED,
            removalPolicy: cdk.RemovalPolicy.RETAIN,
            // enable event bridge for s3 source action
            eventBridgeEnabled: true,
        });

        const backendSourceObjectKey = ciConfig.backendSourceObjectKey;
        const infraSourceObjectKey = ciConfig.infraSourceObjectKey;


        const backendSourceOutput = new codepipeline.Artifact('DWHIBackendSourceOutput');
        const infraSourceOutput = new codepipeline.Artifact('DWHIBackendInfraSourceOutput');

        const deployProject = new codebuild.PipelineProject(this, 'DWHIBackendDeployProject', {
            environment: {
                buildImage: codebuild.LinuxBuildImage.STANDARD_7_0,
                privileged: false,
            },
            environmentVariables: {
                AWS_REGION: { value: cdk.Stack.of(this).region },
            },
            buildSpec: codebuild.BuildSpec.fromObject({
                version: '0.2',
                phases: {
                    install: {
                        'runtime-versions': {
                            nodejs: '22',
                        },
                    },
                    pre_build: {
                        commands: [
                            'infra_dir="$CODEBUILD_SRC_DIR"',
                            'app_dir="$CODEBUILD_SRC_DIR_DWHIBackendSourceOutput"',
                            'export MYAPP_ROOT="$app_dir"',
                            'cd "$infra_dir"',
                            'npm ci',
                        ],
                    },
                    build: {
                        commands: [
                            'cd "$infra_dir"',
                            'npx cdk deploy InfraStack/DWHIApiStack --require-approval never',
                        ],
                    },
                },
            }),
        });

        deployProject.role?.addManagedPolicy(
            iam.ManagedPolicy.fromAwsManagedPolicyName('AdministratorAccess')
        );

        const pipeline = new codepipeline.Pipeline(this, 'DWHIBackendPipeline', {
            pipelineName: 'dwhiapp-backend-pipeline',
            // when pipeline definition itself is updated, restart the pipeline
            restartExecutionOnUpdate: true,
        });

        // github oidc provider already exists as part of the frontend pipeline
        const githubOidcProviderArn = `arn:aws:iam::${cdk.Stack.of(this).account}:oidc-provider/token.actions.githubusercontent.com`;
        const githubOidcProvider = iam.OpenIdConnectProvider.fromOpenIdConnectProviderArn(
            this,
            'GitHubOidcProvider',
            githubOidcProviderArn
        );

        // role for github actions to upload artifacts to s3
        const githubDeployRole = new iam.Role(this, 'GitHubBackendDeployRole', {
            description: 'GitHub Actions role for dwhi backend artifact uploads',
            assumedBy: new iam.FederatedPrincipal(
                githubOidcProvider.openIdConnectProviderArn,
                {
                    'StringEquals': {
                        'token.actions.githubusercontent.com:aud': 'sts.amazonaws.com',
                    },
                    'StringLike': {
                        'token.actions.githubusercontent.com:sub': [
                            `repo:${ciConfig.backendGithubOidcRepo}:ref:refs/heads/main`,
                        ],
                    },
                },
                'sts:AssumeRoleWithWebIdentity'
            ),
        });

        githubDeployRole.addToPolicy(new iam.PolicyStatement({
            actions: [
                's3:GetBucketLocation',
                's3:ListBucket',
            ],
            resources: [sourceBucket.bucketArn],
        }));

        githubDeployRole.addToPolicy(new iam.PolicyStatement({
            actions: [
                's3:PutObject',
                's3:DeleteObject',
            ],
            resources: [`${sourceBucket.bucketArn}/*`],
        }));

        pipeline.addStage({
            stageName: 'Source',
            actions: [
                new codepipelineActions.S3SourceAction({
                    actionName: 'DWHIBackendSource',
                    bucket: sourceBucket,
                    bucketKey: backendSourceObjectKey,
                    output: backendSourceOutput,
                    trigger: codepipelineActions.S3Trigger.NONE,
                }),
                new codepipelineActions.S3SourceAction({
                    actionName: 'DWHIBackendInfraSource',
                    bucket: sourceBucket,
                    bucketKey: infraSourceObjectKey,
                    output: infraSourceOutput,
                    trigger: codepipelineActions.S3Trigger.NONE,
                }),
            ],
        });

        pipeline.addStage({
            stageName: 'Approve',
            actions: [
                new codepipelineActions.ManualApprovalAction({
                    actionName: 'ApproveDeploy',
                }),
            ],
        });

        pipeline.addStage({
            stageName: 'Deploy',
            actions: [
                new codepipelineActions.CodeBuildAction({
                    actionName: 'DeployDWHIBackend',
                    project: deployProject,
                    input: infraSourceOutput,
                    extraInputs: [backendSourceOutput],
                }),
            ],
        });

        // trigger pipeline when backend-source.zip is uploaded
        new events.Rule(this, 'TriggerPipelineOnDWHIBackendZip', {
            description: 'Start CodePipeline when dwhi backend-source.zip is uploaded',
            eventPattern: {
                source: ['aws.s3'],
                detailType: ['Object Created'],
                detail: {
                    bucket: { name: [sourceBucket.bucketName] },
                    object: { key: [backendSourceObjectKey] },
                },
            },
            targets: [new targets.CodePipeline(pipeline)],
        });

        new cdk.CfnOutput(this, 'DWHIBackendSourceBucketName', {
            value: sourceBucket.bucketName,
        });

        new cdk.CfnOutput(this, 'DWHIBackendSourceObjectKey', {
            value: backendSourceObjectKey,
        });

        new cdk.CfnOutput(this, 'DWHIBackendInfraSourceObjectKey', {
            value: infraSourceObjectKey,
        });

        new cdk.CfnOutput(this, 'DWHIBackendGithubDeployRoleArn', {
            value: githubDeployRole.roleArn,
        });
    }
}