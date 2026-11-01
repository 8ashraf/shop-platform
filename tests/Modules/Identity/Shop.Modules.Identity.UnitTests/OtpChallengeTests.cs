using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.UnitTests.Domain;

public class OtpChallengeTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static OtpChallenge NewChallenge() =>
        OtpChallenge.Issue("01000000000", "HASH_OK", Now, Lifetime);

    [Fact]
    public void Verify_WithCorrectHash_SucceedsAndConsumes()
    {
        var challenge = NewChallenge();

        var result = challenge.Verify("HASH_OK", Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.True(challenge.Consumed);
    }

    [Fact]
    public void Verify_WithWrongHash_FailsAndCountsAttempt()
    {
        var challenge = NewChallenge();

        var result = challenge.Verify("HASH_WRONG", Now.AddMinutes(1));

        Assert.Equal(IdentityErrors.Otp.Invalid, result.Error);
        Assert.Equal(1, challenge.Attempts);
        Assert.False(challenge.Consumed);
    }

    [Fact]
    public void Verify_AfterExpiry_Fails()
    {
        var challenge = NewChallenge();

        var result = challenge.Verify("HASH_OK", Now.Add(Lifetime));

        Assert.Equal(IdentityErrors.Otp.Expired, result.Error);
    }

    [Fact]
    public void Verify_Twice_SecondTimeFailsAsAlreadyUsed()
    {
        var challenge = NewChallenge();
        challenge.Verify("HASH_OK", Now.AddMinutes(1));

        var result = challenge.Verify("HASH_OK", Now.AddMinutes(2));

        Assert.Equal(IdentityErrors.Otp.AlreadyUsed, result.Error);
    }

    [Fact]
    public void Verify_AfterMaxWrongAttempts_RejectsEvenTheCorrectCode()
    {
        var challenge = NewChallenge();
        for (var i = 0; i < OtpChallenge.MaxAttempts; i++)
        {
            challenge.Verify("HASH_WRONG", Now.AddMinutes(1));
        }

        var result = challenge.Verify("HASH_OK", Now.AddMinutes(1));

        Assert.Equal(IdentityErrors.Otp.TooManyAttempts, result.Error);
        Assert.False(challenge.Consumed);
    }
}