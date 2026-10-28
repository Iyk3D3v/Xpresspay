namespace XpressPay.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlyList<string> AllRoles = [Admin, User];
}
