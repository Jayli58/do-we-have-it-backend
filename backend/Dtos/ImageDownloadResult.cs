namespace DoWeHaveItApp.Dtos;

public sealed class ImageDownloadResult
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public string? ETag { get; init; }
}
