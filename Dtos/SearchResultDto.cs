namespace DoWeHaveItApp.Dtos;

public sealed class SearchResultDto
{
    public required IReadOnlyList<ItemDto> Items { get; init; }
}
