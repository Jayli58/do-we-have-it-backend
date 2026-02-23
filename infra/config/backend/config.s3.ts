import { apiConfig } from "./config.api";

export const s3Config = {
    ssm: {
        basePath: `${apiConfig.Ssm__BasePath}/s3`,
        keys: {
            arn: "arn",
            name: "name",
        },
    },
    buckets: {
        images: {},
    },
};
