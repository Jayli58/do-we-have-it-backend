# Image Attachment for Items

Add single-image attachment support to items. Images are stored in S3 with user-scoped keys. The `GET items/{id}` response includes image metadata (or `null` if no image), and a new `GET items/{id}/img` endpoint streams the image after verifying token ownership.

**Constraints**: Max upload size 10 MB. Backend compresses all uploads to JPG before storing to S3.

## User Review Required

> [!IMPORTANT]
> **Multipart form-data for POST/PUT** — Because the frontend sends `image` as binary alongside JSON fields, the `POST /items` and `PUT /items/{id}` endpoints must switch from `[FromBody] JSON` to `[FromForm] multipart/form-data`. This is a breaking change to the existing API contract. The request DTO will accept `IFormFile? Image`, `string? ImageName`, and `bool? ImageRemoved` alongside the existing JSON-serialized fields.

> [!WARNING]
> **S3 bucket for images** — A new S3 bucket needs to be provisioned in the CDK `api-stack.ts` and the Lambda needs `s3:PutObject`, `s3:GetObject`, `s3:DeleteObject` permissions. The bucket name will be passed to the Lambda via the `S3__ImageBucket` environment variable. **This requires a CDK deployment.**

> [!IMPORTANT]
> **LocalStack S3 for local dev** — The `docker-compose.yml` needs to add `s3` to `SERVICES` so LocalStack provisions a local S3 endpoint. An init script should create the image bucket.

## Proposed Changes

### S3 Key Design

S3 object key pattern: `{userId}/{itemId}/{originalName}.jpg`

- All images are compressed to JPG before upload, so the stored key always ends in `.jpg`.
- Scopes images per user and per item for easy cleanup on item delete.
- Metadata tag `userId` stored on the S3 object for ownership verification in the GET image endpoint.

---

### Models & DTOs

#### [MODIFY] [Item.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Models/Item.cs)
- Add `string? ImageName` and `string? ImageS3Key` properties to the domain model.

#### [NEW] [ImageInfo.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Dtos/ImageInfo.cs)
- New DTO class with `Name` (string) only. The frontend already knows to call `items/{id}/img` to fetch the image, so no URL is needed.

#### [MODIFY] [ItemDto.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Dtos/ItemDto.cs)
- Add `ImageInfo? Image` property (will be `null` when no image attached).

#### [MODIFY] [CreateItemRequest.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Dtos/CreateItemRequest.cs)
- Add `IFormFile? Image` and `string? ImageName` properties.
- Change non-collection properties to bind from form (attributes need special handling — see note below).

#### [MODIFY] [UpdateItemRequest.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Dtos/UpdateItemRequest.cs)
- Add `IFormFile? Image`, `string? ImageName`, and `bool? ImageRemoved` properties.

> [!NOTE]
> **Attributes serialization in multipart** — Frontend sends `attributes` as a JSON string within the form data (e.g., `attributes: "[{...}]"`). Controller deserializes it.

---

### Image Service (new abstraction)

#### [NEW] [ImageUploadRequest.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Dtos/ImageUploadRequest.cs)
```csharp
public sealed class ImageUploadRequest
{
    public required string UserId { get; init; }
    public required string ItemId { get; init; }
    public required string ImageName { get; init; }
    public required Stream ImageStream { get; init; }
    public required string ContentType { get; init; }
}
```

#### [NEW] [ImageDownloadRequest.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Dtos/ImageDownloadRequest.cs)
```csharp
public sealed class ImageDownloadRequest
{
    public required string UserId { get; init; }
    public required string ItemId { get; init; }
    public required string S3Key { get; init; }
}
```

#### [NEW] [IImageService.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Services/IImageService.cs)
```csharp
public interface IImageService
{
    Task<string> UploadAsync(ImageUploadRequest request);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(ImageDownloadRequest request);
    Task DeleteAsync(string userId, string s3Key);
}
```

#### [NEW] [S3ImageService.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Services/S3ImageService.cs)
- Implements `IImageService` using `IAmazonS3`.
- `UploadAsync`:
  1. Validate file size ≤ 10 MB, reject with 400 if exceeded.
  2. Compress image to JPG using SkiaSharp (`SKBitmap` → `SKImage.Encode(SKEncodedImageFormat.Jpeg, 80)`).
  3. PutObject the compressed stream to `{userId}/{itemId}/{name}.jpg` with `userId` metadata and `content-type: image/jpeg`.
- `DownloadAsync`: GetObject, verifies `userId` metadata matches the caller.
- `DeleteAsync`: GetObject metadata first to verify `userId` ownership, then DeleteObject.

---

### Infrastructure / Configuration

#### [NEW] [S3Options.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Infrastructure/S3Options.cs)
- `ImageBucket` (string), `Region` (string), `UseLocal` (bool), `ServiceUrl` (string?).

#### [MODIFY] [Program.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Program.cs)
- Add `AWSSDK.S3` client registration (respect local/prod config like DynamoDB).
- Register `IImageService → S3ImageService`.
- Bind `S3Options` from configuration section `"S3"`.

#### [MODIFY] [DoWeHaveItApp.csproj](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/DoWeHaveItApp.csproj)
- Add `<PackageReference Include="AWSSDK.S3" ... />`.
- Add `<PackageReference Include="SkiaSharp" ... />` for JPG compression.

#### [MODIFY] [appsettings.json](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/appsettings.json)
- Add `"S3"` section with `ImageBucket`, `Region`, `UseLocal`, `ServiceUrl`.

#### [MODIFY] [appsettings.Development.json](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/appsettings.Development.json)
- Add `"S3"` section pointing to LocalStack (`UseLocal: true`, `ServiceUrl: http://localhost:4566`).

---

### DynamoDB Repository

#### [MODIFY] [DynamoInventoryRepository.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Repositories/DynamoInventoryRepository.cs)
- `BuildItemRecord`: persist `imageName` and `imageS3Key` as optional string attributes.
- `MapItem`: read `imageName` and `imageS3Key` from DynamoDB record.

---

### Controller

#### [MODIFY] [ItemsController.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Controllers/ItemsController.cs)
- Inject `IImageService`.
- `Create` (`POST /items`): switch to `[FromForm]`. Validate file ≤ 10 MB. If `Image` file is present, upload to S3 via `IImageService` (compresses to JPG internally), store returned S3 key in the item.
- `Get` (`GET /items/{id}`): no change needed — `DtoMapper` maps `ImageName`/`ImageS3Key` into `ImageInfo` DTO (or `null` if no image).
- `Update` (`PUT /items/{id}`): switch to `[FromForm]`. Handle three cases:
  1. New image attached → upload to S3, delete old if exists.
  2. `ImageRemoved = true` → delete from S3, clear image fields.
  3. No image change → preserve existing image fields.
- Add new `GetImage` action: `[HttpGet("{id}/img")]` → calls `IImageService.GetImageAsync`, streams file back.

---

### DTO Mapper

#### [MODIFY] [DtoMapper.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Services/DtoMapper.cs)
- Update `ToDto(Item)` to populate `ImageInfo` when `ImageName` is not null (sets `Name` only).

---

### Inventory Service

#### [MODIFY] [IInventoryService.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Services/IInventoryService.cs)
- Update `CreateItemAsync` and `UpdateItemAsync` signatures to accept optional image params (S3 key, image name).

#### [MODIFY] [InventoryService.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/Services/InventoryService.cs)
- `CreateItemAsync`: set `ImageName` and `ImageS3Key` on the `Item` model.
- `UpdateItemAsync`: handle image update/removal — preserve or clear image fields.
- `DeleteItemAsync`: delegate image cleanup to the controller or service (delete S3 object if image exists).

---

### CDK Infrastructure

#### [MODIFY] [api-stack.ts](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/infra/lib/api-stack.ts)
- Create a new **private** S3 bucket `DWHIImageBucket` (block public access, S3 managed encryption).
- Grant Lambda `s3:PutObject`, `s3:GetObject`, `s3:DeleteObject` on the bucket.
- Pass `S3__ImageBucket` and `S3__Region` as Lambda environment variables.

---

### LocalStack

#### [MODIFY] [docker-compose.yml](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/local-aws/docker-compose.yml)
- Add `s3` to `SERVICES`.

#### [MODIFY] [10-init-resources.sh](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/local-aws/localstack-init/10-init-resources.sh)
- Add `awslocal s3 mb s3://dwhi-images` to create the local image bucket.

---

### Tests

#### [MODIFY] [ItemsControllerTests.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/DoWeHaveItApp.Tests/ItemsControllerTests.cs)
- Update `ThrowingInventoryService` stub to match any new `IInventoryService` signatures.

#### [MODIFY] [InMemoryInventoryRepository.cs](file:///c:/Users/Lee58/source/repos/DoWeHaveItApp/backend/DoWeHaveItApp.Tests/InMemoryInventoryRepository.cs)
- Update in-memory repo to persist/return `ImageName` and `ImageS3Key`.

---

## Verification Plan

### Automated Tests
- **Existing tests must still pass**: `dotnet test` in `backend/DoWeHaveItApp.Tests/`
- Unit tests for `S3ImageService` (mock `IAmazonS3`)
- Update existing `ItemsControllerTests` and `InventoryServiceTests` to account for new image fields

### Manual Verification
> [!NOTE]
> Since integration tests require LocalStack running with both DynamoDB and S3, I recommend running a quick manual smoke test:
1. Start LocalStack: `docker compose up -d` in `backend/local-aws/`
2. Run the backend: `dotnet run` in `backend/`
3. Use Swagger or curl to:
   - `POST /items` with multipart form including an image file
   - `GET /items/{id}` — verify response includes `image: { name }` (or `image: null` when none)
   - `GET /items/{id}/img` — verify image binary is returned
   - `PUT /items/{id}` with `imageRemoved=true` — verify image is cleared
   - `DELETE /items/{id}` — verify S3 object is cleaned up
