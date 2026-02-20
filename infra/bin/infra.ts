#!/usr/bin/env node
import * as cdk from 'aws-cdk-lib/core';
import { InfraStack } from '../lib';
import { BackendPipelineStack } from '../lib/ci/backend-pipeline-stack';

const app = new cdk.App();

const account = process.env.CDK_DEFAULT_ACCOUNT!;
const region = process.env.CDK_DEFAULT_REGION!;

new InfraStack(app, 'InfraStack', {
  env: { account: account, region: region },
});

new BackendPipelineStack(app, 'DWHIBackendPipelineStack', {
  env: { account: account, region: region },
});