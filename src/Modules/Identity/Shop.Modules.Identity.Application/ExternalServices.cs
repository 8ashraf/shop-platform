using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.Application;

/// <summary>بيبعت الكود للمستخدم (SMS أو طباعة في الـ log وقت التطوير).</summary>
public interface IOtpSender
{
    Task SendAsync(string phone, string code);
}

/// <summary>بيصنع الـ access token (JWT).</summary>
public interface ITokenIssuer
{
    string CreateAccessToken(User user, Account account, Guid? orgId, List<string> permissions);
}