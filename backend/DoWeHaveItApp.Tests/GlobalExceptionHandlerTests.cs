using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Exceptions;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class GlobalExceptionHandlerTests : IClassFixture<ExceptionHandlerApiFactory>
{
    private readonly HttpClient _client;

    public GlobalExceptionHandlerTests(ExceptionHandlerApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_WhenBaseExceptionThrown_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/items/search?query=milk");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("Search failed.", root.GetProperty("title").GetString());
        Assert.Equal((int)HttpStatusCode.BadRequest, root.GetProperty("status").GetInt32());
        Assert.Equal("/items/search", root.GetProperty("instance").GetString());
    }
}

public sealed class ExceptionHandlerApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISearchService>();
            services.RemoveAll<IInventoryService>();
            services.AddSingleton<ISearchService, ThrowingSearchService>();
            services.AddSingleton<IInventoryService, StubInventoryService>();
        });
    }
}

public sealed class ThrowingSearchService : ISearchService
{
    public Task<SearchResultDto> SearchItemsAsync(string userId, string query)
    {
        throw new BaseException("Search failed.", HttpStatusCode.BadRequest);
    }
}

public sealed class StubInventoryService : IInventoryService
{
    public Task<FolderContentsResponse> GetFolderContentsAsync(string userId, string? parentId)
    {
        throw new NotImplementedException();
    }

    public Task<FolderDto> CreateFolderAsync(string userId, CreateFolderRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<FolderDto> UpdateFolderAsync(string userId, UpdateFolderRequest request)
    {
        throw new NotImplementedException();
    }

    public Task DeleteFolderAsync(string userId, string folderId)
    {
        throw new NotImplementedException();
    }

    public Task<ItemDto> CreateItemAsync(string userId, CreateItemRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<ItemDto> UpdateItemAsync(string userId, UpdateItemRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<ItemDto> GetItemAsync(string userId, string itemId)
    {
        throw new NotImplementedException();
    }

    public Task DeleteItemAsync(string userId, string itemId, string? parentId)
    {
        throw new NotImplementedException();
    }
}
