namespace DoWeHaveItApp.Dtos;

public sealed class ErrorResponse
{
    public required ErrorDetail Error { get; init; }
}

public sealed class ErrorDetail
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}
