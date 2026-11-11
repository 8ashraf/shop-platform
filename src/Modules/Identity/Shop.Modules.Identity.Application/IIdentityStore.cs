using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.Application;

/// <summary>كل اللي الـ Application محتاجه من قاعدة البيانات.</summary>
public interface IIdentityStore
{
    // المستخدمين
    Task<User?> FindUserByIdAsync(Guid id);
    Task<User?> FindUserByPhoneAsync(string phone);
    void AddUser(User user);

    // الحسابات
    Task<Account?> FindAccountByIdAsync(Guid id);
    Task<Account?> FindAccountAsync(Guid userId, AccountType type);
    Task<List<Account>> FindAccountsByUserAsync(Guid userId);
    void AddAccount(Account account);

    // أكواد التحقق
    Task<OtpChallenge?> FindOpenOtpAsync(string phone, DateTime now);
    void AddOtp(OtpChallenge otp);

    // الجلسات
    Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash);
    Task<List<RefreshToken>> FindActiveRefreshTokensAsync(Guid accountId, DateTime now);
    void AddRefreshToken(RefreshToken token);

    // المتاجر
    Task<Organization?> FindOrganizationByIdAsync(Guid id);
    void AddOrganization(Organization organization);
    void AddMember(OrganizationMember member);
    Task<OrganizationMember?> FindOwnerAsync(Guid organizationId);
    Task<OrganizationMember?> FindActiveMembershipAsync(Guid userId);

    // حفظ كل التغييرات مرة واحدة
    Task SaveChangesAsync();
}