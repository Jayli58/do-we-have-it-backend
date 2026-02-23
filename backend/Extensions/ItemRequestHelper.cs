using DoWeHaveItApp.Dtos;
using DoWeHaveItApp.Services;
using System.Text.Json;

namespace DoWeHaveItApp.Extensions;

public static class ItemRequestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Parses the attributes JSON string into a list of ItemAttributeDto objects.
    // If the attributes list is already provided and has items, it is returned directly.
    // Otherwise, it attempts to parse the attributesJson string.
    // If parsing fails, it throws an ApiException.
    public static IReadOnlyList<ItemAttributeDto> ParseAttributes(
        string? attributesJson,
        IReadOnlyList<ItemAttributeDto>? attributes)
    {
        if (attributes is { Count: > 0 })
        {
            return attributes;
        }

        if (string.IsNullOrWhiteSpace(attributesJson))
        {
            return Array.Empty<ItemAttributeDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ItemAttributeDto>>(attributesJson, JsonOptions)
                   ?? new List<ItemAttributeDto>();
        }
        catch (JsonException)
        {
            throw new ApiException(400, "validation_error", "Invalid attributes payload.");
        }
    }

    // Resolves the image name based on the provided image name and fallback name.
    public static string ResolveImageName(string? imageName, string? fallbackName)
    {
        var resolved = imageName;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            resolved = Path.GetFileNameWithoutExtension(fallbackName ?? string.Empty);
        }

        return string.IsNullOrWhiteSpace(resolved) ? "image" : resolved;
    }
}
