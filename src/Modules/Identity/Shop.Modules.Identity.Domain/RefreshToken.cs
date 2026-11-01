namespace Shop.Modules.Identity.Domain;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByHash { get; private set; }

    public static RefreshToken Issue(
        Guid userId, Guid accountId, string tokenHash, DateTime nowUtc, TimeSpan lifetime) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            TokenHash = tokenHash,
            CreatedAt = nowUtc,
            ExpiresAt = nowUtc.Add(lifetime)
        };

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAt;

    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public void Revoke(DateTime nowUtc, string? replacedByHash = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = nowUtc;
        ReplacedByHash = replacedByHash;
    }
}