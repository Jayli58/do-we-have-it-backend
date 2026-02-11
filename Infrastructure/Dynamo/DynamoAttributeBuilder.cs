using Amazon.DynamoDBv2.Model;
using System.Collections.Generic;
using System.Linq;

namespace DoWeHaveItApp.Infrastructure.Dynamo;

internal static class DynamoAttributeBuilder
{
    internal const int DefaultStringLimit = 100;
    private static readonly HashSet<string> SystemKeyAttributes =
        new(StringComparer.OrdinalIgnoreCase) { "PK", "SK", "GSI1PK", "GSI1SK" };

    internal static AttributeValue BuildStringAttribute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // DynamoDB way to explicitly store a Null attribute
            return new AttributeValue { NULL = true };
        }

        return new AttributeValue { S = ApplyStringLimit(value) };
    }

    internal static void AddOptionalStringAttribute(
        Dictionary<string, AttributeValue> attributes,
        string key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        attributes[key] = new AttributeValue { S = ApplyStringLimit(value) };
    }

    // This method ensures that any empty strings, empty sets, or empty maps are converted to DynamoDB's NULL representation.
    internal static Dictionary<string, AttributeValue> SanitizeAttributes(
        Dictionary<string, AttributeValue> attributes)
        => SanitizeAttributes(attributes, true);

    private static Dictionary<string, AttributeValue> SanitizeAttributes(
        Dictionary<string, AttributeValue> attributes,
        bool skipSystemKeyLimit)
    {
        var sanitized = new Dictionary<string, AttributeValue>(attributes.Count);
        foreach (var (key, value) in attributes)
        {
            var applyLimit = !skipSystemKeyLimit || !SystemKeyAttributes.Contains(key);
            sanitized[key] = SanitizeAttributeValue(value, applyLimit);
        }

        return sanitized;
    }

    private static AttributeValue SanitizeAttributeValue(AttributeValue value, bool applyStringLimit)
    {
        if (value == null)
        {
            return new AttributeValue { NULL = true };
        }

        if (value.S != null)
        {
            return string.IsNullOrWhiteSpace(value.S)
                ? new AttributeValue { NULL = true }
                : new AttributeValue { S = applyStringLimit ? ApplyStringLimit(value.S) : value.S };
        }

        if (value.N != null || value.B != null || value.NULL || value.BOOL)
        {
            return value;
        }

        if (value.M != null)
        {
            if (value.M.Count == 0)
            {
                return new AttributeValue { NULL = true };
            }

            return new AttributeValue { M = SanitizeAttributes(value.M, false) };
        }

        if (value.L != null)
        {
            if (value.L.Count == 0)
            {
                return new AttributeValue { NULL = true };
            }

            var sanitizedList = value.L.Select(item => SanitizeAttributeValue(item, true)).ToList();
            return new AttributeValue { L = sanitizedList };
        }

        if (value.SS != null)
        {
            if (value.SS.Count == 0)
            {
                return new AttributeValue { NULL = true };
            }

            return applyStringLimit
                ? new AttributeValue { SS = value.SS.Select(ApplyStringLimit).ToList() }
                : value;
        }

        if (value.NS != null)
        {
            return value.NS.Count == 0
                ? new AttributeValue { NULL = true }
                : value;
        }

        if (value.BS != null)
        {
            return value.BS.Count == 0
                ? new AttributeValue { NULL = true }
                : value;
        }

        return new AttributeValue { NULL = true };
    }

    private static string ApplyStringLimit(string value)
        => value.Length > DefaultStringLimit
            ? value.Substring(0, DefaultStringLimit)
            : value;
}
