namespace DoWeHaveItApp.Dtos;

public sealed class ImageUploadRequest
{
    public required string UserId { get; init; }
    public required string ItemId { get; init; }
    public required string ImageName { get; init; }
    public required Stream ImageStream { get; init; }
    public required string ContentType { get; init; }
}
