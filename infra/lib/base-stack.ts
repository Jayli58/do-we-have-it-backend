import * as cdk from 'aws-cdk-lib/core';
import { Construct } from 'constructs';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as ssm from "aws-cdk-lib/aws-ssm";
import { dynamodbConfig } from '../config/backend/config.dynamodb';
import { ddbParam } from './dynamodb-param-helper';
import { s3Param } from './s3-param-helper';


export class BaseStack extends cdk.Stack {
  public readonly inventoryTable: dynamodb.Table;
  public readonly imageBucket: s3.Bucket;

  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, {
      ...props,
      description: "Base infra: DynamoDB for DoWeHaveItApp"
    });

    const cfg4Inven = dynamodbConfig.tables.inventory;

    // Create dynamo db tables
    // 1) Inventory table
    this.inventoryTable = new dynamodb.Table(this, 'DWHIInventoryTable', {
      tableName: cfg4Inven.tableName,
      partitionKey: {
        name: cfg4Inven.partitionKey.name,
        type: cfg4Inven.partitionKey.type
      },
      sortKey: {
        name: cfg4Inven.sortKey.name,
        type: cfg4Inven.sortKey.type
      },
      billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
      removalPolicy: cdk.RemovalPolicy.RETAIN,
    });

    // secondary index added for load more functionality
    this.inventoryTable.addGlobalSecondaryIndex({
      indexName: 'GSI1',
      partitionKey: {
        name: 'GSI1PK',
        type: dynamodb.AttributeType.STRING,
      },
      sortKey: {
        name: 'GSI1SK',
        type: dynamodb.AttributeType.STRING,
      },
      projectionType: dynamodb.ProjectionType.ALL
    });

    // output dynamo tbl info to ssm for later uses in other stacks
    new ssm.StringParameter(this, "DWHIInventoryTableArnParam", {
      parameterName: ddbParam("inventory", "arn"),
      stringValue: this.inventoryTable.tableArn,
    });

    // Create s3 bucket for images
    this.imageBucket = new s3.Bucket(this, 'DWHIImageBucket', {
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      encryption: s3.BucketEncryption.S3_MANAGED,
    });

    new ssm.StringParameter(this, "DWHIImageBucketArnParam", {
      parameterName: s3Param("images", "arn"),
      stringValue: this.imageBucket.bucketArn,
    });

    new ssm.StringParameter(this, "DWHIImageBucketNameParam", {
      parameterName: s3Param("images", "name"),
      stringValue: this.imageBucket.bucketName,
    });
  }
}
