namespace DoWeHaveItApp.Dtos;

public sealed class CreateItemContext
{
    public required string UserId { get; init; }
    public required CreateItemRequest Request { get; init; }
    public string? ImageName { get; init; }
    public string? ImageS3Key { get; init; }
    public string? ItemId { get; init; }
}
