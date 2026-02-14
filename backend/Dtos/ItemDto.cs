namespace DoWeHaveItApp.Dtos;

public sealed class ItemDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Comments { get; init; }
    public string? ParentId { get; init; }
    public required IReadOnlyList<ItemAttributeDto> Attributes { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
