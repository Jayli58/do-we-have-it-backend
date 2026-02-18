using Amazon.DynamoDBv2;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using DoWeHaveItApp.Exceptions;
using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Repositories;
using DoWeHaveItApp.Services;
using DoWeHaveItApp.Extensions;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Configure CORS
builder.Services.AddMyCors(builder.Configuration);
// Configure Cognito authentication
builder.Services.AddCognitoAuth(builder.Configuration);

// Configure DynamoDB
builder.Services.Configure<DynamoDbOptions>(builder.Configuration.GetSection("DynamoDb"));
// Configure DynamoDB client using factory for dependency injection
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var options = sp.GetRequiredService<IOptions<DynamoDbOptions>>().Value;
    return DynamoDbClientFactory.Create(options);
});
// Configure Tokenizer
builder.Services.AddSingleton<Tokenizer>();
// Configure repositories
builder.Services.AddScoped<IInventoryRepository, DynamoInventoryRepository>();
// Configure services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ISearchService, SearchService>();

// Register global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Register ProblemDetails middleware
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use custom exception handler middleware
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(CorsExtensions.GetPolicyName());

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// for integration tests
public partial class Program { }
