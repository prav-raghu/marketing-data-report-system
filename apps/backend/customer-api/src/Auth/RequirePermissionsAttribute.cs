namespace CustomerApi.Auth;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequirePermissionsAttribute : Attribute
{
    public RequirePermissionsAttribute(params string[] permissions) => Permissions = permissions;

    public IReadOnlyList<string> Permissions { get; }
}
