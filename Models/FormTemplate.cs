namespace DoWeHaveItApp.Models;

public sealed class FormTemplate
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required List<FormField> Fields { get; set; }
    public required string CreatedAt { get; set; }
}
