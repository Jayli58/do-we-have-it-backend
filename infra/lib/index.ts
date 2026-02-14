import * as cdk from 'aws-cdk-lib/core';
import { Construct } from 'constructs';
import { BaseStack } from './base-stack';
import { ApiStack } from './api-stack';

export class InfraStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const baseStack = new BaseStack(this, 'DWHIBackendBaseStack');

    const apiStack = new ApiStack(this, 'DWHIApiStack');
  }
}
