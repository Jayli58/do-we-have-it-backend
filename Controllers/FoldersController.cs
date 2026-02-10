using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace DoWeHaveItApp.Controllers;

[Route("folders")]
public sealed class FoldersController : ApiControllerBase
{
    private readonly IInventoryService _inventoryService;

    public FoldersController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<ActionResult<FolderContentsResponse>> Get([FromQuery] string? parentId)
    {
        var response = await _inventoryService.GetFolderContentsAsync(UserId, parentId);
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<FolderDto>> Create([FromBody] CreateFolderRequest request)
    {
        try
        {
            var folder = await _inventoryService.CreateFolderAsync(UserId, request);
            return Ok(folder);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FolderDto>> Update(string id, [FromBody] UpdateFolderRequest request)
    {
        if (!string.Equals(id, request.Id, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail { Code = "validation_error", Message = "Folder id mismatch." },
            });
        }

        try
        {
            var folder = await _inventoryService.UpdateFolderAsync(UserId, request);
            return Ok(folder);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await _inventoryService.DeleteFolderAsync(UserId, id);
            return NoContent();
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }
}
