namespace AdminWeb.Models;

public sealed record DemoPost
{
    public int UserId { get; init; }
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}
