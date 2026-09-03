namespace DotNetMonoRepoTemplate.Types;

public static class RoleName
{
    public const string ChatUser = "Chat User";
    public const string SuperAdmin = "Super Admin";
    public const string Moderator = "Moderator";
    public const string Support = "Support";

    public static readonly IReadOnlyList<string> AdminTierRoles = new[] { SuperAdmin, Moderator, Support };

    public static readonly IReadOnlyList<string> CustomerTierRoles = new[] { ChatUser };

    public static readonly IReadOnlyList<string> All = new[] { ChatUser, SuperAdmin, Moderator, Support };
}

public static class TokenScope
{
    public const string Customer = "customer";
    public const string Admin = "admin";
}
