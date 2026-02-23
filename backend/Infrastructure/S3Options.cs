namespace DoWeHaveItApp.Infrastructure;

public sealed class S3Options
{
    public string ImageBucket { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public bool UseLocal { get; init; }
    public string? ServiceUrl { get; init; }
}
