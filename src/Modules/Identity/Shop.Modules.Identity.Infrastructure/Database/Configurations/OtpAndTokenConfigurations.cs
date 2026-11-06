using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.Infrastructure.Database.Configurations;

public class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("OtpChallenges");
        builder.HasKey(otp => otp.Id);
        builder.Property(otp => otp.Id).ValueGeneratedNever();

        builder.Property(otp => otp.Phone).HasMaxLength(20).IsRequired();
        builder.Property(otp => otp.CodeHash).HasMaxLength(64).IsRequired();

        // بندوّر بالرقم في كل تحقق
        builder.HasIndex(otp => otp.Phone);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.ReplacedByHash).HasMaxLength(64);

        // بندوّر بالبصمة في كل refresh
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.AccountId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(token => token.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}