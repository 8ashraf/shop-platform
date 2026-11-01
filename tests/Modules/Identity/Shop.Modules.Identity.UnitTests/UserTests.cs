using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void Create_WithBlankPhone_Fails()
    {
        var result = User.Create(" ", DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.User.PhoneRequired, result.Error);
    }

    [Fact]
    public void Create_TrimsPhoneAndStartsUnverified()
    {
        var now = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);

        var user = User.Create(" 01000000000 ", now).Value;

        Assert.Equal("01000000000", user.Phone);
        Assert.False(user.PhoneVerified);
        Assert.Equal(now, user.CreatedAt);
    }

    [Fact]
    public void MarkPhoneVerified_SetsFlag()
    {
        var user = User.Create("01000000000", DateTime.UtcNow).Value;

        user.MarkPhoneVerified();

        Assert.True(user.PhoneVerified);
    }
}