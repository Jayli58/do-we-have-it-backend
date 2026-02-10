namespace DoWeHaveItApp.Dtos;

public sealed class FormFieldDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool Required { get; init; }
}
