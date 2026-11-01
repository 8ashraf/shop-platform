using System.Security.Cryptography;
using System.Text;
using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Domain;

public sealed class OtpChallenge
{
    public const int MaxAttempts = 5;

    private OtpChallenge() { }

    public Guid Id { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public int Attempts { get; private set; }
    public bool Consumed { get; private set; }

    public static OtpChallenge Issue(string phone, string codeHash, DateTime nowUtc, TimeSpan lifetime) =>
        new()
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            CodeHash = codeHash,
            ExpiresAt = nowUtc.Add(lifetime)
        };

    // مهم: لو فشل التحقق، المحاولة اتحسبت. الـ Application لازم تحفظ التغيير (SaveChanges) حتى مع الفشل
    public Result Verify(string codeHash, DateTime nowUtc)
    {
        if (Consumed)
        {
            return Result.Failure(IdentityErrors.Otp.AlreadyUsed);
        }

        if (nowUtc >= ExpiresAt)
        {
            return Result.Failure(IdentityErrors.Otp.Expired);
        }

        if (Attempts >= MaxAttempts)
        {
            return Result.Failure(IdentityErrors.Otp.TooManyAttempts);
        }

        Attempts++;

        // مقارنة بزمن ثابت (بتمنع تخمين الكود من فرق الوقت)
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(CodeHash),
            Encoding.UTF8.GetBytes(codeHash));

        if (!matches)
        {
            return Result.Failure(IdentityErrors.Otp.Invalid);
        }

        Consumed = true;
        return Result.Success();
    }
}