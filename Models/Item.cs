namespace DoWeHaveItApp.Models;

public sealed class Item
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Comments { get; set; }
    public string? ParentId { get; set; }
    public required List<ItemAttribute> Attributes { get; set; }
    public required string CreatedAt { get; set; }
    public required string UpdatedAt { get; set; }
}
