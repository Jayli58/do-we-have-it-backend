using System.Text.Json.Serialization;

namespace DoWeHaveItApp.Dtos;

public sealed class ItemAttributeDto
{
    [JsonPropertyName("fieldId")]
    public required string FieldId { get; init; }

    [JsonPropertyName("fieldName")]
    public required string FieldName { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}
