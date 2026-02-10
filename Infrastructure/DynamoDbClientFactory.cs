using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

namespace DoWeHaveItApp.Infrastructure;

public static class DynamoDbClientFactory
{
    public static IAmazonDynamoDB Create(DynamoDbOptions options)
    {
        var config = new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
        };

        if (options.UseLocal && !string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.UseHttp = true;
            config.AuthenticationRegion = options.Region;
            return new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), config);
        }

        return new AmazonDynamoDBClient(config);
    }
}
