using Amazon.DynamoDBv2;
using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Repositories;
using DoWeHaveItApp.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
