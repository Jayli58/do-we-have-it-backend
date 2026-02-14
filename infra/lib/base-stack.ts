import * as cdk from 'aws-cdk-lib/core';
import { Construct } from 'constructs';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as ssm from "aws-cdk-lib/aws-ssm";
import { dynamodbConfig } from '../config/backend/config.dynamodb';
import { ddbParam } from './dynamodb-param-helper';


export class BaseStack extends cdk.Stack {
  public readonly inventoryTable: dynamodb.Table;

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
  }
}
