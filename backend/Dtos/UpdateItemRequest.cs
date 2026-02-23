using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DoWeHaveItApp.Dtos;

public sealed class UpdateItemRequest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Comments { get; init; }
    public string? ParentId { get; init; }

    // let .net not bind attributes from form data; we will parse it ourselves
    // only needed in multipart/form-data requests
    [BindNever]
    public IReadOnlyList<ItemAttributeDto>? Attributes { get; set; }

    // read attributes from form data instead of json body
    [FromForm(Name = "attributes")]
    public string? AttributesJson { get; init; }

    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
    public IFormFile? Image { get; init; }
    public string? ImageName { get; init; }
    public bool? ImageRemoved { get; init; }
}
