using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace DoWeHaveItApp.Controllers;

[Route("items")]
public sealed class ItemsController : ApiControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ISearchService _searchService;

    public ItemsController(IInventoryService inventoryService, ISearchService searchService)
    {
        _inventoryService = inventoryService;
        _searchService = searchService;
    }

    [HttpPost]
    public async Task<ActionResult<ItemDto>> Create([FromBody] CreateItemRequest request)
    {
        try
        {
            var item = await _inventoryService.CreateItemAsync(UserId, request);
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
    public async Task<ActionResult<ItemDto>> Update(string id, [FromBody] UpdateItemRequest request)
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
            var item = await _inventoryService.UpdateItemAsync(UserId, request);
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
            await _inventoryService.DeleteItemAsync(UserId, id, parentId);
            return NoContent();
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
