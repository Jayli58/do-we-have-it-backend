using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace DoWeHaveItApp.Extensions;

// We still use id token here for authentication for simple fetching of user info
public static class CognitoAuthExtensions
{
    public static IServiceCollection AddCognitoAuth(this IServiceCollection services, IConfiguration config)
    {
        var region = config["Cognito:Region"];
        var userPoolId = config["Cognito:UserPoolId"];
        var clientId = config["Cognito:ClientId"];

        if (string.IsNullOrWhiteSpace(region) ||
            string.IsNullOrWhiteSpace(userPoolId) ||
            string.IsNullOrWhiteSpace(clientId))
        {
            return services;
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
                options.Audience = clientId;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });

        services.AddAuthorization();

        return services;
    }
}
