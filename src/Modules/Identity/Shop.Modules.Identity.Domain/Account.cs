using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Domain;

public sealed class Account : Entity
{
    private Account() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public AccountType Type { get; private set; }
    public AccountStatus Status { get; private set; }

    // العميل بيتفعّل فوراً (الموبايل اتحقق منه بالـ OTP)
    public static Account CreateCustomer(Guid userId) =>
        new()  {
                    Id = Guid.NewGuid(),
                    UserId = userId, Type = AccountType.Customer,
                    Status = AccountStatus.Active 
                };

    // التاجر لازم يستنى مراجعة KYB
    public static Account CreateMerchant(Guid userId) =>
        new() { 
                    Id = Guid.NewGuid(), 
                    UserId = userId, Type = AccountType.Merchant, 
                    Status = AccountStatus.Pending 
                };

    public static Account CreatePlatformStaff(Guid userId) =>
        new() { 
                    Id = Guid.NewGuid(),
                    UserId = userId, Type = AccountType.PlatformStaff, 
                    Status = AccountStatus.Active 
                };

    public bool IsActive => Status == AccountStatus.Active;

    public Result Activate()
    {
        if (Status == AccountStatus.Active)
        {
            return Result.Failure(IdentityErrors.Account.AlreadyActive);
        }

        Status = AccountStatus.Active;
        return Result.Success();
    }

    public void Suspend() => Status = AccountStatus.Suspended;
}