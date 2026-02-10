using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class TemplateServiceTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task CreateAndUpdateTemplateAsync_RoundTrips()
    {
        var repository = new InMemoryInventoryRepository();
        var service = new TemplateService(repository);

        var created = await service.CreateTemplateAsync(UserId, new CreateTemplateRequest
        {
            Name = "Appliance Details",
            Fields = new List<FormFieldDto>
            {
                new()
                {
                    Id = "field-model",
                    Name = "Model",
                    Type = "text",
                    Required = true,
                },
            },
        });

        var updated = await service.UpdateTemplateAsync(UserId, new UpdateTemplateRequest
        {
            Id = created.Id,
            Name = "Updated",
            CreatedAt = created.CreatedAt,
            Fields = created.Fields,
        });

        Assert.Equal("Updated", updated.Name);
        var fetched = await service.GetTemplateAsync(UserId, created.Id);
        Assert.Equal("Updated", fetched.Name);
    }

    [Fact]
    public async Task DeleteTemplateAsync_RemovesTemplate()
    {
        var repository = new InMemoryInventoryRepository();
        var service = new TemplateService(repository);

        var created = await service.CreateTemplateAsync(UserId, new CreateTemplateRequest
        {
            Name = "Tool Specs",
            Fields = new List<FormFieldDto>(),
        });

        await service.DeleteTemplateAsync(UserId, created.Id);

        var templates = await service.GetTemplatesAsync(UserId);
        Assert.Empty(templates);
    }
}
