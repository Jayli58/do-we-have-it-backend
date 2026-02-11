using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Infrastructure.Dynamo;
using DoWeHaveItApp.Models;
using Microsoft.Extensions.Options;

namespace DoWeHaveItApp.Repositories;

// Implements an inventory repository using Amazon DynamoDB as the storage backend.
// Raw DynamoDB operations are used for fine-grained control over data modeling and performance optimization.
public sealed class DynamoInventoryRepository : IInventoryRepository
{
    private const string RootParentId = "ROOT";
    private const string FolderEntity = "folder";
    private const string ItemEntity = "item";
    private const string TemplateEntity = "template";
    private const string SearchEntity = "search";

    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbOptions _options;
    private readonly Tokenizer _tokenizer;

    public DynamoInventoryRepository(
        IAmazonDynamoDB client,
        IOptions<DynamoDbOptions> options,
        Tokenizer tokenizer)
    {
        _client = client;
        _options = options.Value;
        _tokenizer = tokenizer;
    }

    private string TableName => _options.TableName;

    public async Task<IReadOnlyList<Folder>> GetFoldersByParentAsync(string userId, string? parentId)
    {
        var parentKey = NormalizeParent(parentId);
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue(BuildPk(userId)),
                [":skPrefix"] = new AttributeValue($"FOLDER#{parentKey}#"),
            },
        });

        return response.Items.Select(MapFolder).ToList();
    }

    public async Task<IReadOnlyList<Item>> GetItemsByParentAsync(string userId, string? parentId)
    {
        var parentKey = NormalizeParent(parentId);
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue(BuildPk(userId)),
                [":skPrefix"] = new AttributeValue($"ITEM#{parentKey}#"),
            },
        });

        return response.Items.Select(MapItem).ToList();
    }

    public async Task<Folder?> GetFolderByIdAsync(string userId, string folderId)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk",
            FilterExpression = "entityType = :type AND folderId = :folderId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue(BuildPk(userId)),
                [":type"] = new AttributeValue(FolderEntity),
                [":folderId"] = new AttributeValue(folderId),
            },
        });

        var item = response.Items.FirstOrDefault();
        return item == null ? null : MapFolder(item);
    }

    public async Task<Item?> GetItemByIdAsync(string userId, string itemId)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk",
            FilterExpression = "entityType = :type AND itemId = :itemId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue(BuildPk(userId)),
                [":type"] = new AttributeValue(ItemEntity),
                [":itemId"] = new AttributeValue(itemId),
            },
        });

        var item = response.Items.FirstOrDefault();
        return item == null ? null : MapItem(item);
    }

    public async Task CreateFolderAsync(string userId, Folder folder)
    {
        var parentKey = NormalizeParent(folder.ParentId);
        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = BuildFolderRecord(userId, folder, parentKey),
        };

        await _client.PutItemAsync(request);
    }

    public async Task UpdateFolderAsync(string userId, Folder folder)
    {
        var parentKey = NormalizeParent(folder.ParentId);
        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = BuildFolderRecord(userId, folder, parentKey),
        };

        await _client.PutItemAsync(request);
    }

    public async Task DeleteFolderAsync(string userId, string? parentId, string folderId)
    {
        var parentKey = NormalizeParent(parentId);
        await _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(BuildPk(userId)),
                ["SK"] = new AttributeValue(BuildFolderSk(parentKey, folderId)),
            },
        });
    }

    public async Task CreateItemAsync(string userId, Item item)
    {
        var parentKey = NormalizeParent(item.ParentId);
        var itemRecord = BuildItemRecord(userId, item, parentKey);
        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = itemRecord,
        });

        var writeRequests = BuildSearchRecords(userId, item, parentKey).ToList();
        if (writeRequests.Count == 0)
        {
            return;
        }

        await BatchWriteAsync(writeRequests);
    }

    public async Task UpdateItemAsync(string userId, Item item)
    {
        var parentKey = NormalizeParent(item.ParentId);
        await DeleteSearchRecordsAsync(userId, item.Id);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = BuildItemRecord(userId, item, parentKey),
        });

        var writeRequests = BuildSearchRecords(userId, item, parentKey).ToList();
        if (writeRequests.Count == 0)
        {
            return;
        }

        await BatchWriteAsync(writeRequests);
    }

    public async Task DeleteItemAsync(string userId, string? parentId, string itemId)
    {
        var parentKey = NormalizeParent(parentId);
        var writeRequests = new List<WriteRequest>
        {
            new()
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new AttributeValue(BuildPk(userId)),
                        ["SK"] = new AttributeValue(BuildItemSk(parentKey, itemId)),
                    },
                },
            },
        };

        writeRequests.AddRange(await BuildDeleteSearchRequestsAsync(userId, itemId));
        await BatchWriteAsync(writeRequests);
    }

    public async Task<IReadOnlyList<Item>> SearchItemsAsync(string userId, string query)
    {
        var tokens = _tokenizer.Tokenize(query).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tokens.Count == 0)
        {
            return Array.Empty<Item>();
        }

        HashSet<(string ParentKey, string ItemId)>? itemKeys = null;

        foreach (var token in tokens)
        {
            var tokenKeys = new HashSet<(string ParentKey, string ItemId)>();
            Dictionary<string, AttributeValue>? lastEvaluatedKey = null;
            do
            {
                var response = await _client.ScanAsync(new ScanRequest
                {
                    TableName = TableName,
                    IndexName = "GSI1",
                    FilterExpression = "GSI1PK = :pk AND begins_with(GSI1SK, :skPrefix)",
                    ExclusiveStartKey = lastEvaluatedKey,
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":pk"] = new AttributeValue(BuildPk(userId)),
                        [":skPrefix"] = new AttributeValue($"TOKEN#{token}"),
                    },
                });

                foreach (var item in response.Items)
                {
                    var itemId = GetString(item, "itemId");
                    var itemParent = GetString(item, "parentId");
                    if (!string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrWhiteSpace(itemParent))
                    {
                        tokenKeys.Add((itemParent, itemId));
                    }
                }

                lastEvaluatedKey = response.LastEvaluatedKey;
            } while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);

            if (itemKeys == null)
            {
                itemKeys = tokenKeys;
            }
            else
            {
                itemKeys.IntersectWith(tokenKeys);
            }

            if (itemKeys.Count == 0)
            {
                return Array.Empty<Item>();
            }
        }

        if (itemKeys == null || itemKeys.Count == 0)
        {
            return Array.Empty<Item>();
        }

        var results = await BatchGetItemsAsync(userId, itemKeys);
        return results;
    }

    public async Task<IReadOnlyList<FormTemplate>> GetTemplatesAsync(string userId)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue(BuildPk(userId)),
                [":skPrefix"] = new AttributeValue("TEMPLATE#"),
            },
        });

        return response.Items.Select(MapTemplate).ToList();
    }

    public async Task<FormTemplate?> GetTemplateAsync(string userId, string templateId)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(BuildPk(userId)),
                ["SK"] = new AttributeValue(BuildTemplateSk(templateId)),
            },
        });

        return response.Item.Count == 0 ? null : MapTemplate(response.Item);
    }

    public async Task CreateTemplateAsync(string userId, FormTemplate template)
    {
        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = BuildTemplateRecord(userId, template),
        });
    }

    public async Task UpdateTemplateAsync(string userId, FormTemplate template)
    {
        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = BuildTemplateRecord(userId, template),
        });
    }

    public async Task DeleteTemplateAsync(string userId, string templateId)
    {
        await _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(BuildPk(userId)),
                ["SK"] = new AttributeValue(BuildTemplateSk(templateId)),
            },
        });
    }

    private static string BuildPk(string userId) => $"USER#{userId}";

    private static string NormalizeParent(string? parentId)
        => string.IsNullOrWhiteSpace(parentId) ? RootParentId : parentId;

    private static string? NormalizeParentForDto(string parentKey)
        => parentKey == RootParentId ? null : parentKey;

    private static string BuildFolderSk(string parentKey, string folderId)
        => $"FOLDER#{parentKey}#{folderId}";

    private static string BuildItemSk(string parentKey, string itemId)
        => $"ITEM#{parentKey}#{itemId}";

    private static string BuildTemplateSk(string templateId)
        => $"TEMPLATE#{templateId}";

    private static string BuildSearchSk(string itemId, string token, string parentKey)
        => $"SEARCH#ITEM#{itemId}#TOKEN#{token}#PARENT#{parentKey}";

    private static string BuildGsi1Sk(string token, string parentKey, string itemId)
        => $"TOKEN#{token}#PARENT#{parentKey}#ITEM#{itemId}";


    private Dictionary<string, AttributeValue> BuildFolderRecord(string userId, Folder folder, string parentKey)
    {
        return DynamoAttributeBuilder.SanitizeAttributes(new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue(BuildPk(userId)),
            ["SK"] = new AttributeValue(BuildFolderSk(parentKey, folder.Id)),
            ["entityType"] = new AttributeValue(FolderEntity),
            ["folderId"] = new AttributeValue(folder.Id),
            ["parentId"] = new AttributeValue(parentKey),
            ["name"] = new AttributeValue(folder.Name),
            ["createdAt"] = new AttributeValue(folder.CreatedAt),
            ["updatedAt"] = new AttributeValue(folder.UpdatedAt),
        });
    }

    private Dictionary<string, AttributeValue> BuildItemRecord(string userId, Item item, string parentKey)
    {
        var attributes = new List<AttributeValue>();
        foreach (var attribute in item.Attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.FieldId) || string.IsNullOrWhiteSpace(attribute.FieldName))
            {
                continue;
            }

            var attributeMap = new Dictionary<string, AttributeValue>
            {
                ["fieldId"] = DynamoAttributeBuilder.BuildStringAttribute(attribute.FieldId),
                ["fieldName"] = DynamoAttributeBuilder.BuildStringAttribute(attribute.FieldName),
            };

            DynamoAttributeBuilder.AddOptionalStringAttribute(attributeMap, "value", attribute.Value);

            if (attributeMap.Count > 0)
            {
                attributes.Add(new AttributeValue { M = attributeMap });
            }
        }

        var record = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue(BuildPk(userId)),
            ["SK"] = new AttributeValue(BuildItemSk(parentKey, item.Id)),
            ["entityType"] = new AttributeValue(ItemEntity),
            ["itemId"] = new AttributeValue(item.Id),
            ["parentId"] = new AttributeValue(parentKey),
            ["name"] = new AttributeValue(item.Name),
            ["createdAt"] = new AttributeValue(item.CreatedAt),
            ["updatedAt"] = new AttributeValue(item.UpdatedAt),
        };

        DynamoAttributeBuilder.AddOptionalStringAttribute(record, "comments", item.Comments);

        if (attributes.Count > 0)
        {
            var attributesValue = new AttributeValue { L = new List<AttributeValue>() };
            attributesValue.L.AddRange(attributes);
            record["attributes"] = attributesValue;
        }

        return DynamoAttributeBuilder.SanitizeAttributes(record);
    }

    private Dictionary<string, AttributeValue> BuildTemplateRecord(string userId, FormTemplate template)
    {
        var fields = template.Fields.Select(field => new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["id"] = DynamoAttributeBuilder.BuildStringAttribute(field.Id),
                ["name"] = DynamoAttributeBuilder.BuildStringAttribute(field.Name),
                ["type"] = DynamoAttributeBuilder.BuildStringAttribute(field.Type),
                ["required"] = new AttributeValue { BOOL = field.Required },
            },
        }).ToList();

        var record = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue(BuildPk(userId)),
            ["SK"] = new AttributeValue(BuildTemplateSk(template.Id)),
            ["entityType"] = new AttributeValue(TemplateEntity),
            ["templateId"] = DynamoAttributeBuilder.BuildStringAttribute(template.Id),
            ["name"] = DynamoAttributeBuilder.BuildStringAttribute(template.Name),
            ["fields"] = new AttributeValue { L = new List<AttributeValue>() },
            ["createdAt"] = DynamoAttributeBuilder.BuildStringAttribute(template.CreatedAt),
        };

        record["fields"].L.AddRange(fields);

        return DynamoAttributeBuilder.SanitizeAttributes(record);
    }

    private IEnumerable<WriteRequest> BuildSearchRecords(string userId, Item item, string parentKey)
    {
        var tokens = _tokenizer.Tokenize(item.Name, item.Comments)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var token in tokens)
        {
            var record = DynamoAttributeBuilder.SanitizeAttributes(new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(BuildPk(userId)),
                ["SK"] = new AttributeValue(BuildSearchSk(item.Id, token, parentKey)),
                ["entityType"] = new AttributeValue(SearchEntity),
                ["itemId"] = new AttributeValue(item.Id),
                ["parentId"] = new AttributeValue(parentKey),
                ["token"] = new AttributeValue(token),
                ["GSI1PK"] = new AttributeValue(BuildPk(userId)),
                ["GSI1SK"] = new AttributeValue(BuildGsi1Sk(token, parentKey, item.Id)),
            });

            yield return new WriteRequest { PutRequest = new PutRequest { Item = record } };
        }
    }

    private async Task DeleteSearchRecordsAsync(string userId, string itemId)
    {
        var requests = await BuildDeleteSearchRequestsAsync(userId, itemId);
        if (requests.Count == 0)
        {
            return;
        }

        await BatchWriteAsync(requests);
    }

    private async Task<List<WriteRequest>> BuildDeleteSearchRequestsAsync(string userId, string itemId)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue(BuildPk(userId)),
                [":skPrefix"] = new AttributeValue($"SEARCH#ITEM#{itemId}#"),
            },
        });

        var requests = new List<WriteRequest>();
        foreach (var item in response.Items)
        {
            requests.Add(new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = item["PK"],
                        ["SK"] = item["SK"],
                    },
                },
            });
        }

        return requests;
    }

    private async Task<IReadOnlyList<Item>> BatchGetItemsAsync(
        string userId,
        IEnumerable<(string ParentKey, string ItemId)> itemKeys)
    {
        var keys = itemKeys.Select(key => new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue(BuildPk(userId)),
            ["SK"] = new AttributeValue(BuildItemSk(key.ParentKey, key.ItemId)),
        }).ToList();

        var results = new List<Item>();
        foreach (var batch in Chunk(keys, 100))
        {
            var request = new BatchGetItemRequest
            {
                RequestItems = new Dictionary<string, KeysAndAttributes>
                {
                    [TableName] = new KeysAndAttributes { Keys = batch },
                },
            };

            BatchGetItemResponse response;
            do
            {
                response = await _client.BatchGetItemAsync(request);
                if (response.Responses.TryGetValue(TableName, out var items))
                {
                    results.AddRange(items.Select(MapItem));
                }

                request.RequestItems = response.UnprocessedKeys;
            } while (request.RequestItems.Count > 0);
        }

        return results;
    }

    private async Task BatchWriteAsync(List<WriteRequest> requests)
    {
        foreach (var batch in Chunk(requests, 25))
        {
            var request = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [TableName] = batch,
                },
            };

            BatchWriteItemResponse response;
            do
            {
                response = await _client.BatchWriteItemAsync(request);
                request.RequestItems = response.UnprocessedItems;
            } while (request.RequestItems.Count > 0);
        }
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private static Folder MapFolder(Dictionary<string, AttributeValue> item)
    {
        return new Folder
        {
            Id = GetString(item, "folderId"),
            Name = GetString(item, "name"),
            ParentId = NormalizeParentForDto(GetString(item, "parentId")),
            CreatedAt = GetString(item, "createdAt"),
            UpdatedAt = GetString(item, "updatedAt"),
        };
    }

    private static Item MapItem(Dictionary<string, AttributeValue> item)
    {
        var attributes = new List<ItemAttribute>();
        if (item.TryGetValue("attributes", out var attributeValue) && attributeValue.L != null)
        {
            foreach (var attribute in attributeValue.L)
            {
                if (attribute.M == null)
                {
                    continue;
                }

                attributes.Add(new ItemAttribute
                {
                    FieldId = GetString(attribute.M, "fieldId"),
                    FieldName = GetString(attribute.M, "fieldName"),
                    Value = GetString(attribute.M, "value"),
                });
            }
        }

        return new Item
        {
            Id = GetString(item, "itemId"),
            Name = GetString(item, "name"),
            Comments = GetString(item, "comments"),
            ParentId = NormalizeParentForDto(GetString(item, "parentId")),
            Attributes = attributes,
            CreatedAt = GetString(item, "createdAt"),
            UpdatedAt = GetString(item, "updatedAt"),
        };
    }

    private static FormTemplate MapTemplate(Dictionary<string, AttributeValue> item)
    {
        var fields = new List<FormField>();
        if (item.TryGetValue("fields", out var fieldValue) && fieldValue.L != null)
        {
            foreach (var field in fieldValue.L)
            {
                if (field.M == null)
                {
                    continue;
                }

                fields.Add(new FormField
                {
                    Id = GetString(field.M, "id"),
                    Name = GetString(field.M, "name"),
                    Type = GetString(field.M, "type"),
                    Required = field.M.TryGetValue("required", out var required) && required.BOOL,
                });
            }
        }

        return new FormTemplate
        {
            Id = GetString(item, "templateId"),
            Name = GetString(item, "name"),
            Fields = fields,
            CreatedAt = GetString(item, "createdAt"),
        };
    }

    private static string GetString(Dictionary<string, AttributeValue> item, string key)
    {
        return item.TryGetValue(key, out var value) ? value.S ?? string.Empty : string.Empty;
    }
}
