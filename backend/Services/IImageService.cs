using DoWeHaveItApp.Dtos;

namespace DoWeHaveItApp.Services;

public interface IImageService
{
    Task<string> UploadAsync(ImageUploadRequest request);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(ImageDownloadRequest request);
    Task DeleteAsync(string userId, string s3Key);
}
