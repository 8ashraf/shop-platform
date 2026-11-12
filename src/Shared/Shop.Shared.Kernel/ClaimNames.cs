namespace Shop.Shared.Kernel;

/// <summary>أسماء الـ claims اللي بتتكتب في الـ JWT وبتتقرا منه. مصدر واحد للاتنين.</summary>
public static class ClaimNames
{
    public const string UserId = "sub";
    public const string AccountId = "account_id";
    public const string AccountType = "account_type";
    public const string OrgId = "org_id";
    public const string Permission = "perms";
}