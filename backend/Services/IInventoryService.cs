using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Models;

namespace DoWeHaveItApp.Services;

public interface IInventoryService
{
    Task<FolderContentsResponse> GetFolderContentsAsync(string userId, string? parentId);
    Task<FolderDto> CreateFolderAsync(string userId, CreateFolderRequest request);
    Task<FolderDto> UpdateFolderAsync(string userId, UpdateFolderRequest request);
    Task DeleteFolderAsync(string userId, string folderId);
    Task<ItemDto> CreateItemAsync(CreateItemContext context);
    Task<ItemDto> UpdateItemAsync(
        string userId,
        UpdateItemRequest request,
        string? imageName = null,
        string? imageS3Key = null);
    Task<ItemDto> GetItemAsync(string userId, string itemId);
    // get full item model including internal fields like ImageS3Key
    Task<Item> GetItemModelAsync(string userId, string itemId);
    Task DeleteItemAsync(string userId, string itemId, string? parentId);
}
