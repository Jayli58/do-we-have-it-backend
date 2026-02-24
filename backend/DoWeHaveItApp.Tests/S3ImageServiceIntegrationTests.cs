using Amazon.S3;
using Amazon.S3.Model;
using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Services;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class S3ImageServiceIntegrationTests : IClassFixture<S3ImageFixture>
{
    private readonly S3ImageFixture _fixture;

    public S3ImageServiceIntegrationTests(S3ImageFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UploadDownloadDelete_RoundTrip()
    {
        var itemId = $"item-{Guid.NewGuid():N}";
        var imageStream = CreateImageStream();

        var s3Key = await _fixture.Service.UploadAsync(new ImageUploadRequest
        {
            UserId = _fixture.UserId,
            ItemId = itemId,
            ImageName = "sample",
            ImageStream = imageStream,
            ContentType = "image/png",
        });

        var download = await _fixture.Service.DownloadAsync(new ImageDownloadRequest
        {
            UserId = _fixture.UserId,
            ItemId = itemId,
            S3Key = s3Key,
        });

        Assert.Equal("image/jpeg", download.ContentType);
        Assert.Equal("sample.jpg", download.FileName);
        Assert.True(download.Stream.Length > 0);

        await _fixture.Service.DeleteAsync(_fixture.UserId, s3Key);

        var exception = await Assert.ThrowsAsync<ApiException>(() => _fixture.Service.DownloadAsync(new ImageDownloadRequest
        {
            UserId = _fixture.UserId,
            ItemId = itemId,
            S3Key = s3Key,
        }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("not_found", exception.Code);
    }

    [Fact]
    public async Task DownloadAsync_RejectsWrongOwner()
    {
        var itemId = $"item-{Guid.NewGuid():N}";
        var imageStream = CreateImageStream();

        var s3Key = await _fixture.Service.UploadAsync(new ImageUploadRequest
        {
            UserId = _fixture.UserId,
            ItemId = itemId,
            ImageName = "owner-check",
            ImageStream = imageStream,
            ContentType = "image/png",
        });

        try
        {
            var exception = await Assert.ThrowsAsync<ApiException>(() => _fixture.Service.DownloadAsync(new ImageDownloadRequest
            {
                UserId = "other-user",
                ItemId = itemId,
                S3Key = s3Key,
            }));

            Assert.Equal(403, exception.StatusCode);
            Assert.Equal("forbidden", exception.Code);
        }
        finally
        {
            await _fixture.Client.DeleteObjectAsync(_fixture.BucketName, s3Key);
        }
    }

    private static Stream CreateImageStream()
    {
        using var image = new Image<Rgba32>(1, 1);
        var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }
}

public sealed class S3ImageFixture : IAsyncLifetime
{
    public const string BucketNameConst = "dwhi-images";
    public const string Region = "ap-southeast-2";
    public const string ServiceUrl = "http://localhost:4566";

    public IAmazonS3 Client { get; private set; } = null!;
    public S3ImageService Service { get; private set; } = null!;
    public string BucketName => BucketNameConst;
    public string UserId => "user-1";

    // part of the xUnit IAsyncLifetime fixture. It runs once before any tests that use the fixture.
    public async Task InitializeAsync()
    {
        var options = new S3Options
        {
            ImageBucket = BucketNameConst,
            Region = Region,
            UseLocal = true,
            ServiceUrl = ServiceUrl,
        };

        Client = S3ClientFactory.Create(options);
        Service = new S3ImageService(Client, Options.Create(options));

        try
        {
            await Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = BucketNameConst,
                BucketRegion = S3Region.APSoutheast2,
            });
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == HttpStatusCode.Conflict ||
            string.Equals(ex.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ex.ErrorCode, "BucketAlreadyExists", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
