using DoWeHaveItApp.Dtos;

namespace DoWeHaveItApp.Services;

public interface ITemplateService
{
    Task<IReadOnlyList<FormTemplateDto>> GetTemplatesAsync(string userId);
    Task<FormTemplateDto> GetTemplateAsync(string userId, string templateId);
    Task<FormTemplateDto> CreateTemplateAsync(string userId, CreateTemplateRequest request);
    Task<FormTemplateDto> UpdateTemplateAsync(string userId, UpdateTemplateRequest request);
    Task DeleteTemplateAsync(string userId, string templateId);
}
