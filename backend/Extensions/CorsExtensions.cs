using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoWeHaveItApp.Extensions
{
    public static class CorsExtensions
    {
        private const string PolicyName = "AllowFrontend";

        public static IServiceCollection AddMyCors(this IServiceCollection services, IConfiguration config)
        {
            var frontendUrl = config["Frontend:Url"] ?? "http://localhost:3000";

            var allowedOrigins = new[]
                {
                    frontendUrl,
                    "http://localhost:3000"
                }
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct()
                .ToArray();

            services.AddCors(options =>
            {
                options.AddPolicy(PolicyName, builder =>
                {
                    builder
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .WithExposedHeaders("X-Next-Page-Key")
                        .AllowCredentials();
                });
            });

            return services;
        }

        public static string GetPolicyName() => PolicyName;
    }
}
