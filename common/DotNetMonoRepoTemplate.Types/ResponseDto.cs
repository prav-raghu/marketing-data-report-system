namespace DotNetMonoRepoTemplate.Types;

public abstract record ResponseDto
{
    public required bool IsSuccessful { get; init; }
    public string? Message { get; init; }
    public DateTime? DateTimeStamp { get; init; }
}
