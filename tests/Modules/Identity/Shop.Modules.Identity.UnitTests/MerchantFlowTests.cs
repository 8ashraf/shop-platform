using Shop.Modules.Identity.Application;
using Shop.Modules.Identity.Domain;
using Shop.Modules.Identity.IntegrationEvents;

namespace Shop.Modules.Identity.UnitTests;

public class MerchantFlowTests
{
    private readonly FakeStore _store = new();
    private readonly FakeClock _clock = new();
    private readonly FakeOtpSender _sender = new();
    private readonly FakeEventBus _events = new();
    private readonly AuthService _auth;
    private readonly MerchantService _merchants;

    public MerchantFlowTests()
    {
        _auth = new AuthService(_store, _sender, new FakeTokenIssuer(), _clock);
        _merchants = new MerchantService(_store, _events);
    }

    // مساعد: مستخدم مسجّل دخول (عنده حساب Customer)
    private async Task<Guid> CreateUserAsync()
    {
        await _auth.RequestOtpAsync("01000000000");
        await _auth.VerifyOtpAsync("01000000000", _sender.LastCode);
        return _store.Users.Single().Id;
    }

    [Fact]
    public async Task Register_CreatesPendingStore_OwnerMembership_AndPendingMerchantAccount()
    {
        var userId = await CreateUserAsync();

        var result = await _merchants.RegisterAsync(userId, "Al-Amal");

        Assert.True(result.IsSuccess);
        var store = Assert.Single(_store.Organizations);
        Assert.Equal(OrgStatus.Pending, store.Status);
        var member = Assert.Single(_store.Members);
        Assert.Equal(OrgRole.Owner, member.Role);
        var merchant = _store.Accounts.Single(a => a.Type == AccountType.Merchant);
        Assert.Equal(AccountStatus.Pending, merchant.Status);
    }

    [Fact]
    public async Task Register_Twice_Fails()
    {
        var userId = await CreateUserAsync();
        await _merchants.RegisterAsync(userId, "Al-Amal");

        var result = await _merchants.RegisterAsync(userId, "Another Store");

        Assert.Equal(MerchantErrors.AlreadyMerchant, result.Error);
        Assert.Single(_store.Organizations);
    }

    [Fact]
    public async Task Register_WithBlankName_FailsAndCreatesNothing()
    {
        var userId = await CreateUserAsync();

        var result = await _merchants.RegisterAsync(userId, "  ");

        Assert.Equal(IdentityErrors.Organization.NameRequired, result.Error);
        Assert.Empty(_store.Organizations);
        Assert.DoesNotContain(_store.Accounts, a => a.Type == AccountType.Merchant);
    }

    [Fact]
    public async Task Approve_ActivatesStoreAndAccount_AndPublishesOneEvent()
    {
        var userId = await CreateUserAsync();
        var registration = (await _merchants.RegisterAsync(userId, "Al-Amal")).Value;

        var result = await _merchants.ApproveAsync(registration.OrganizationId);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrgStatus.Active, _store.Organizations.Single().Status);
        Assert.True(_store.Accounts.Single(a => a.Type == AccountType.Merchant).IsActive);
                var published = Assert.IsType<AccountActivated>(Assert.Single(_events.Published));
        Assert.Equal(registration.AccountId, published.AccountId);
        Assert.Equal(userId, published.UserId);
        Assert.Equal("Merchant", published.AccountType);
        Assert.Equal(registration.OrganizationId, published.OrganizationId);
    }

    [Fact]
    public async Task Approve_Twice_FailsAndDoesNotPublishAgain()
    {
        var userId = await CreateUserAsync();
        var registration = (await _merchants.RegisterAsync(userId, "Al-Amal")).Value;
        await _merchants.ApproveAsync(registration.OrganizationId);

        var result = await _merchants.ApproveAsync(registration.OrganizationId);

        Assert.Equal(IdentityErrors.Organization.NotPending, result.Error);
        Assert.Single(_events.Published);
    }

    [Fact]
    public async Task Approve_UnknownOrganization_Fails()
    {
        var result = await _merchants.ApproveAsync(Guid.NewGuid());

        Assert.Equal(MerchantErrors.OrganizationNotFound, result.Error);
        Assert.Empty(_events.Published);
    }

    [Fact]
    public async Task ListAccounts_ReturnsAllOfTheUsersAccounts()
    {
        var userId = await CreateUserAsync();
        await _merchants.RegisterAsync(userId, "Al-Amal");

        var accounts = await _auth.ListAccountsAsync(userId);

        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.Type == "Customer");
        Assert.Contains(accounts, a => a.Type == "Merchant");
    }

    [Fact]
    public async Task Switch_ToPendingMerchant_Fails()
    {
        var userId = await CreateUserAsync();
        var registration = (await _merchants.RegisterAsync(userId, "Al-Amal")).Value;

        var result = await _auth.SwitchAccountAsync(userId, registration.AccountId);

        Assert.Equal(AuthErrors.AccountNotAvailable, result.Error);
    }

    [Fact]
    public async Task Switch_ToSomeoneElsesAccount_Fails()
    {
        var userId = await CreateUserAsync();
        var otherAccount = Account.CreateCustomer(Guid.NewGuid());
        _store.Accounts.Add(otherAccount);

        var result = await _auth.SwitchAccountAsync(userId, otherAccount.Id);

        Assert.Equal(AuthErrors.AccountNotAvailable, result.Error);
    }

    [Fact]
    public async Task Switch_ToApprovedMerchant_CarriesStoreAndOwnerPermissions()
    {
        var userId = await CreateUserAsync();
        var registration = (await _merchants.RegisterAsync(userId, "Al-Amal")).Value;
        await _merchants.ApproveAsync(registration.OrganizationId);

        var result = await _auth.SwitchAccountAsync(userId, registration.AccountId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Merchant", result.Value.AccountType);
        Assert.Contains(registration.OrganizationId.ToString(), result.Value.AccessToken);
        Assert.Contains("staff.manage", result.Value.AccessToken);
    }
}