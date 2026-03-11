using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Models;
using DoWeHaveItApp.Repositories;

namespace DoWeHaveItApp.Services;

public sealed class SearchService : ISearchService
{
    private readonly IInventoryRepository _repository;

    public SearchService(IInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<SearchResultDto> SearchItemsAsync(string userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResultDto { Items = Array.Empty<ItemDto>() };
        }

        var items = await _repository.SearchItemsAsync(userId, query);
        // single request cache to avoid multiple database queries
        var folderCache = new Dictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
        var itemDtos = new List<ItemDto>();

        foreach (var item in items)
        {
            var parentPath = await ResolveParentPathAsync(userId, item.ParentId, folderCache);
            itemDtos.Add(DtoMapper.ToDto(item, parentPath));
        }

        return new SearchResultDto { Items = itemDtos };
    }

    private async Task<string?> ResolveParentPathAsync(
        string userId,
        string? parentId,
        Dictionary<string, Folder> folderCache)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return "root";
        }

        var parents = new List<string>();
        var currentId = parentId;

        while (!string.IsNullOrWhiteSpace(currentId))
        {
            var folder = await GetFolderAsync(userId, currentId, folderCache);
            if (folder == null)
            {
                break;
            }

            parents.Add(folder.Name);
            currentId = folder.ParentId;
        }

        if (parents.Count == 0)
        {
            return null;
        }

        parents.Reverse();
        return string.Join(" > ", parents);
    }

    private async Task<Folder?> GetFolderAsync(
        string userId,
        string folderId,
        Dictionary<string, Folder> folderCache)
    {
        if (folderCache.TryGetValue(folderId, out var cached))
        {
            return cached;
        }

        var folder = await _repository.GetFolderByIdAsync(userId, folderId);
        if (folder != null)
        {
            folderCache[folderId] = folder;
        }

        return folder;
    }
}
