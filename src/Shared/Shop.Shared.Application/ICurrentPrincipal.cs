namespace Shop.Shared.Application;

/// <summary>مين اللي بيتكلم دلوقتي؟ (من الـ access token)</summary>
public interface ICurrentPrincipal
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }
    Guid AccountId { get; }

    /// <summary>Customer أو Merchant أو PlatformStaff (نص، عشان الـ Shared ما يشوفش Domain)</summary>
    string AccountType { get; }

    /// <summary>رقم المتجر. فاضي لو الحساب مش تاجر.</summary>
    Guid? OrgId { get; }

    IReadOnlyList<string> Permissions { get; }

    bool HasPermission(string permission);
}