using Amazon.DynamoDBv2.Model;
using DoWeHaveItApp.Infrastructure.Dynamo;
using System.Collections.Generic;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class DynamoAttributeBuilderTests
{
    [Fact]
    public void BuildStringAttribute_ReturnsNullForEmpty()
    {
        var empty = DynamoAttributeBuilder.BuildStringAttribute(string.Empty);
        var whitespace = DynamoAttributeBuilder.BuildStringAttribute("  ");
        // check if it is dynamodb null
        Assert.True(empty.NULL);
        Assert.True(whitespace.NULL);
    }

    [Fact]
    public void BuildStringAttribute_ReturnsStringForValue()
    {
        var value = DynamoAttributeBuilder.BuildStringAttribute("Coffee");

        Assert.Equal("Coffee", value.S);
        Assert.False(value.NULL);
    }

    [Fact]
    public void BuildStringAttribute_TruncatesLongValues()
    {
        var longValue = new string('a', DynamoAttributeBuilder.DefaultStringLimit + 10);

        var value = DynamoAttributeBuilder.BuildStringAttribute(longValue);

        Assert.Equal(DynamoAttributeBuilder.DefaultStringLimit, value.S!.Length);
    }

    [Fact]
    public void AddOptionalStringAttribute_SkipsEmpty()
    {
        var attributes = new Dictionary<string, AttributeValue>();

        DynamoAttributeBuilder.AddOptionalStringAttribute(attributes, "name", string.Empty);
        DynamoAttributeBuilder.AddOptionalStringAttribute(attributes, "notes", "  ");

        Assert.Empty(attributes);
    }

    [Fact]
    public void AddOptionalStringAttribute_AddsValue()
    {
        var attributes = new Dictionary<string, AttributeValue>();

        DynamoAttributeBuilder.AddOptionalStringAttribute(attributes, "name", "Kettle");

        Assert.Equal("Kettle", attributes["name"].S);
    }

    // This test verifies that the SanitizeAttributes method correctly converts any empty strings, empty sets, or empty maps to DynamoDB's NULL representation, while preserving valid values.
    [Fact]
    public void SanitizeAttributes_ReplacesEmptyValues()
    {
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["empty"] = new AttributeValue { S = string.Empty },
            ["valid"] = new AttributeValue { S = "ok" },
            ["set"] = new AttributeValue { SS = new List<string>() },
            ["map"] = new AttributeValue
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["inner"] = new AttributeValue { S = string.Empty },
                },
            },
            ["list"] = new AttributeValue
            {
                L = new List<AttributeValue>
                {
                    new AttributeValue { S = string.Empty },
                    new AttributeValue { S = "value" },
                },
            },
        };

        var sanitized = DynamoAttributeBuilder.SanitizeAttributes(attributes);

        Assert.True(sanitized["empty"].NULL);
        Assert.Equal("ok", sanitized["valid"].S);
        Assert.True(sanitized["set"].NULL);

        if (sanitized["map"].NULL)
        {
            Assert.True(sanitized["map"].NULL);
        }
        else
        {
            var map = sanitized["map"].M;
            Assert.NotNull(map);
            Assert.True(map!.TryGetValue("inner", out var inner));
            Assert.True(inner.NULL);
        }

        if (sanitized["list"].NULL)
        {
            Assert.True(sanitized["list"].NULL);
        }
        else
        {
            Assert.Equal(2, sanitized["list"].L!.Count);
            Assert.True(sanitized["list"].L![0].NULL);
            Assert.Equal("value", sanitized["list"].L![1].S);
        }
    }
}
