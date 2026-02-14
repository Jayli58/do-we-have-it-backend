using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Models;
using DoWeHaveItApp.Repositories;

namespace DoWeHaveItApp.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;

    public InventoryService(IInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<FolderContentsResponse> GetFolderContentsAsync(string userId, string? parentId)
    {
        var folders = await _repository.GetFoldersByParentAsync(userId, parentId);
        var items = await _repository.GetItemsByParentAsync(userId, parentId);

        var folderDtos = folders
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .Select(DtoMapper.ToDto)
            .ToList();
        var itemDtos = items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(DtoMapper.ToDto)
            .ToList();

        return new FolderContentsResponse
        {
            Folders = folderDtos,
            Items = itemDtos,
        };
    }

    public async Task<FolderDto> CreateFolderAsync(string userId, CreateFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "validation_error", "Folder name is required.");
        }

        var siblings = await _repository.GetFoldersByParentAsync(userId, request.ParentId);
        if (siblings.Any(folder => string.Equals(folder.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(409, "conflict", "Folder name must be unique within the parent.");
        }

        var timestamp = DateTime.UtcNow.ToString("O");
        var folder = new Folder
        {
            Id = $"folder-{Guid.NewGuid():N}",
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };

        await _repository.CreateFolderAsync(userId, folder);
        return DtoMapper.ToDto(folder);
    }

    public async Task<FolderDto> UpdateFolderAsync(string userId, UpdateFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "validation_error", "Folder name is required.");
        }

        var existing = await _repository.GetFolderByIdAsync(userId, request.Id);
        if (existing == null)
        {
            throw new ApiException(404, "not_found", "Folder not found.");
        }

        var parentId = request.ParentId;
        var siblings = await _repository.GetFoldersByParentAsync(userId, parentId);
        if (siblings.Any(folder =>
                !string.Equals(folder.Id, existing.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(folder.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(409, "conflict", "Folder name must be unique within the parent.");
        }

        var updated = new Folder
        {
            Id = existing.Id,
            Name = request.Name.Trim(),
            ParentId = parentId,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTime.UtcNow.ToString("O"),
        };

        var existingParent = existing.ParentId ?? string.Empty;
        var updatedParent = parentId ?? string.Empty;
        if (!string.Equals(existingParent, updatedParent, StringComparison.OrdinalIgnoreCase))
        {
            await _repository.DeleteFolderAsync(userId, existing.ParentId, existing.Id);
        }

        await _repository.UpdateFolderAsync(userId, updated);
        return DtoMapper.ToDto(updated);
    }

    public async Task DeleteFolderAsync(string userId, string folderId)
    {
        var existing = await _repository.GetFolderByIdAsync(userId, folderId);
        if (existing == null)
        {
            throw new ApiException(404, "not_found", "Folder not found.");
        }

        await DeleteFolderRecursiveAsync(userId, existing);
    }

    public async Task<ItemDto> CreateItemAsync(string userId, CreateItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "validation_error", "Item name is required.");
        }

        var timestamp = DateTime.UtcNow.ToString("O");
        var item = new Item
        {
            Id = $"item-{Guid.NewGuid():N}",
            Name = request.Name.Trim(),
            Comments = request.Comments?.Trim() ?? string.Empty,
            ParentId = request.ParentId,
            Attributes = request.Attributes.Select(DtoMapper.ToModel).ToList(),
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };

        await _repository.CreateItemAsync(userId, item);
        return DtoMapper.ToDto(item);
    }

    public async Task<ItemDto> UpdateItemAsync(string userId, UpdateItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "validation_error", "Item name is required.");
        }

        var existing = await _repository.GetItemByIdAsync(userId, request.Id);
        if (existing == null)
        {
            throw new ApiException(404, "not_found", "Item not found.");
        }

        var updated = new Item
        {
            Id = existing.Id,
            Name = request.Name.Trim(),
            Comments = request.Comments?.Trim() ?? string.Empty,
            ParentId = request.ParentId,
            Attributes = request.Attributes.Select(DtoMapper.ToModel).ToList(),
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTime.UtcNow.ToString("O"),
        };

        await _repository.UpdateItemAsync(userId, updated);

        return DtoMapper.ToDto(updated);
    }

    public async Task<ItemDto> GetItemAsync(string userId, string itemId)
    {
        var item = await _repository.GetItemByIdAsync(userId, itemId);
        if (item == null)
        {
            throw new ApiException(404, "not_found", "Item not found.");
        }

        return DtoMapper.ToDto(item);
    }

    public async Task DeleteItemAsync(string userId, string itemId, string? parentId)
    {
        var actualParentId = parentId;
        if (string.IsNullOrWhiteSpace(actualParentId))
        {
            var existing = await _repository.GetItemByIdAsync(userId, itemId);
            if (existing == null)
            {
                throw new ApiException(404, "not_found", "Item not found.");
            }

            actualParentId = existing.ParentId;
        }

        await _repository.DeleteItemAsync(userId, actualParentId, itemId);
    }

    private async Task DeleteFolderRecursiveAsync(string userId, Folder folder)
    {
        var childFolders = await _repository.GetFoldersByParentAsync(userId, folder.Id);
        foreach (var child in childFolders)
        {
            await DeleteFolderRecursiveAsync(userId, child);
        }

        var items = await _repository.GetItemsByParentAsync(userId, folder.Id);
        foreach (var item in items)
        {
            await _repository.DeleteItemAsync(userId, folder.Id, item.Id);
        }

        await _repository.DeleteFolderAsync(userId, folder.ParentId, folder.Id);
    }
}
