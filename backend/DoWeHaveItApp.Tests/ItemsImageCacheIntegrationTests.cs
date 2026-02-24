using DoWeHaveItApp.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class ItemsImageCacheIntegrationTests : IClassFixture<DynamoDbFixture>, IClassFixture<S3ImageFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetImage_ReturnsCacheHeadersAndSupportsEtagRevalidation()
    {
        var userId = $"test-user-{Guid.NewGuid():N}";
        using var factory = new DynamoS3ItemsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);

        var imageBytes = CreatePngBytes();
        using var form = new MultipartFormDataContent
        {
            { new StringContent("item1"), "name" },
            { new StringContent(string.Empty), "comments" },
            { new StringContent("[]", Encoding.UTF8, "application/json"), "attributes" },
            { new ByteArrayContent(imageBytes)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") }
            }, "image", "photo.png" },
        };

        var createResponse = await client.PostAsync("/items", form);
        createResponse.EnsureSuccessStatusCode();

        var createBody = await createResponse.Content.ReadAsStringAsync();
        using var createJson = JsonDocument.Parse(createBody);
        var itemId = createJson.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(itemId));

        var imageResponse = await client.GetAsync($"/items/{itemId}/img");
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.True(imageResponse.Headers.TryGetValues("Cache-Control", out var cacheControlValues));
        var cacheControl = string.Join(",", cacheControlValues).ToLowerInvariant();
        Assert.Contains("private", cacheControl);
        Assert.Contains("max-age=0", cacheControl);
        Assert.Contains("must-revalidate", cacheControl);
        Assert.True(imageResponse.Headers.TryGetValues("ETag", out var etagValues));
        var etag = etagValues.FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(etag));

        using var revalidateRequest = new HttpRequestMessage(HttpMethod.Get, $"/items/{itemId}/img");
        // add etag to request headers to revalidate
        revalidateRequest.Headers.IfNoneMatch.ParseAdd(etag!);
        var revalidateResponse = await client.SendAsync(revalidateRequest);
        Assert.Equal(HttpStatusCode.NotModified, revalidateResponse.StatusCode);
    }

    private static byte[] CreatePngBytes()
    {
        using var image = new Image<Rgba32>(1, 1);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}

// mini test host for integration tests; run api in memory with test client
public sealed class DynamoS3ItemsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DynamoDb:TableName"] = DynamoDbFixture.InventoryTable,
                ["DynamoDb:Region"] = DynamoDbFixture.Region,
                ["DynamoDb:UseLocal"] = "true",
                ["DynamoDb:ServiceUrl"] = DynamoDbFixture.ServiceUrl,
                ["S3:ImageBucket"] = S3ImageFixture.BucketNameConst,
                ["S3:Region"] = S3ImageFixture.Region,
                ["S3:UseLocal"] = "true",
                ["S3:ServiceUrl"] = S3ImageFixture.ServiceUrl,
            });
        });
    }
}
