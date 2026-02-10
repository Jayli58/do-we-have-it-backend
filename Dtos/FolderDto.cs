namespace DoWeHaveItApp.Dtos;

public sealed class FolderDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
