using DoWeHaveItApp.Controllers;
using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Models;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class ItemsControllerTests
{
    [Fact]
    public async Task Search_ReturnsOkWithResults()
    {
        var expected = new SearchResultDto
        {
            Items = new[]
            {
                new ItemDto
                {
                    Id = "item-1",
                    Name = "Milk",
                    Comments = string.Empty,
                    ParentId = null,
                    Attributes = new[]
                    {
                        new ItemAttributeDto
                        {
                            FieldId = "field-1",
                            FieldName = "Brand",
                            Value = "Acme",
                        },
                    },
                    CreatedAt = "2024-01-01T00:00:00Z",
                    UpdatedAt = "2024-01-02T00:00:00Z",
                },
            },
        };

        var searchService = new FakeSearchService(expected);
        var controller = CreateController(new ThrowingInventoryService(), searchService, new ThrowingImageService(), "test-user");

        var response = await controller.Search("milk");

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SearchResultDto>(okResult.Value);
        Assert.Same(expected, payload);
        Assert.Equal("test-user", searchService.CapturedUserId);
        Assert.Equal("milk", searchService.CapturedQuery);
    }

    [Fact]
    public async Task Delete_IgnoresImageDeletionFailures()
    {
        var inventoryService = new FakeInventoryService(new Item
        {
            Id = "item-1",
            Name = "Kettle",
            Comments = string.Empty,
            ParentId = "folder-1",
            Attributes = new List<ItemAttribute>(),
            CreatedAt = "2026-02-10T00:00:00Z",
            UpdatedAt = "2026-02-10T00:00:00Z",
            ImageS3Key = "user-1/item-1/photo.jpg",
        });
        var imageService = new ThrowingDeleteImageService();
        var controller = CreateController(inventoryService, new NoopSearchService(), imageService, "user-1");

        var result = await controller.Delete("item-1", "folder-1");

        Assert.IsType<NoContentResult>(result);
        Assert.True(inventoryService.DeleteCalled);
        Assert.Equal("user-1", inventoryService.DeleteUserId);
        Assert.Equal("item-1", inventoryService.DeleteItemId);
        Assert.Equal("folder-1", inventoryService.DeleteParentId);
        Assert.True(imageService.DeleteCalled);
    }

    private static ItemsController CreateController(
        IInventoryService inventoryService,
        ISearchService searchService,
        IImageService imageService,
        string userId)
    {
        var controller = new ItemsController(inventoryService, searchService, imageService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        controller.HttpContext.Request.Headers["X-User-Id"] = userId;
        return controller;
    }

    private sealed class FakeSearchService : ISearchService
    {
        public string? CapturedUserId { get; private set; }
        public string? CapturedQuery { get; private set; }
        private readonly SearchResultDto _result;

        public FakeSearchService(SearchResultDto result)
        {
            _result = result;
        }

        public Task<SearchResultDto> SearchItemsAsync(string userId, string query)
        {
            CapturedUserId = userId;
            CapturedQuery = query;
            return Task.FromResult(_result);
        }
    }

    private sealed class NoopSearchService : ISearchService
    {
        public Task<SearchResultDto> SearchItemsAsync(string userId, string query)
            => Task.FromResult(new SearchResultDto { Items = Array.Empty<ItemDto>() });
    }

    private sealed class FakeInventoryService : IInventoryService
    {
        private readonly Item _item;

        public FakeInventoryService(Item item)
        {
            _item = item;
        }

        public bool DeleteCalled { get; private set; }
        public string? DeleteUserId { get; private set; }
        public string? DeleteItemId { get; private set; }
        public string? DeleteParentId { get; private set; }

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

        public Task<ItemDto> CreateItemAsync(CreateItemContext context)
        {
            throw new NotImplementedException();
        }

        public Task<ItemDto> UpdateItemAsync(string userId, UpdateItemRequest request, string? imageName, string? imageS3Key)
        {
            throw new NotImplementedException();
        }

        public Task<ItemDto> GetItemAsync(string userId, string itemId)
        {
            throw new NotImplementedException();
        }

        public Task<Item> GetItemModelAsync(string userId, string itemId)
        {
            return Task.FromResult(_item);
        }

        public Task DeleteItemAsync(string userId, string itemId, string? parentId)
        {
            DeleteCalled = true;
            DeleteUserId = userId;
            DeleteItemId = itemId;
            DeleteParentId = parentId;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingInventoryService : IInventoryService
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

        public Task<ItemDto> CreateItemAsync(CreateItemContext context)
        {
            throw new NotImplementedException();
        }

        public Task<ItemDto> UpdateItemAsync(string userId, UpdateItemRequest request, string? imageName, string? imageS3Key)
        {
            throw new NotImplementedException();
        }

        public Task<ItemDto> GetItemAsync(string userId, string itemId)
        {
            throw new NotImplementedException();
        }

        public Task<DoWeHaveItApp.Models.Item> GetItemModelAsync(string userId, string itemId)
        {
            throw new NotImplementedException();
        }

        public Task DeleteItemAsync(string userId, string itemId, string? parentId)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class ThrowingImageService : IImageService
    {
        public Task<string> UploadAsync(ImageUploadRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(ImageDownloadRequest request)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(string userId, string s3Key)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class ThrowingDeleteImageService : IImageService
    {
        public bool DeleteCalled { get; private set; }

        public Task<string> UploadAsync(ImageUploadRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(ImageDownloadRequest request)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(string userId, string s3Key)
        {
            DeleteCalled = true;
            throw new ApiException(404, "not_found", "Image not found.");
        }
    }
}
