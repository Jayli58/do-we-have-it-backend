using DoWeHaveItApp.Dtos;

namespace DoWeHaveItApp.Services;

public interface IInventoryService
{
    Task<FolderContentsResponse> GetFolderContentsAsync(string userId, string? parentId);
    Task<FolderDto> CreateFolderAsync(string userId, CreateFolderRequest request);
    Task<FolderDto> UpdateFolderAsync(string userId, UpdateFolderRequest request);
    Task DeleteFolderAsync(string userId, string folderId);
    Task<ItemDto> CreateItemAsync(string userId, CreateItemRequest request);
    Task<ItemDto> UpdateItemAsync(string userId, UpdateItemRequest request);
    Task<ItemDto> GetItemAsync(string userId, string itemId);
    Task DeleteItemAsync(string userId, string itemId, string? parentId);
}
