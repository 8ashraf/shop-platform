using System.Security.Cryptography;
using System.Text;
using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Application;

/// <summary>اللي بيرجع للمستخدم بعد تسجيل الدخول أو التجديد.</summary>
public record AuthTokens(Guid AccountId, string AccountType, string AccessToken, string RefreshToken);

public static class AuthErrors
{
    public static readonly Error InvalidRefreshToken =
        new("Identity.Auth.InvalidRefreshToken", "Refresh token is invalid or expired.");

    public static readonly Error RefreshTokenReused =
        new("Identity.Auth.RefreshTokenReused", "Refresh token was already used. All sessions were revoked.");

    public static readonly Error AccountNotActive =
        new("Identity.Auth.AccountNotActive", "Account is not active.");
}

/// <summary>بصمة SHA-256 (بنخزّن البصمة مش الكود ولا التوكن نفسه).</summary>
public static class Hasher
{
    public static string Sha256(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}