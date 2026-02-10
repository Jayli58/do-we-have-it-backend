namespace DoWeHaveItApp.Dtos;

public sealed class FormTemplateDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<FormFieldDto> Fields { get; init; }
    public required string CreatedAt { get; init; }
}
