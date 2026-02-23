using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DoWeHaveItApp.Tests;

// In-memory repository tests
public sealed class ItemsApiTests : IClassFixture<ItemsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    // In-memory HttpClient
    public ItemsApiTests(ItemsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateItem_ReturnsAttributes()
    {
        var attributes = new[]
        {
            new { fieldId = "field-mlhlmsvk-bvl4", fieldName = "f1", value = "f1" },
            new { fieldId = "field-mlhlmynm-46gi", fieldName = "f2", value = "f2" },
            new { fieldId = "field-mlhln0jf-ppco", fieldName = "f3", value = "f3" },
        };

        using var form = new MultipartFormDataContent
        {
            { new StringContent("item1"), "name" },
            { new StringContent(string.Empty), "comments" },
            { new StringContent(JsonSerializer.Serialize(attributes, JsonOptions), Encoding.UTF8, "application/json"), "attributes" },
        };

        var response = await _client.PostAsync("/items", form);

        response.EnsureSuccessStatusCode();

        var item = await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        Assert.NotNull(item);
        Assert.NotNull(item!.Attributes);
        Assert.Equal(3, item.Attributes.Count);
        Assert.Equal("field-mlhlmsvk-bvl4", item.Attributes[0].FieldId);
        Assert.Equal("f1", item.Attributes[0].FieldName);
        Assert.Equal("f1", item.Attributes[0].Value);
    }
}

public sealed class ItemsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IInventoryRepository>();
            services.AddSingleton<IInventoryRepository, InMemoryInventoryRepository>();
        });
    }
}
