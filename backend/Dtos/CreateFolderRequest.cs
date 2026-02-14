namespace DoWeHaveItApp.Dtos;

public sealed class CreateFolderRequest
{
    public required string Name { get; init; }
    public string? ParentId { get; init; }
}
