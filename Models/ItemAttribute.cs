namespace DoWeHaveItApp.Models;

public sealed class ItemAttribute
{
    public required string FieldId { get; set; }
    public required string FieldName { get; set; }
    public required string Value { get; set; }
}
