using Microsoft.EntityFrameworkCore;
using Shop.Modules.Identity.Application;
using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.Infrastructure.Database;

public class IdentityStore(IdentityDbContext db) : IIdentityStore
{
    // المستخدمين
    public Task<User?> FindUserByIdAsync(Guid id) =>
        db.Users.FirstOrDefaultAsync(user => user.Id == id);

    public Task<User?> FindUserByPhoneAsync(string phone) =>
        db.Users.FirstOrDefaultAsync(user => user.Phone == phone);

    public void AddUser(User user) => db.Users.Add(user);

    // الحسابات
    public Task<Account?> FindAccountByIdAsync(Guid id) =>
        db.Accounts.FirstOrDefaultAsync(account => account.Id == id);

    public Task<Account?> FindAccountAsync(Guid userId, AccountType type) =>
        db.Accounts.FirstOrDefaultAsync(account => account.UserId == userId && account.Type == type);

    public void AddAccount(Account account) => db.Accounts.Add(account);

    // أكواد التحقق: آخر كود لسه ما اتستخدمش ولا انتهى
    public Task<OtpChallenge?> FindOpenOtpAsync(string phone, DateTime now) =>
        db.OtpChallenges
            .Where(otp => otp.Phone == phone && !otp.Consumed && otp.ExpiresAt > now)
            .OrderByDescending(otp => otp.ExpiresAt)
            .FirstOrDefaultAsync();

    public void AddOtp(OtpChallenge otp) => db.OtpChallenges.Add(otp);

    // الجلسات
    public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash) =>
        db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash);

    public Task<List<RefreshToken>> FindActiveRefreshTokensAsync(Guid accountId, DateTime now) =>
        db.RefreshTokens
            .Where(token => token.AccountId == accountId
                         && token.RevokedAt == null
                         && token.ExpiresAt > now)
            .ToListAsync();

    public void AddRefreshToken(RefreshToken token) => db.RefreshTokens.Add(token);

    // عضوية المستخدم في متجر فعّال
    public Task<OrganizationMember?> FindActiveMembershipAsync(Guid userId) =>
        db.OrganizationMembers
            .Where(member => member.UserId == userId)
            .Where(member => db.Organizations.Any(org => org.Id == member.OrgId && org.Status == OrgStatus.Active))
            .FirstOrDefaultAsync();

        public Task<List<Account>> FindAccountsByUserAsync(Guid userId) =>
        db.Accounts.Where(account => account.UserId == userId).ToListAsync();

    public Task<Organization?> FindOrganizationByIdAsync(Guid id) =>
        db.Organizations.FirstOrDefaultAsync(organization => organization.Id == id);

    public void AddOrganization(Organization organization) => db.Organizations.Add(organization);

    public void AddMember(OrganizationMember member) => db.OrganizationMembers.Add(member);

    public Task<OrganizationMember?> FindOwnerAsync(Guid organizationId) =>
        db.OrganizationMembers.FirstOrDefaultAsync(member =>
            member.OrgId == organizationId && member.Role == OrgRole.Owner);

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}