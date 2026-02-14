namespace DoWeHaveItApp.Models;

public sealed class FormField
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required bool Required { get; set; }
}
