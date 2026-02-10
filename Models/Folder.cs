namespace DoWeHaveItApp.Models;

public sealed class Folder
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? ParentId { get; set; }
    public required string CreatedAt { get; set; }
    public required string UpdatedAt { get; set; }
}
