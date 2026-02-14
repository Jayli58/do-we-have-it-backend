using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;
using DoWeHaveItApp.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DoWeHaveItApp.Tests;

// Integration tests
// dynamodb got injected by the framework
public sealed class ItemsApiIntegrationTests : IClassFixture<DynamoDbFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DynamoDbFixture _fixture;

    public ItemsApiIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateItem_PersistsAttributesToDynamo()
    {
        var userId = $"test-user-{Guid.NewGuid():N}";
        using var factory = new DynamoItemsApiFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-User-Id", userId);

        var payload = new
        {
            name = "item1",
            comments = string.Empty,
            parentId = (string?)null,
            attributes = new[]
            {
                new { fieldId = "field-mlhlmsvk-bvl4", fieldName = "f1", value = "f1" },
                new { fieldId = "field-mlhlmynm-46gi", fieldName = "f2", value = "f2" },
                new { fieldId = "field-mlhln0jf-ppco", fieldName = "f3", value = "f3" },
            },
        };

        var response = await client.PostAsJsonAsync("/items", payload, JsonOptions);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var responseJson = JsonDocument.Parse(responseBody);
        var root = responseJson.RootElement;
        var attributes = root.GetProperty("attributes");
        Assert.True(attributes.GetArrayLength() == 3, responseBody);
        var firstAttribute = attributes[0];
        Assert.Equal("field-mlhlmsvk-bvl4", firstAttribute.GetProperty("fieldId").GetString());
        Assert.Equal("f1", firstAttribute.GetProperty("fieldName").GetString());
        Assert.Equal("f1", firstAttribute.GetProperty("value").GetString());
        var itemId = root.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(itemId));

        var itemRecord = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _fixture.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"USER#{userId}"),
                ["SK"] = new AttributeValue($"ITEM#ROOT#{itemId}"),
            },
        });

        Assert.True(itemRecord.Item.Count > 0);
        Assert.True(itemRecord.Item.TryGetValue("attributes", out var attributesValue));
        Assert.NotNull(attributesValue.L);
        Assert.True(attributesValue.L.Count == 3, Document.FromAttributeMap(itemRecord.Item).ToJson());
        Assert.Equal("field-mlhlmsvk-bvl4", attributesValue.L[0].M!["fieldId"].S);
        Assert.Equal("f1", attributesValue.L[0].M!["fieldName"].S);
        Assert.Equal("f1", attributesValue.L[0].M!["value"].S);
    }
}

public sealed class DynamoItemsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DynamoDb:TableName"] = DynamoDbFixture.InventoryTable,
                ["DynamoDb:Region"] = DynamoDbFixture.Region,
                ["DynamoDb:UseLocal"] = "true",
                ["DynamoDb:ServiceUrl"] = DynamoDbFixture.ServiceUrl,
            });
        });
    }
}

public sealed class DynamoDbFixture : IAsyncLifetime
{
    public const string InventoryTable = "Inventory";
    public const string Region = "ap-southeast-2";
    public const string ServiceUrl = "http://localhost:4566";

    public IAmazonDynamoDB Client { get; private set; } = null!;
    public string TableName => InventoryTable;

    public async Task InitializeAsync()
    {
        var options = new DynamoDbOptions
        {
            TableName = InventoryTable,
            Region = Region,
            UseLocal = true,
            ServiceUrl = ServiceUrl,
        };

        Client = DynamoDbClientFactory.Create(options);

        if (!await TableExistsAsync())
        {
            await Client.CreateTableAsync(new CreateTableRequest
            {
                TableName = InventoryTable,
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new("PK", ScalarAttributeType.S),
                    new("SK", ScalarAttributeType.S),
                    new("GSI1PK", ScalarAttributeType.S),
                    new("GSI1SK", ScalarAttributeType.S),
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new("PK", KeyType.HASH),
                    new("SK", KeyType.RANGE),
                },
                GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                {
                    new()
                    {
                        IndexName = "GSI1",
                        KeySchema = new List<KeySchemaElement>
                        {
                            new("GSI1PK", KeyType.HASH),
                            new("GSI1SK", KeyType.RANGE),
                        },
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    },
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });
        }

        await WaitForTableActiveAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<bool> TableExistsAsync()
    {
        try
        {
            await Client.DescribeTableAsync(InventoryTable);
            return true;
        }
        catch (ResourceNotFoundException)
        {
            return false;
        }
    }

    private async Task WaitForTableActiveAsync()
    {
        for (var attempt = 0; attempt < 10; attempt += 1)
        {
            var response = await Client.DescribeTableAsync(InventoryTable);
            if (string.Equals(response.Table.TableStatus, TableStatus.ACTIVE, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
    }
}
