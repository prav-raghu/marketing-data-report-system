namespace DotNetMonoRepoTemplate.Types;

public static class PermissionName
{
    public const string UserRead = "user:read";
    public const string UserWrite = "user:write";
    public const string UserDelete = "user:delete";

    public const string RoleRead = "role:read";
    public const string RoleAssign = "role:assign";

    public const string ReportView = "report:view";
    public const string ReportExport = "report:export";

    public const string SettingsRead = "settings:read";
    public const string SettingsWrite = "settings:write";

    public const string BatchWrite = "batch:write";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UserRead,
        UserWrite,
        UserDelete,
        RoleRead,
        RoleAssign,
        ReportView,
        ReportExport,
        SettingsRead,
        SettingsWrite,
        BatchWrite,
    };
}
