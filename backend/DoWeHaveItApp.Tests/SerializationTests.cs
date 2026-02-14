using DoWeHaveItApp.Dtos;
using System.Text.Json;
using Xunit;

namespace DoWeHaveItApp.Tests;

public sealed class SerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateItemRequest_DeserializesAttributes()
    {
        const string payload = """
        {
          "name": "item1",
          "comments": "",
          "parentId": null,
          "attributes": [
            { "fieldId": "field-mlhlmsvk-bvl4", "fieldName": "f1", "value": "f1" },
            { "fieldId": "field-mlhlmynm-46gi", "fieldName": "f2", "value": "f2" },
            { "fieldId": "field-mlhln0jf-ppco", "fieldName": "f3", "value": "f3" }
          ]
        }
        """;

        var request = JsonSerializer.Deserialize<CreateItemRequest>(payload, JsonOptions);

        Assert.NotNull(request);
        Assert.NotNull(request!.Attributes);
        Assert.Equal(3, request.Attributes.Count);
        Assert.Equal("field-mlhlmsvk-bvl4", request.Attributes[0].FieldId);
    }
}
