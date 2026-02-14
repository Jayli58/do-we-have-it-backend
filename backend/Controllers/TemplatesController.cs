using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace DoWeHaveItApp.Controllers;

[Route("templates")]
public sealed class TemplatesController : ApiControllerBase
{
    private readonly ITemplateService _templateService;

    public TemplatesController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormTemplateDto>>> Get()
    {
        var templates = await _templateService.GetTemplatesAsync(UserId);
        return Ok(templates);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FormTemplateDto>> GetById(string id)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(UserId, id);
            return Ok(template);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpPost]
    public async Task<ActionResult<FormTemplateDto>> Create([FromBody] CreateTemplateRequest request)
    {
        try
        {
            var template = await _templateService.CreateTemplateAsync(UserId, request);
            return Ok(template);
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FormTemplateDto>> Update(string id, [FromBody] UpdateTemplateRequest request)
    {
        if (!string.Equals(id, request.Id, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponse
            {
                Error = new ErrorDetail { Code = "validation_error", Message = "Template id mismatch." },
            });
        }

        try
        {
            var template = await _templateService.UpdateTemplateAsync(UserId, request);
            return Ok(template);
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
            await _templateService.DeleteTemplateAsync(UserId, id);
            return NoContent();
        }
        catch (ApiException ex)
        {
            return BuildErrorResponse(ex);
        }
    }
}
