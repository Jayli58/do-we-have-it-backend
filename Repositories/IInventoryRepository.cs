using DoWeHaveItApp.Models;

namespace DoWeHaveItApp.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<Folder>> GetFoldersByParentAsync(string userId, string? parentId);
    Task<IReadOnlyList<Item>> GetItemsByParentAsync(string userId, string? parentId);
    Task<Folder?> GetFolderByIdAsync(string userId, string folderId);
    Task<Item?> GetItemByIdAsync(string userId, string itemId);
    Task CreateFolderAsync(string userId, Folder folder);
    Task UpdateFolderAsync(string userId, Folder folder);
    Task DeleteFolderAsync(string userId, string? parentId, string folderId);
    Task CreateItemAsync(string userId, Item item);
    Task UpdateItemAsync(string userId, Item item);
    Task DeleteItemAsync(string userId, string? parentId, string itemId);
    Task<IReadOnlyList<Item>> SearchItemsAsync(string userId, string? parentId, string query);
    Task<IReadOnlyList<FormTemplate>> GetTemplatesAsync(string userId);
    Task<FormTemplate?> GetTemplateAsync(string userId, string templateId);
    Task CreateTemplateAsync(string userId, FormTemplate template);
    Task UpdateTemplateAsync(string userId, FormTemplate template);
    Task DeleteTemplateAsync(string userId, string templateId);
}
