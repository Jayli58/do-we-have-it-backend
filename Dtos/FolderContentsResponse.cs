namespace DoWeHaveItApp.Dtos;

public sealed class FolderContentsResponse
{
    public required IReadOnlyList<FolderDto> Folders { get; init; }
    public required IReadOnlyList<ItemDto> Items { get; init; }
}
