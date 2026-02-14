namespace DoWeHaveItApp.Infrastructure;

public sealed class DynamoDbOptions
{
    public string TableName { get; set; } = "Inventory";
    public string Region { get; set; } = "ap-southeast-2";
    public bool UseLocal { get; set; }
    public string? ServiceUrl { get; set; }
}
