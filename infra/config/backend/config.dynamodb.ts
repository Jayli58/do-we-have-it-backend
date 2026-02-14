import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import { apiConfig } from "./config.api";

export const dynamodbConfig = {
    ssm: {
        basePath: `${apiConfig.Ssm__BasePath}/dynamodb`,
        keys: {
            arn: "arn",
            name: "name",
        },
    },
    tables: {
        inventory: {
            tableName: 'Inventory',
            partitionKey: {
                name: 'PK',
                type: dynamodb.AttributeType.STRING
            },
            sortKey: {
                name: 'SK',
                type: dynamodb.AttributeType.STRING
            }
        }
    },
};
