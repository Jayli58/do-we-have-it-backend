using Amazon.S3;
using Amazon.S3.Model;
using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Infrastructure;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System.Net;

namespace DoWeHaveItApp.Services;

public sealed class S3ImageService : IImageService
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private const int JpegQuality = 80;
    private const string JpegContentType = "image/jpeg";

    private readonly IAmazonS3 _client;
    private readonly S3Options _options;

    public S3ImageService(IAmazonS3 client, IOptions<S3Options> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<string> UploadAsync(ImageUploadRequest request)
    {
        var sourceStream = request.ImageStream;
        MemoryStream? bufferedStream = null;
        try
        {
            // if the stream doesn't have a length or can't be rewound, copy it to a memory stream
            if (!sourceStream.CanSeek)
            {
                bufferedStream = new MemoryStream();
                await sourceStream.CopyToAsync(bufferedStream);
                if (bufferedStream.Length > MaxUploadBytes)
                {
                    throw new ApiException(400, "validation_error", "Image exceeds 10 MB limit.");
                }
                // reset the memory stream's read pointer back to the start
                bufferedStream.Position = 0;
                sourceStream = bufferedStream;
            }
            else if (sourceStream.Length > MaxUploadBytes)
            {
                throw new ApiException(400, "validation_error", "Image exceeds 10 MB limit.");
            }

            if (sourceStream.CanSeek)
            {
                sourceStream.Position = 0;
            }

            using var bitmap = SKBitmap.Decode(sourceStream);
            if (bitmap == null)
            {
                throw new ApiException(400, "validation_error", "Invalid image upload.");
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            if (data == null)
            {
                throw new ApiException(400, "validation_error", "Invalid image upload.");
            }

            var s3Key = BuildS3Key(request);
            await using var uploadStream = new MemoryStream(data.ToArray());

            var putRequest = new PutObjectRequest
            {
                BucketName = _options.ImageBucket,
                Key = s3Key,
                InputStream = uploadStream,
                ContentType = JpegContentType,
            };

            putRequest.Metadata["userId"] = request.UserId;

            await _client.PutObjectAsync(putRequest);

            return s3Key;
        }
        finally
        {
            bufferedStream?.Dispose();
        }
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(ImageDownloadRequest request)
    {
        GetObjectResponse response;
        try
        {
            response = await _client.GetObjectAsync(_options.ImageBucket, request.S3Key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ApiException(404, "not_found", "Image not found.");
        }

        // dispose the response after the using block
        using (response)
        {
            EnsureUserOwnership(response.Metadata, request.UserId);

            await using var responseStream = response.ResponseStream;
            var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                ? JpegContentType
                : response.Headers.ContentType;
            var fileName = Path.GetFileName(request.S3Key);

            return (memoryStream, contentType, fileName);
        }
    }

    public async Task DeleteAsync(string userId, string s3Key)
    {
        GetObjectMetadataResponse metadataResponse;
        try
        {
            metadataResponse = await _client.GetObjectMetadataAsync(_options.ImageBucket, s3Key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ApiException(404, "not_found", "Image not found.");
        }

        EnsureUserOwnership(metadataResponse.Metadata, userId);

        await _client.DeleteObjectAsync(_options.ImageBucket, s3Key);
    }

    private static string BuildS3Key(ImageUploadRequest request)
    {
        var safeName = Path.GetFileNameWithoutExtension(request.ImageName).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "image";
        }

        return $"{request.UserId}/{request.ItemId}/{safeName}.jpg";
    }

    private static void EnsureUserOwnership(MetadataCollection metadata, string userId)
    {
        var owner = GetMetadataUserId(metadata);
        if (string.IsNullOrWhiteSpace(owner) ||
            !string.Equals(owner, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(403, "forbidden", "You do not have access to this image.");
        }
    }

    private static string? GetMetadataUserId(MetadataCollection metadata)
    {
        foreach (var key in metadata.Keys)
        {
            if (string.Equals(key, "userid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "userId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "x-amz-meta-userid", StringComparison.OrdinalIgnoreCase))
            {
                return metadata[key];
            }
        }

        return null;
    }
}
