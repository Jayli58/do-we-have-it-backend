namespace DoWeHaveItApp.Dtos;

public sealed class ImageDownloadRequest
{
    public required string UserId { get; init; }
    public required string ItemId { get; init; }
    public required string S3Key { get; init; }
}
