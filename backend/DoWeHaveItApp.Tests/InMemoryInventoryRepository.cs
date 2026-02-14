using DoWeHaveItApp.Infrastructure;
using DoWeHaveItApp.Models;
using DoWeHaveItApp.Repositories;

namespace DoWeHaveItApp.Tests;

// Mocked up in-memory implementation of the inventory repository for testing purposes.
public sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly List<Folder> _folders = new();
    private readonly List<Item> _items = new();
    private readonly List<FormTemplate> _templates = new();
    private readonly Tokenizer _tokenizer = new();

    public Task<IReadOnlyList<Folder>> GetFoldersByParentAsync(string userId, string? parentId)
    {
        var results = _folders.Where(folder => string.Equals(folder.ParentId, parentId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<Folder>>(results);
    }

    public Task<IReadOnlyList<Item>> GetItemsByParentAsync(string userId, string? parentId)
    {
        var results = _items.Where(item => string.Equals(item.ParentId, parentId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<Item>>(results);
    }

    public Task<Folder?> GetFolderByIdAsync(string userId, string folderId)
    {
        var folder = _folders.FirstOrDefault(entry => string.Equals(entry.Id, folderId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(folder);
    }

    public Task<Item?> GetItemByIdAsync(string userId, string itemId)
    {
        var item = _items.FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task CreateFolderAsync(string userId, Folder folder)
    {
        _folders.Add(CloneFolder(folder));
        return Task.CompletedTask;
    }

    public Task UpdateFolderAsync(string userId, Folder folder)
    {
        var index = _folders.FindIndex(entry => string.Equals(entry.Id, folder.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _folders[index] = CloneFolder(folder);
        }
        else
        {
            _folders.Add(CloneFolder(folder));
        }

        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(string userId, string? parentId, string folderId)
    {
        _folders.RemoveAll(entry =>
            string.Equals(entry.Id, folderId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.ParentId, parentId, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task CreateItemAsync(string userId, Item item)
    {
        _items.Add(CloneItem(item));
        return Task.CompletedTask;
    }

    public Task UpdateItemAsync(string userId, Item item)
    {
        var index = _items.FindIndex(entry => string.Equals(entry.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _items[index] = CloneItem(item);
        }
        else
        {
            _items.Add(CloneItem(item));
        }

        return Task.CompletedTask;
    }

    public Task DeleteItemAsync(string userId, string? parentId, string itemId)
    {
        _items.RemoveAll(entry =>
            string.Equals(entry.Id, itemId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.ParentId, parentId, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Item>> SearchItemsAsync(string userId, string query)
    {
        var tokens = _tokenizer.Tokenize(query)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Item>>(Array.Empty<Item>());
        }

        var results = _items.Where(item =>
        {
            var itemTokens = _tokenizer.Tokenize(item.Name, item.Comments)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return tokens.All(token => itemTokens.Any(itemToken =>
                itemToken.StartsWith(token, StringComparison.OrdinalIgnoreCase)));
        })
        .Select(CloneItem)
        .ToList();

        return Task.FromResult<IReadOnlyList<Item>>(results);
    }

    public Task<IReadOnlyList<FormTemplate>> GetTemplatesAsync(string userId)
    {
        var results = _templates.Select(CloneTemplate).ToList();
        return Task.FromResult<IReadOnlyList<FormTemplate>>(results);
    }

    public Task<FormTemplate?> GetTemplateAsync(string userId, string templateId)
    {
        var template = _templates.FirstOrDefault(entry => string.Equals(entry.Id, templateId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(template == null ? null : CloneTemplate(template));
    }

    public Task CreateTemplateAsync(string userId, FormTemplate template)
    {
        _templates.Add(CloneTemplate(template));
        return Task.CompletedTask;
    }

    public Task UpdateTemplateAsync(string userId, FormTemplate template)
    {
        var index = _templates.FindIndex(entry => string.Equals(entry.Id, template.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _templates[index] = CloneTemplate(template);
        }
        else
        {
            _templates.Add(CloneTemplate(template));
        }

        return Task.CompletedTask;
    }

    public Task DeleteTemplateAsync(string userId, string templateId)
    {
        _templates.RemoveAll(entry => string.Equals(entry.Id, templateId, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    private static Folder CloneFolder(Folder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        ParentId = folder.ParentId,
        CreatedAt = folder.CreatedAt,
        UpdatedAt = folder.UpdatedAt,
    };

    private static Item CloneItem(Item item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Comments = item.Comments,
        ParentId = item.ParentId,
        Attributes = item.Attributes.Select(attribute => new ItemAttribute
        {
            FieldId = attribute.FieldId,
            FieldName = attribute.FieldName,
            Value = attribute.Value,
        }).ToList(),
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
    };

    private static FormTemplate CloneTemplate(FormTemplate template) => new()
    {
        Id = template.Id,
        Name = template.Name,
        CreatedAt = template.CreatedAt,
        Fields = template.Fields.Select(field => new FormField
        {
            Id = field.Id,
            Name = field.Name,
            Type = field.Type,
            Required = field.Required,
        }).ToList(),
    };
}
