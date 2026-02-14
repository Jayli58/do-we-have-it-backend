import * as cdk from 'aws-cdk-lib/core';
import { Construct } from 'constructs';
import { BaseStack } from './base-stack';
// import * as sqs from 'aws-cdk-lib/aws-sqs';

export class InfraStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const baseStack = new BaseStack(this, 'BaseStack');

    const apiStack = new ApiStack(this, 'DWHIApiStack');
  }
}
