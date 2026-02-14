# DoWeHaveIt Backend - Implementation Plan

ASP.NET Core Web API backend for the DoWeHaveIt app, designed to match the frontend mock API contract and use a single DynamoDB table with user-scoped partition keys. Updates use PUT (full payload).

## Tech Stack

- Runtime: .NET 8, ASP.NET Core Web API
- Data: DynamoDB (single-table design)
- AWS SDK: AWSSDK.DynamoDBv2
- API docs: Swagger (built-in)

## API Contract (PUT for updates)

- `GET /folders?parentId=...` -> `{ folders, items }`
- `POST /folders`
- `PUT /folders/{id}` (full folder payload)
- `DELETE /folders/{id}` (cascade delete)
- `POST /items`
- `GET /items/{id}`
- `PUT /items/{id}` (full item payload)
- `DELETE /items/{id}`
- `GET /items/search?query=...&parentId=...` (name + comments)
- `GET /templates`
- `POST /templates`
- `PUT /templates/{id}` (full template payload)
- `DELETE /templates/{id}`
- `GET /templates/{id}` (import)

## DynamoDB Single-Table Design

Table: `Inventory`

Primary keys:

- PK: `USER#<userId>`
- SK: entity-specific

Folders:

- SK: `FOLDER#<parentId>#<folderId>`
- Attributes: `entityType=folder`, `folderId`, `parentId`, `name`, `createdAt`, `updatedAt`

Items:

- SK: `ITEM#<parentId>#<itemId>`
- Attributes: `entityType=item`, `itemId`, `parentId`, `name`, `comments`, `attributes[]`, `createdAt`, `updatedAt`

Templates:

- SK: `TEMPLATE#<templateId>`
- Attributes: `entityType=template`, `templateId`, `name`, `fields[]`, `createdAt`

Example keys:

- Folder in root: `PK=USER#u1`, `SK=FOLDER#ROOT#folder-kitchen`
- Item in a folder: `PK=USER#u1`, `SK=ITEM#folder-appliances#item-toaster`

## Search GSI (Name + Comments Only)

GSI1 keys (tokenized inverted index):

- GSI1PK: `USER#<userId>`
- GSI1SK: `TOKEN#<token>#PARENT#<parentId>#ITEM#<itemId>`

Tokenization rules:

- Lowercase
- Trim
- Remove punctuation
- Split on whitespace

Example item:

- name: "Coffee Maker"
- comments: "Top shelf"
- tokens: `coffee`, `maker`, `top`, `shelf`
- GSI1SK values:
  - `TOKEN#coffee#PARENT#folder-appliances#ITEM#item-123`
  - `TOKEN#maker#PARENT#folder-appliances#ITEM#item-123`
  - `TOKEN#top#PARENT#folder-appliances#ITEM#item-123`
  - `TOKEN#shelf#PARENT#folder-appliances#ITEM#item-123`

Search flow:

1. Normalize the user query to tokens.
2. Query GSI1 with `begins_with(GSI1SK, TOKEN#<token>#PARENT#<parentId>)`.
3. De-duplicate item IDs and fetch items (or project full attributes in the index).

## Local DynamoDB (LocalStack)

Local testing uses a LocalStack docker compose setup and an init script to create the table and GSI.

`local-aws/docker-compose.yml`

```yaml
version: "3.8"

services:
  localstack:
    container_name: localstack
    image: localstack/localstack:3.4
    ports:
      - "4566:4566"
    environment:
      - SERVICES=dynamodb
      - DEBUG=1
      - AWS_DEFAULT_REGION=ap-southeast-2
      - DYNAMODB_REMOVE_EXPIRED_ITEMS=1
    volumes:
      - "./localstack:/var/lib/localstack"
      - "./localstack-init:/etc/localstack/init/ready.d"
      - "/var/run/docker.sock:/var/run/docker.sock"
```

`local-aws/localstack-init/10-init-resources.sh`

```bash
#!/usr/bin/env bash
set -euo pipefail

echo "[init] Creating Inventory table..."

awslocal dynamodb create-table \
  --table-name Inventory \
  --attribute-definitions \
      AttributeName=PK,AttributeType=S \
      AttributeName=SK,AttributeType=S \
      AttributeName=GSI1PK,AttributeType=S \
      AttributeName=GSI1SK,AttributeType=S \
  --key-schema \
      AttributeName=PK,KeyType=HASH \
      AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
      "[\
        {\
          \"IndexName\": \"GSI1\",\
          \"KeySchema\": [\
            {\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},\
            {\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}\
          ],\
          \"Projection\": {\"ProjectionType\": \"ALL\"}\
        }\
      ]" \
  --billing-mode PAY_PER_REQUEST \
  >/dev/null 2>&1 || echo "[init] Inventory already exists"

echo "[init] Done."
```

LocalStack commands:

- Start: `docker compose up -d` (run in `local-aws`)
- Verify: `awslocal dynamodb list-tables`

## Cascade Delete (Folder)

- On delete folder, recursively delete:
  - child folders: `SK begins_with FOLDER#<parentId>#`
  - child items: `SK begins_with ITEM#<parentId>#`
- Use `BatchWriteItem` for deletes in chunks.
- Continue recursion until no child folders remain.

## Validation and Errors

- PUT requires full payload; missing fields return 400.
- Path ID must match body ID.
- `parentId` must exist for folder and item creation.
- Folder name uniqueness per parent (query existing siblings).
- Error format: `{ error: { code, message } }`
- Status codes: 400 (validation), 404 (missing), 409 (conflict)

## Suggested Folder Structure

```
DoWeHaveItApp/
  Controllers/
    FoldersController.cs
    ItemsController.cs
    TemplatesController.cs
  Models/
    Folder.cs
    Item.cs
    ItemAttribute.cs
    FormTemplate.cs
    FormField.cs
  Dtos/
    FolderDto.cs
    ItemDto.cs
    CreateFolderRequest.cs
    UpdateFolderRequest.cs
    CreateItemRequest.cs
    UpdateItemRequest.cs
    FormTemplateDto.cs
    CreateTemplateRequest.cs
    UpdateTemplateRequest.cs
    SearchResultDto.cs
  Repositories/
    IInventoryRepository.cs
    DynamoInventoryRepository.cs
  Services/
    InventoryService.cs
    TemplateService.cs
    SearchService.cs
  Infrastructure/
    DynamoDbOptions.cs
    DynamoDbClientFactory.cs
    Tokenizer.cs
  Mapping/
    MappingProfiles.cs
```

## Sample DTOs (shape matches frontend)

```csharp
public sealed class FolderDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}

public sealed class ItemAttributeDto
{
    public required string FieldId { get; init; }
    public required string FieldName { get; init; }
    public required string Value { get; init; }
}

public sealed class ItemDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Comments { get; init; }
    public string? ParentId { get; init; }
    public required IReadOnlyList<ItemAttributeDto> Attributes { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}

public sealed class FormFieldDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool Required { get; init; }
}

public sealed class FormTemplateDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<FormFieldDto> Fields { get; init; }
    public required string CreatedAt { get; init; }
}
```

## Implementation Order

1. Add DynamoDB configuration and options.
2. Implement tokenization utility for search.
3. Build repository layer (queries and writes).
4. Implement services (validation, cascade delete).
5. Add controllers + DTO mappings.
6. Add seed data (optional).
7. Smoke test endpoints via Swagger.

## Verification Checklist

- Create folders and items, list by parent.
- PUT updates require full payloads.
- Delete folder cascades to children.
- Search returns items by name or comments.
- Templates CRUD matches frontend expectations.
