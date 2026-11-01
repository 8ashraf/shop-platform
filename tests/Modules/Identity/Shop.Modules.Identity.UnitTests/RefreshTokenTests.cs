using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.UnitTests.Domain;

public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static RefreshToken NewToken() =>
        RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "HASH", Now, Lifetime);

    [Fact]
    public void NewToken_IsActive()
    {
        Assert.True(NewToken().IsActive(Now.AddDays(1)));
    }

    [Fact]
    public void Token_IsInactiveAfterExpiry()
    {
        Assert.False(NewToken().IsActive(Now.Add(Lifetime)));
    }

    [Fact]
    public void Revoke_MakesTokenInactiveAndRecordsReplacement()
    {
        var token = NewToken();

        token.Revoke(Now.AddDays(1), "NEW_HASH");

        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(Now.AddDays(1)));
        Assert.Equal("NEW_HASH", token.ReplacedByHash);
    }

    [Fact]
    public void Revoke_Twice_KeepsTheFirstRevocation()
    {
        var token = NewToken();
        token.Revoke(Now.AddDays(1), "FIRST");

        token.Revoke(Now.AddDays(2), "SECOND");

        Assert.Equal("FIRST", token.ReplacedByHash);
        Assert.Equal(Now.AddDays(1), token.RevokedAt);
    }
}