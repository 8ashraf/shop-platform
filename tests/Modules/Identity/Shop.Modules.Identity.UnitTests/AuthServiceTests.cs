using Shop.Modules.Identity.Application;
using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.UnitTests;

public class AuthServiceTests
{
    private readonly FakeStore _store = new();
    private readonly FakeClock _clock = new();
    private readonly FakeOtpSender _sender = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(_store, _sender, new FakeTokenIssuer(), _clock);
    }

    // مساعد: تسجيل دخول كامل (طلب كود ثم تحقق)
    private async Task<AuthTokens> LoginAsync()
    {
        await _auth.RequestOtpAsync("01000000000");
        var result = await _auth.VerifyOtpAsync("01000000000", _sender.LastCode);
        return result.Value;
    }

    [Fact]
    public async Task RequestOtp_WithBlankPhone_Fails()
    {
        var result = await _auth.RequestOtpAsync("  ");

        Assert.Equal(IdentityErrors.User.PhoneRequired, result.Error);
    }

    [Fact]
    public async Task RequestOtp_StoresHashOfCode_NotTheCode()
    {
        await _auth.RequestOtpAsync("01000000000");

        var stored = Assert.Single(_store.Otps);
        Assert.NotEqual(_sender.LastCode, stored.CodeHash);
    }

    [Fact]
    public async Task VerifyOtp_NewPhone_CreatesVerifiedUserAndCustomerAccount()
    {
        await LoginAsync();

        var user = Assert.Single(_store.Users);
        var account = Assert.Single(_store.Accounts);
        Assert.True(user.PhoneVerified);
        Assert.Equal(AccountType.Customer, account.Type);
        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task VerifyOtp_WithWrongCode_Fails()
    {
        await _auth.RequestOtpAsync("01000000000");

        var result = await _auth.VerifyOtpAsync("01000000000", "000000");

        Assert.Equal(IdentityErrors.Otp.Invalid, result.Error);
        Assert.Empty(_store.Users);
        Assert.Equal(1, _store.Otps.Single().Attempts);
    }

    [Fact]
    public async Task VerifyOtp_AfterExpiry_Fails()
    {
        await _auth.RequestOtpAsync("01000000000");
        _clock.Advance(TimeSpan.FromMinutes(6));

        var result = await _auth.VerifyOtpAsync("01000000000", _sender.LastCode);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Login_Twice_ReusesTheSameUserAndAccount()
    {
        await LoginAsync();
        await LoginAsync();

        Assert.Single(_store.Users);
        Assert.Single(_store.Accounts);
    }

    [Fact]
    public async Task Refresh_GivesNewTokens_AndRevokesTheOldOne()
    {
        var first = await LoginAsync();

        var result = await _auth.RefreshAsync(first.RefreshToken);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(first.RefreshToken, result.Value.RefreshToken);
        Assert.True(_store.RefreshTokens[0].IsRevoked);
        Assert.False(_store.RefreshTokens[1].IsRevoked);
    }

    [Fact]
    public async Task Refresh_WithAnAlreadyUsedToken_RevokesAllSessions()
    {
        var first = await LoginAsync();
        var second = (await _auth.RefreshAsync(first.RefreshToken)).Value;

        var reuse = await _auth.RefreshAsync(first.RefreshToken);

        Assert.Equal(AuthErrors.RefreshTokenReused, reuse.Error);
        Assert.All(_store.RefreshTokens, token => Assert.True(token.IsRevoked));

        var useSecond = await _auth.RefreshAsync(second.RefreshToken);
        Assert.True(useSecond.IsFailure);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Fails()
    {
        var result = await _auth.RefreshAsync("not-a-real-token");

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Refresh_AfterThirtyDays_Fails()
    {
        var tokens = await LoginAsync();
        _clock.Advance(TimeSpan.FromDays(31));

        var result = await _auth.RefreshAsync(tokens.RefreshToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Logout_RevokesTheToken_SoRefreshFails()
    {
        var tokens = await LoginAsync();

        await _auth.LogoutAsync(tokens.RefreshToken);
        var result = await _auth.RefreshAsync(tokens.RefreshToken);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Merchant_AccessToken_CarriesStoreAndRolePermissions()
    {
        // نجهّز: مستخدم له متجر Active وحساب تاجر Active بدور CatalogManager
        await LoginAsync();
        var user = _store.Users.Single();

        var store = Organization.Create("Al-Amal").Value;
        store.Approve();
        _store.Organizations.Add(store);
        _store.Members.Add(OrganizationMember.Create(store.Id, user.Id, OrgRole.CatalogManager));

        var merchantAccount = Account.CreateMerchant(user.Id);
        merchantAccount.Activate();
        _store.Accounts.Add(merchantAccount);

        // refresh token مربوط بحساب التاجر
        var merchantRefresh = RefreshToken.Issue(
            user.Id, merchantAccount.Id, Hasher.Sha256("merchant-token"), _clock.UtcNow, TimeSpan.FromDays(30));
        _store.RefreshTokens.Add(merchantRefresh);

        var result = await _auth.RefreshAsync("merchant-token");

        var accessToken = result.Value.AccessToken;
        Assert.Contains(store.Id.ToString(), accessToken);
        Assert.Contains("catalog.write", accessToken);
        Assert.DoesNotContain("finance.read", accessToken);
    }
}