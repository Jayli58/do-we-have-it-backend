using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Models;
using DoWeHaveItApp.Repositories;

namespace DoWeHaveItApp.Services;

public sealed class TemplateService : ITemplateService
{
    private readonly IInventoryRepository _repository;

    public TemplateService(IInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FormTemplateDto>> GetTemplatesAsync(string userId)
    {
        var templates = await _repository.GetTemplatesAsync(userId);
        return templates.Select(DtoMapper.ToDto).ToList();
    }

    public async Task<FormTemplateDto> GetTemplateAsync(string userId, string templateId)
    {
        var template = await _repository.GetTemplateAsync(userId, templateId);
        if (template == null)
        {
            throw new ApiException(404, "not_found", "Template not found.");
        }

        return DtoMapper.ToDto(template);
    }

    public async Task<FormTemplateDto> CreateTemplateAsync(string userId, CreateTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "validation_error", "Template name is required.");
        }

        var template = new FormTemplate
        {
            Id = $"tmpl-{Guid.NewGuid():N}",
            Name = request.Name.Trim(),
            Fields = request.Fields.Select(DtoMapper.ToModel).ToList(),
            CreatedAt = DateTime.UtcNow.ToString("O"),
        };

        await _repository.CreateTemplateAsync(userId, template);
        return DtoMapper.ToDto(template);
    }

    public async Task<FormTemplateDto> UpdateTemplateAsync(string userId, UpdateTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiException(400, "validation_error", "Template name is required.");
        }

        var existing = await _repository.GetTemplateAsync(userId, request.Id);
        if (existing == null)
        {
            throw new ApiException(404, "not_found", "Template not found.");
        }

        var updated = new FormTemplate
        {
            Id = existing.Id,
            Name = request.Name.Trim(),
            Fields = request.Fields.Select(DtoMapper.ToModel).ToList(),
            CreatedAt = existing.CreatedAt,
        };

        await _repository.UpdateTemplateAsync(userId, updated);
        return DtoMapper.ToDto(updated);
    }

    public async Task DeleteTemplateAsync(string userId, string templateId)
    {
        var existing = await _repository.GetTemplateAsync(userId, templateId);
        if (existing == null)
        {
            throw new ApiException(404, "not_found", "Template not found.");
        }

        await _repository.DeleteTemplateAsync(userId, templateId);
    }
}
