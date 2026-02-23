using Amazon.S3;
using Amazon.S3.Model;
using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Services;
using Microsoft.Extensions.Options;
using Moq;
using SkiaSharp;
using System.Net;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class S3ImageServiceTests
{
    private const string BucketName = "test-bucket";

    [Fact]
    public async Task UploadAsync_StoresJpegWithMetadata()
    {
        var client = new Mock<IAmazonS3>();
        PutObjectRequest? captured = null;

        // stub PutObjectAsync to capture the request for later assertions
        client.Setup(s3 => s3.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(client.Object);
        var stream = CreateImageStream();

        var s3Key = await service.UploadAsync(new ImageUploadRequest
        {
            UserId = "user-1",
            ItemId = "item-1",
            ImageName = "photo",
            ImageStream = stream,
            ContentType = "image/png",
        });

        Assert.Equal("user-1/item-1/photo.jpg", s3Key);
        Assert.NotNull(captured);
        Assert.Equal(BucketName, captured!.BucketName);
        Assert.Equal(s3Key, captured.Key);
        Assert.Equal("image/jpeg", captured.ContentType);
        Assert.Equal("user-1", captured.Metadata["userId"]);
    }

    [Fact]
    public async Task UploadAsync_RejectsOversizedImages()
    {
        var client = new Mock<IAmazonS3>();
        var service = CreateService(client.Object);
        var oversized = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.UploadAsync(new ImageUploadRequest
        {
            UserId = "user-1",
            ItemId = "item-1",
            ImageName = "photo",
            ImageStream = oversized,
            ContentType = "image/png",
        }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("validation_error", exception.Code);
    }

    [Fact]
    public async Task UploadAsync_BuffersNonSeekableStreams()
    {
        var client = new Mock<IAmazonS3>();
        PutObjectRequest? captured = null;

        client.Setup(s3 => s3.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(client.Object);
        var stream = new NonSeekableStream(CreateImageStream());

        var s3Key = await service.UploadAsync(new ImageUploadRequest
        {
            UserId = "user-2",
            ItemId = "item-2",
            ImageName = "photo",
            ImageStream = stream,
            ContentType = "image/png",
        });

        Assert.Equal("user-2/item-2/photo.jpg", s3Key);
        Assert.NotNull(captured);
        Assert.Equal("user-2", captured!.Metadata["userId"]);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsStreamWhenOwnerMatches()
    {
        var client = new Mock<IAmazonS3>();
        var response = new GetObjectResponse
        {
            ResponseStream = new MemoryStream(new byte[] { 1, 2, 3 }),
        };
        response.Metadata["userId"] = "user-1";
        response.Headers.ContentType = "image/jpeg";

        client.Setup(s3 => s3.GetObjectAsync(BucketName, "user-1/item-1/photo.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var service = CreateService(client.Object);

        var result = await service.DownloadAsync(new ImageDownloadRequest
        {
            UserId = "user-1",
            ItemId = "item-1",
            S3Key = "user-1/item-1/photo.jpg",
        });

        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal("photo.jpg", result.FileName);
        Assert.Equal(3, result.Stream.Length);
    }

    [Fact]
    public async Task DownloadAsync_RejectsMismatchedOwner()
    {
        var client = new Mock<IAmazonS3>();
        var response = new GetObjectResponse
        {
            ResponseStream = new MemoryStream(new byte[] { 1, 2, 3 }),
        };
        response.Metadata["userId"] = "other-user";

        client.Setup(s3 => s3.GetObjectAsync(BucketName, "user-1/item-1/photo.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var service = CreateService(client.Object);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.DownloadAsync(new ImageDownloadRequest
        {
            UserId = "user-1",
            ItemId = "item-1",
            S3Key = "user-1/item-1/photo.jpg",
        }));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("forbidden", exception.Code);
    }

    [Fact]
    public async Task DeleteAsync_DeletesWhenOwnerMatches()
    {
        var client = new Mock<IAmazonS3>();
        var metadata = new GetObjectMetadataResponse();
        metadata.Metadata["userId"] = "user-1";

        client.Setup(s3 => s3.GetObjectMetadataAsync(BucketName, "user-1/item-1/photo.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        client.Setup(s3 => s3.DeleteObjectAsync(BucketName, "user-1/item-1/photo.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        var service = CreateService(client.Object);

        await service.DeleteAsync("user-1", "user-1/item-1/photo.jpg");

        client.Verify(s3 => s3.DeleteObjectAsync(BucketName, "user-1/item-1/photo.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_ThrowsNotFoundWhenMissing()
    {
        var client = new Mock<IAmazonS3>();

        client.Setup(s3 => s3.GetObjectAsync(BucketName, "missing.jpg", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });

        var service = CreateService(client.Object);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.DownloadAsync(new ImageDownloadRequest
        {
            UserId = "user-1",
            ItemId = "item-1",
            S3Key = "missing.jpg",
        }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("not_found", exception.Code);
    }

    private static S3ImageService CreateService(IAmazonS3 client)
    {
        var options = Options.Create(new S3Options
        {
            ImageBucket = BucketName,
            Region = "ap-southeast-2",
            UseLocal = true,
            ServiceUrl = "http://localhost:4566",
        });

        return new S3ImageService(client, options);
    }

    private static Stream CreateImageStream()
    {
        using var bitmap = new SKBitmap(1, 1);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        return new MemoryStream(data.ToArray());
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => _inner.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
