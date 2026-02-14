namespace DoWeHaveItApp.Dtos;

public sealed class CreateTemplateRequest
{
    public required string Name { get; init; }
    public required IReadOnlyList<FormFieldDto> Fields { get; init; }
}
