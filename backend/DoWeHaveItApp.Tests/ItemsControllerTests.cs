using DoWeHaveItApp.Controllers;
using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        var controller = CreateController(searchService, "test-user");

        var response = await controller.Search("milk");

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SearchResultDto>(okResult.Value);
        Assert.Same(expected, payload);
        Assert.Equal("test-user", searchService.CapturedUserId);
        Assert.Equal("milk", searchService.CapturedQuery);
    }

    private static ItemsController CreateController(ISearchService searchService, string userId)
    {
        var controller = new ItemsController(new ThrowingInventoryService(), searchService, new ThrowingImageService())
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
}
