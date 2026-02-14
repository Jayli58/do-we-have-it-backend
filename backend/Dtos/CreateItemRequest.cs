namespace DoWeHaveItApp.Dtos;

public sealed class CreateItemRequest
{
    public required string Name { get; init; }
    public required string Comments { get; init; }
    public string? ParentId { get; init; }
    public required IReadOnlyList<ItemAttributeDto> Attributes { get; init; }
}
