namespace DotNetMonoRepoTemplate.Types;

public static class Rbac
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [RoleName.SuperAdmin] = PermissionName.All,

            [RoleName.Moderator] = new[]
            {
                PermissionName.UserRead,
                PermissionName.UserWrite,
                PermissionName.RoleRead,
                PermissionName.ReportView,
                PermissionName.ReportExport,
                PermissionName.BatchWrite,
            },

            [RoleName.Support] = new[] { PermissionName.UserRead, PermissionName.RoleRead, PermissionName.ReportView },

            [RoleName.ChatUser] = new[] { PermissionName.UserRead },
        };

    public static IReadOnlyList<string> GetPermissionsForRole(string role) =>
        RolePermissions.TryGetValue(role, out var permissions) ? permissions : Array.Empty<string>();

    public static bool RoleHasPermission(string role, string permission) =>
        RolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(permission);
}
