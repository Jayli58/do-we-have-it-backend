using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace DoWeHaveItApp.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    // Get user ID from header
    protected string UserId => HttpContext.Request.Headers.TryGetValue("X-User-Id", out var values) &&
                               !string.IsNullOrWhiteSpace(values)
        ? values.ToString()
        : "demo-user";

    // Error handling
    // Need try catch blocks in controllers to convert exceptions into HTTP responses
    protected ActionResult BuildErrorResponse(ApiException exception)
    {
        return StatusCode(exception.StatusCode, new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = exception.Code,
                Message = exception.Message,
            },
        });
    }
}
