using System.Security.Cryptography;
using Shop.Modules.Identity.Domain;
using Shop.Shared.Application;
using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Application;

public class AuthService(
    IIdentityStore store,
    IOtpSender otpSender,
    ITokenIssuer tokenIssuer,
    IDateTimeProvider clock)
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    // ---------------------------------------------------------------
    // 1) طلب كود
    // ---------------------------------------------------------------
    public async Task<Result> RequestOtpAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure(IdentityErrors.User.PhoneRequired);

        phone = phone.Trim();

        // كود من 6 أرقام
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        // نحفظ البصمة بس، مش الكود
        var otp = OtpChallenge.Issue(phone, Hasher.Sha256(code), clock.UtcNow, OtpLifetime);
        store.AddOtp(otp);
        await store.SaveChangesAsync();

        await otpSender.SendAsync(phone, code);
        return Result.Success();
    }

    // ---------------------------------------------------------------
    // 2) التحقق من الكود وتسجيل الدخول
    // ---------------------------------------------------------------
    public async Task<Result<AuthTokens>> VerifyOtpAsync(string phone, string code)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(code))
            return Result.Failure<AuthTokens>(IdentityErrors.Otp.Invalid);

        phone = phone.Trim();
        var now = clock.UtcNow;

        // أ) دوّر على آخر كود مفتوح للرقم ده
        var otp = await store.FindOpenOtpAsync(phone, now);
        if (otp is null)
            return Result.Failure<AuthTokens>(IdentityErrors.Otp.Invalid);

        // ب) اتحقق منه
        var check = otp.Verify(Hasher.Sha256(code.Trim()), now);
        if (check.IsFailure)
        {
            // نحفظ حتى مع الفشل، عشان المحاولة تتحسب
            await store.SaveChangesAsync();
            return Result.Failure<AuthTokens>(check.Error);
        }

        // ج) المستخدم: موجود؟ لو لأ نعمله
        var user = await store.FindUserByPhoneAsync(phone);
        if (user is null)
        {
            user = User.Create(phone, now).Value;
            store.AddUser(user);
        }
        user.MarkPhoneVerified();

        // د) حساب العميل: موجود؟ لو لأ نعمله
        var account = await store.FindAccountAsync(user.Id, AccountType.Customer);
        if (account is null)
        {
            account = Account.CreateCustomer(user.Id);
            store.AddAccount(account);
        }

        if (!account.IsActive)
            return Result.Failure<AuthTokens>(AuthErrors.AccountNotActive);

        // هـ) نصدر الجلسة ونحفظ كل حاجة مرة واحدة
        var tokens = await CreateSessionAsync(user, account);
        await store.SaveChangesAsync();

        return Result.Success(tokens);
    }

    // ---------------------------------------------------------------
    // 3) تجديد الجلسة
    // ---------------------------------------------------------------
    public async Task<Result<AuthTokens>> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);

        var now = clock.UtcNow;

        // أ) دوّر على التوكن بالبصمة
        var oldToken = await store.FindRefreshTokenAsync(Hasher.Sha256(refreshToken));
        if (oldToken is null)
            return Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);

        // ب) توكن اتلغى واتستخدم تاني؟ ده معناه سرقة محتملة، نلغي كل جلسات الحساب
        if (oldToken.IsRevoked)
        {
            var openSessions = await store.FindActiveRefreshTokensAsync(oldToken.AccountId, now);
            foreach (var session in openSessions)
                session.Revoke(now);

            await store.SaveChangesAsync();
            return Result.Failure<AuthTokens>(AuthErrors.RefreshTokenReused);
        }

        // ج) منتهي؟
        if (oldToken.IsExpired(now))
            return Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);

        // د) المستخدم والحساب لسه سليمين؟
        var user = await store.FindUserByIdAsync(oldToken.UserId);
        var account = await store.FindAccountByIdAsync(oldToken.AccountId);
        if (user is null || account is null || !account.IsActive)
            return Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);

        // هـ) نصدر جلسة جديدة ونلغي القديمة (Rotation)
        var tokens = await CreateSessionAsync(user, account);
        oldToken.Revoke(now, Hasher.Sha256(tokens.RefreshToken));
        await store.SaveChangesAsync();

        return Result.Success(tokens);
    }

    // ---------------------------------------------------------------
    // 4) تسجيل الخروج (دايماً ينجح)
    // ---------------------------------------------------------------
    public async Task<Result> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Success();

        var token = await store.FindRefreshTokenAsync(Hasher.Sha256(refreshToken));
        if (token is not null && !token.IsRevoked)
        {
            token.Revoke(clock.UtcNow);
            await store.SaveChangesAsync();
        }

        return Result.Success();
    }

    // ---------------------------------------------------------------
    // مشترك: يصنع access token + refresh token لحساب معيّن
    // ---------------------------------------------------------------
    private async Task<AuthTokens> CreateSessionAsync(User user, Account account)
    {
        // التاجر بياخد معاه رقم المتجر وصلاحيات دوره
        Guid? orgId = null;
        var permissions = new List<string>();

        if (account.Type == AccountType.Merchant)
        {
            var membership = await store.FindActiveMembershipAsync(user.Id);
            if (membership is not null)
            {
                orgId = membership.OrgId;
                permissions = RolePermissions.ForRole(membership.Role).ToList();
            }
        }

        // refresh token: نص عشوائي، بنخزّن بصمته بس
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenRecord = RefreshToken.Issue(
            user.Id, account.Id, Hasher.Sha256(refreshToken), clock.UtcNow, RefreshTokenLifetime);
        store.AddRefreshToken(tokenRecord);

        var accessToken = tokenIssuer.CreateAccessToken(user, account, orgId, permissions);

        return new AuthTokens(account.Id, account.Type.ToString(), accessToken, refreshToken);
    }
}