using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class SearchServiceTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task SearchItemsAsync_ReturnsMatchingItemsByParent()
    {
        var repository = new InMemoryInventoryRepository();
        var inventoryService = new InventoryService(repository);
        var searchService = new SearchService(repository);

        await inventoryService.CreateItemAsync(UserId, new CreateItemRequest
        {
            Name = "Coffee Maker",
            Comments = "Top shelf",
            ParentId = "kitchen",
            Attributes = new List<ItemAttributeDto>(),
        });

        await inventoryService.CreateItemAsync(UserId, new CreateItemRequest
        {
            Name = "Cordless Drill",
            Comments = "Garage drawer",
            ParentId = "garage",
            Attributes = new List<ItemAttributeDto>(),
        });

        var results = await searchService.SearchItemsAsync(UserId, "kitchen", "coffee maker");

        Assert.Single(results.Items);
        Assert.Equal("Coffee Maker", results.Items[0].Name);
    }

    [Fact]
    public async Task SearchItemsAsync_ReturnsEmptyForMisspelledQuery()
    {
        var repository = new InMemoryInventoryRepository();
        var inventoryService = new InventoryService(repository);
        var searchService = new SearchService(repository);

        await inventoryService.CreateItemAsync(UserId, new CreateItemRequest
        {
            Name = "Coffee Maker",
            Comments = "Top shelf",
            ParentId = "kitchen",
            Attributes = new List<ItemAttributeDto>(),
        });

        var results = await searchService.SearchItemsAsync(UserId, "kitchen", "coffee makerr");

        Assert.Empty(results.Items);
    }

    [Fact]
    public async Task SearchItemsAsync_ReturnsEmptyForBlankQuery()
    {
        var repository = new InMemoryInventoryRepository();
        var searchService = new SearchService(repository);

        var results = await searchService.SearchItemsAsync(UserId, "kitchen", " ");

        Assert.Empty(results.Items);
    }
}
