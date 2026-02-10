using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Models;

namespace DoWeHaveItApp.Services;

public static class DtoMapper
{
    public static FolderDto ToDto(Folder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        ParentId = folder.ParentId,
        CreatedAt = folder.CreatedAt,
        UpdatedAt = folder.UpdatedAt,
    };

    public static ItemAttributeDto ToDto(ItemAttribute attribute) => new()
    {
        FieldId = attribute.FieldId,
        FieldName = attribute.FieldName,
        Value = attribute.Value,
    };

    public static ItemDto ToDto(Item item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Comments = item.Comments,
        ParentId = item.ParentId,
        Attributes = item.Attributes.Select(ToDto).ToList(),
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
    };

    public static FormFieldDto ToDto(FormField field) => new()
    {
        Id = field.Id,
        Name = field.Name,
        Type = field.Type,
        Required = field.Required,
    };

    public static FormTemplateDto ToDto(FormTemplate template) => new()
    {
        Id = template.Id,
        Name = template.Name,
        Fields = template.Fields.Select(ToDto).ToList(),
        CreatedAt = template.CreatedAt,
    };

    public static ItemAttribute ToModel(ItemAttributeDto attribute) => new()
    {
        FieldId = attribute.FieldId,
        FieldName = attribute.FieldName,
        Value = attribute.Value,
    };

    public static FormField ToModel(FormFieldDto field) => new()
    {
        Id = field.Id,
        Name = field.Name,
        Type = field.Type,
        Required = field.Required,
    };
}
