import { s3Config } from "../config/backend/config.s3";
import { ssmParam } from "./ssm-param-helper";

export function s3Param(
    bucketKey: keyof typeof s3Config.buckets,
    leaf: "arn" | "name"
) {
    return ssmParam(s3Config.ssm.basePath, String(bucketKey), leaf);
}
