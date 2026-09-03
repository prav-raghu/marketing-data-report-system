namespace DotNetMonoRepoTemplate.Utilities;

public sealed record ApiVersionInfo
{
    public required string Version { get; init; }
    public required bool IsDeprecated { get; init; }
    public string? SunsetDate { get; init; }
    public required bool IsCurrent { get; init; }
}

public sealed class ApiVersionManager
{
    private readonly Dictionary<string, ApiVersionInfo> _supportedVersions = new()
    {
        ["v1"] = new ApiVersionInfo { Version = "v1", IsDeprecated = true, SunsetDate = "2026-12-31", IsCurrent = false },
        ["v2"] = new ApiVersionInfo { Version = "v2", IsDeprecated = false, IsCurrent = true },
    };

    public bool IsVersionSupported(string version) => _supportedVersions.ContainsKey(version);

    public ApiVersionInfo? GetVersionInfo(string version) =>
        _supportedVersions.TryGetValue(version, out var info) ? info : null;

    public string GetCurrentVersion() =>
        _supportedVersions.Values.FirstOrDefault(v => v.IsCurrent)?.Version ?? "v1";

    public IReadOnlyList<string> GetSupportedVersions() => _supportedVersions.Keys.ToList();

    public IReadOnlyList<ApiVersionInfo> GetDeprecatedVersions() =>
        _supportedVersions.Values.Where(v => v.IsDeprecated).ToList();
}
