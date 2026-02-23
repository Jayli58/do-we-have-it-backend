using Amazon;
using Amazon.Runtime;
using Amazon.S3;

namespace DoWeHaveItApp.Infrastructure;

public static class S3ClientFactory
{
    public static IAmazonS3 Create(S3Options options)
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
        };

        if (options.UseLocal && !string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.UseHttp = true;
            config.ForcePathStyle = true;
            config.AuthenticationRegion = options.Region;
            return new AmazonS3Client(new BasicAWSCredentials("test", "test"), config);
        }

        return new AmazonS3Client(config);
    }
}
