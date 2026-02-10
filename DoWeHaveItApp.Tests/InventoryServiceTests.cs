using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using System.Collections.Generic;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class InventoryServiceTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task CreateFolderAsync_RejectsDuplicateNames()
    {
        var repository = new InMemoryInventoryRepository();
        var service = new InventoryService(repository);

        await service.CreateFolderAsync(UserId, new CreateFolderRequest
        {
            Name = "Kitchen",
            ParentId = null,
        });

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.CreateFolderAsync(UserId, new CreateFolderRequest
        {
            Name = "Kitchen",
            ParentId = null,
        }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("conflict", exception.Code);
    }

    [Fact]
    public async Task UpdateItemAsync_UpdatesFields()
    {
        var repository = new InMemoryInventoryRepository();
        var service = new InventoryService(repository);

        var created = await service.CreateItemAsync(UserId, new CreateItemRequest
        {
            Name = "Coffee Maker",
            Comments = "Top shelf",
            ParentId = "folder-a",
            Attributes = new List<ItemAttributeDto>(),
        });

        var updated = await service.UpdateItemAsync(UserId, new UpdateItemRequest
        {
            Id = created.Id,
            Name = "Coffee Maker Deluxe",
            Comments = "Bottom shelf",
            ParentId = created.ParentId,
            Attributes = new List<ItemAttributeDto>
            {
                new()
                {
                    FieldId = "field-serial",
                    FieldName = "Serial",
                    Value = "ABC-123",
                },
            },
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt,
        });

        var parentItems = await repository.GetItemsByParentAsync(UserId, created.ParentId);

        Assert.Single(parentItems);
        Assert.Equal(updated.Id, parentItems[0].Id);
        Assert.Equal("Coffee Maker Deluxe", parentItems[0].Name);
        Assert.Equal("Bottom shelf", parentItems[0].Comments);
        Assert.Single(parentItems[0].Attributes);
    }

    [Fact]
    public async Task DeleteFolderAsync_CascadesToChildren()
    {
        var repository = new InMemoryInventoryRepository();
        var service = new InventoryService(repository);

        var parent = await service.CreateFolderAsync(UserId, new CreateFolderRequest
        {
            Name = "Kitchen",
            ParentId = null,
        });

        var child = await service.CreateFolderAsync(UserId, new CreateFolderRequest
        {
            Name = "Appliances",
            ParentId = parent.Id,
        });

        await service.CreateItemAsync(UserId, new CreateItemRequest
        {
            Name = "Toaster",
            Comments = "Bottom shelf",
            ParentId = parent.Id,
            Attributes = new List<ItemAttributeDto>(),
        });

        await service.CreateItemAsync(UserId, new CreateItemRequest
        {
            Name = "Blender",
            Comments = "Top shelf",
            ParentId = child.Id,
            Attributes = new List<ItemAttributeDto>(),
        });

        await service.DeleteFolderAsync(UserId, parent.Id);

        var remainingParent = await repository.GetFolderByIdAsync(UserId, parent.Id);
        var remainingChild = await repository.GetFolderByIdAsync(UserId, child.Id);
        var parentItems = await repository.GetItemsByParentAsync(UserId, parent.Id);
        var childItems = await repository.GetItemsByParentAsync(UserId, child.Id);

        Assert.Null(remainingParent);
        Assert.Null(remainingChild);
        Assert.Empty(parentItems);
        Assert.Empty(childItems);
    }
}
