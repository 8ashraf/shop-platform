using Shop.Modules.Identity.Application;
using Shop.Modules.Identity.Domain;
using Shop.Shared.Application;
using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.UnitTests;

/// <summary>قاعدة بيانات وهمية: مجرد قوائم في الذاكرة.</summary>
public class FakeStore : IIdentityStore
{
    public List<User> Users { get; } = [];
    public List<Account> Accounts { get; } = [];
    public List<OtpChallenge> Otps { get; } = [];
    public List<RefreshToken> RefreshTokens { get; } = [];
    public List<Organization> Organizations { get; } = [];
    public List<OrganizationMember> Members { get; } = [];

    public Task<User?> FindUserByIdAsync(Guid id) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> FindUserByPhoneAsync(string phone) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Phone == phone));

    public void AddUser(User user) => Users.Add(user);

    public Task<Account?> FindAccountByIdAsync(Guid id) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task<Account?> FindAccountAsync(Guid userId, AccountType type) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.UserId == userId && a.Type == type));

    public void AddAccount(Account account) => Accounts.Add(account);

    public Task<OtpChallenge?> FindOpenOtpAsync(string phone, DateTime now)
    {
        var otp = Otps
            .Where(o => o.Phone == phone && !o.Consumed && o.ExpiresAt > now)
            .OrderByDescending(o => o.ExpiresAt)
            .FirstOrDefault();
        return Task.FromResult(otp);
    }

    public void AddOtp(OtpChallenge otp) => Otps.Add(otp);

    public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash) =>
        Task.FromResult(RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<List<RefreshToken>> FindActiveRefreshTokensAsync(Guid accountId, DateTime now) =>
        Task.FromResult(RefreshTokens.Where(t => t.AccountId == accountId && t.IsActive(now)).ToList());

    public void AddRefreshToken(RefreshToken token) => RefreshTokens.Add(token);

    public Task<OrganizationMember?> FindActiveMembershipAsync(Guid userId)
    {
        var member = Members.FirstOrDefault(m =>
            m.UserId == userId &&
            Organizations.Any(o => o.Id == m.OrgId && o.Status == OrgStatus.Active));
        return Task.FromResult(member);
    }

    public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<List<Account>> FindAccountsByUserAsync(Guid userId) =>
        Task.FromResult(Accounts.Where(a => a.UserId == userId).ToList());

    public Task<Organization?> FindOrganizationByIdAsync(Guid id) =>
        Task.FromResult(Organizations.FirstOrDefault(o => o.Id == id));

    public void AddOrganization(Organization organization) => Organizations.Add(organization);

    public void AddMember(OrganizationMember member) => Members.Add(member);

    public Task<OrganizationMember?> FindOwnerAsync(Guid organizationId) =>
        Task.FromResult(Members.FirstOrDefault(m => m.OrgId == organizationId && m.Role == OrgRole.Owner));
}

public class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; private set; } = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan time) => UtcNow = UtcNow.Add(time);
}

public class FakeOtpSender : IOtpSender
{
    public List<string> SentCodes { get; } = [];

    public string LastCode => SentCodes[^1];

    public Task SendAsync(string phone, string code)
    {
        SentCodes.Add(code);
        return Task.CompletedTask;
    }
}

/// <summary>بدل JWT حقيقي: نص بسيط نقدر نقراه في الاختبار.</summary>
public class FakeTokenIssuer : ITokenIssuer
{
    public string CreateAccessToken(User user, Account account, Guid? orgId, List<string> permissions) =>
        $"org={orgId};perms={string.Join(",", permissions)}";
}

/// <summary>ناقل أحداث وهمي: بيحفظ الأحداث في قايمة عشان نفحصها.</summary>
public class FakeEventBus : IEventBus
{
    public List<IntegrationEvent> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}