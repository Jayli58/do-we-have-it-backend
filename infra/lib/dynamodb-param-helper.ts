import { dynamodbConfig } from "../config/backend/config.dynamodb";
import { ssmParam } from "./ssm-param-helper";


export function ddbParam(
    tableKey: keyof typeof dynamodbConfig.tables,
    leaf: "arn" | "name" | "streamArn"
) {
    return ssmParam(dynamodbConfig.ssm.basePath, String(tableKey), leaf);
}
