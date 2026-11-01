using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.UnitTests.Domain;

public class AccountTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void CreateCustomer_IsActiveImmediately()
    {
        var account = Account.CreateCustomer(UserId);

        Assert.Equal(AccountType.Customer, account.Type);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void CreateMerchant_StartsPending()
    {
        var account = Account.CreateMerchant(UserId);

        Assert.Equal(AccountType.Merchant, account.Type);
        Assert.Equal(AccountStatus.Pending, account.Status);
        Assert.False(account.IsActive);
    }

    [Fact]
    public void Activate_PendingMerchant_Succeeds()
    {
        var account = Account.CreateMerchant(UserId);

        var result = account.Activate();

        Assert.True(result.IsSuccess);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void Activate_AlreadyActive_Fails()
    {
        var account = Account.CreateCustomer(UserId);

        var result = account.Activate();

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.Account.AlreadyActive, result.Error);
    }
}