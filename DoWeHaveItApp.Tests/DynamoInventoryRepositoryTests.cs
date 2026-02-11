using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Models;
using DoWeHaveItApp.Repositories;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class DynamoInventoryRepositoryTests
{
    private const string UserId = "user-1";

    // This test verifies that the BuildItemRecord method can handle item attributes with null or empty values without throwing exceptions.
    [Fact]
    public void BuildItemRecord_AllowsEmptyAttributeValues()
    {
        var repository = CreateRepository();

        var item = new Item
        {
            Id = "item-1",
            Name = "Mixer",
            Comments = string.Empty,
            ParentId = null,
            Attributes = new List<ItemAttribute>
            {
                new()
                {
                    FieldId = "field-1",
                    FieldName = "Serial",
                    Value = null!,
                },
            },
            CreatedAt = "2026-02-10T00:00:00Z",
            UpdatedAt = "2026-02-10T00:00:00Z",
        };

        // Invoke the BuildItemRecord method using reflection to verify that it can handle empty attribute values without throwing exceptions.
        var record = InvokeBuildItemRecord(repository, UserId, item, "ROOT");
        Assert.True(record.ContainsKey("attributes"));
        if (record["attributes"].NULL)
        {
            Assert.True(record["attributes"].NULL);
        }
        else
        {
            Assert.Single(record["attributes"].L);

            var attributeMap = record["attributes"].L[0].M;
            Assert.NotNull(attributeMap);
            Assert.Equal("field-1", attributeMap!["fieldId"].S);
            Assert.Equal("Serial", attributeMap!["fieldName"].S);
            Assert.False(attributeMap.ContainsKey("value"));
        }
        Assert.False(record.ContainsKey("comments"));
    }

    [Fact]
    public void BuildItemRecord_DoesNotCreateEmptyAttributeValues()
    {
        var repository = CreateRepository();

        var item = new Item
        {
            Id = "item-1",
            Name = "Mixer",
            Comments = string.Empty,
            ParentId = null,
            Attributes = new List<ItemAttribute>
            {
                new()
                {
                    FieldId = string.Empty,
                    FieldName = "Serial",
                    Value = null!,
                },
            },
            CreatedAt = "2026-02-10T00:00:00Z",
            UpdatedAt = "2026-02-10T00:00:00Z",
        };

        var record = InvokeBuildItemRecord(repository, UserId, item, "ROOT");
        AssertNoEmptyAttributeValues(record);

        var searchRecords = InvokeBuildSearchRecords(repository, UserId, item, "ROOT").ToList();
        Assert.NotEmpty(searchRecords);

        foreach (var request in searchRecords)
        {
            Assert.NotNull(request.PutRequest);
            Assert.NotNull(request.PutRequest!.Item);
            AssertNoEmptyAttributeValues(request.PutRequest.Item);
        }
    }

    private static Dictionary<string, AttributeValue> InvokeBuildItemRecord(
        DynamoInventoryRepository repository,
        string userId,
        Item item,
        string parentKey)
    {
        var method = typeof(DynamoInventoryRepository).GetMethod(
            "BuildItemRecord",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return (Dictionary<string, AttributeValue>)method.Invoke(repository, new object[]
        {
            userId,
            item,
            parentKey,
        })!;
    }

    private static IEnumerable<WriteRequest> InvokeBuildSearchRecords(
        DynamoInventoryRepository repository,
        string userId,
        Item item,
        string parentKey)
    {
        var method = typeof(DynamoInventoryRepository).GetMethod(
            "BuildSearchRecords",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return (IEnumerable<WriteRequest>)method.Invoke(repository, new object[]
        {
            userId,
            item,
            parentKey,
        })!;
    }

    private static DynamoInventoryRepository CreateRepository()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("DynamoDb__ServiceUrl")
            ?? "http://localhost:4566";
        var tableName = Environment.GetEnvironmentVariable("DynamoDb__TableName")
            ?? "Inventory";
        var client = new AmazonDynamoDBClient(
            new AnonymousAWSCredentials(),
            new AmazonDynamoDBConfig { ServiceURL = serviceUrl });
        var options = Options.Create(new DynamoDbOptions { TableName = tableName });

        return new DynamoInventoryRepository(client, options, new Tokenizer());
    }

    private static void AssertNoEmptyAttributeValues(Dictionary<string, AttributeValue> attributes)
    {
        foreach (var (key, value) in attributes)
        {
            Assert.False(IsAttributeValueEmpty(value), $"AttributeValue is empty for {key}.");

            if (value.M != null)
            {
                AssertNoEmptyAttributeValues(value.M);
            }

            if (value.L != null)
            {
                foreach (var entry in value.L)
                {
                    Assert.False(IsAttributeValueEmpty(entry), $"AttributeValue is empty for list entry in {key}.");

                    if (entry.M != null)
                    {
                        AssertNoEmptyAttributeValues(entry.M);
                    }
                }
            }
        }
    }

    private static bool IsAttributeValueEmpty(AttributeValue value)
    {
        if (value == null)
        {
            return true;
        }

        if (value.S != null)
        {
            return string.IsNullOrWhiteSpace(value.S);
        }

        if (value.N != null)
        {
            return string.IsNullOrWhiteSpace(value.N);
        }

        if (value.B != null)
        {
            return value.B == null;
        }

        if (value.NULL || value.BOOL)
        {
            return false;
        }

        if (value.SS != null)
        {
            return value.SS.Count == 0;
        }

        if (value.NS != null)
        {
            return value.NS.Count == 0;
        }

        if (value.BS != null)
        {
            return value.BS.Count == 0;
        }

        if (value.M != null)
        {
            return false;
        }

        if (value.L != null)
        {
            return false;
        }

        return true;
    }
}
