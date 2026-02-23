using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Extensions;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace DoWeHaveItApp.Controllers;

[Route("items")]
public sealed class ItemsController : ApiControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ISearchService _searchService;
    private readonly IImageService _imageService;

    public ItemsController(
        IInventoryService inventoryService,
        ISearchService searchService,
        IImageService imageService)
    {
        _inventoryService = inventoryService;
        _searchService = searchService;
        _imageService = imageService;
    }

    [HttpPost]
    public async Task<ActionResult<ItemDto>> Create([FromForm] CreateItemRequest request)
    {
        try
        {
            request.Attributes = ItemRequestHelper.ParseAttributes(request.AttributesJson, request.Attributes);

            string? imageName = null;
            string? imageS3Key = null;
            var itemId = $"item-{Guid.NewGuid():N}";

            if (request.Image != null)
            {
                imageName = ItemRequestHelper.ResolveImageName(request.ImageName, request.Image.FileName);
                // await using ensures that the stream is disposed of when the scope exits
                // alike to try-with-resources in Java
                await using var imageStream = request.Image.OpenReadStream();
                imageS3Key = await _imageService.UploadAsync(new ImageUploadRequest
                {
                    UserId = UserId,
                    ItemId = itemId,
                    ImageName = imageName,
                    ImageStream = imageStream,
                    ContentType = request.Image.ContentType,
                });
            }

            var item = await _inventoryService.CreateItemAsync(new CreateItemContext
            {
                UserId = UserId,
                Request = request,
                ImageName = imageName,
                ImageS3Key = imageS3Key,
                ItemId = itemId,
            });
            return Ok(item);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ItemDto>> Get(string id)
    {
        try
        {
            var item = await _inventoryService.GetItemAsync(UserId, id);
            return Ok(item);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ItemDto>> Update(string id, [FromForm] UpdateItemRequest request)
    {
        if (!string.Equals(id, request.Id, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail { Code = "validation_error", Message = "Item id mismatch." },
            });
        }

        try
        {
            request.Attributes = ItemRequestHelper.ParseAttributes(request.AttributesJson, request.Attributes);

            var existing = await _inventoryService.GetItemModelAsync(UserId, id);

            string? imageName = null;
            string? imageS3Key = null;

            // upload new image
            if (request.Image != null)
            {
                imageName = ItemRequestHelper.ResolveImageName(request.ImageName, request.Image.FileName);
                await using var imageStream = request.Image.OpenReadStream();
                imageS3Key = await _imageService.UploadAsync(new ImageUploadRequest
                {
                    UserId = UserId,
                    ItemId = existing.Id,
                    ImageName = imageName,
                    ImageStream = imageStream,
                    ContentType = request.Image.ContentType,
                });

                if (!string.IsNullOrWhiteSpace(existing.ImageS3Key))
                {
                    await _imageService.DeleteAsync(UserId, existing.ImageS3Key);
                }
            }
            // remove existing image
            else if (request.ImageRemoved == true && !string.IsNullOrWhiteSpace(existing.ImageS3Key))
            {
                await _imageService.DeleteAsync(UserId, existing.ImageS3Key);
            }

            var item = await _inventoryService.UpdateItemAsync(UserId, request, imageName, imageS3Key);
            return Ok(item);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string? parentId)
    {
        try
        {
            var existing = await _inventoryService.GetItemModelAsync(UserId, id);
            if (!string.IsNullOrWhiteSpace(existing.ImageS3Key))
            {
                await _imageService.DeleteAsync(UserId, existing.ImageS3Key);
            }

            await _inventoryService.DeleteItemAsync(UserId, id, parentId);
            return NoContent();
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpGet("{id}/img")]
    public async Task<IActionResult> GetImage(string id)
    {
        try
        {
            var item = await _inventoryService.GetItemModelAsync(UserId, id);
            if (string.IsNullOrWhiteSpace(item.ImageS3Key))
            {
                throw new ApiException(404, "not_found", "Image not found.");
            }

            var result = await _imageService.DownloadAsync(new ImageDownloadRequest
            {
                UserId = UserId,
                ItemId = item.Id,
                S3Key = item.ImageS3Key,
            });

            return File(result.Stream, result.ContentType, result.FileName);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<SearchResultDto>> Search([FromQuery] string query)
    {
        var results = await _searchService.SearchItemsAsync(UserId, query);
        return Ok(results);
    }

}
