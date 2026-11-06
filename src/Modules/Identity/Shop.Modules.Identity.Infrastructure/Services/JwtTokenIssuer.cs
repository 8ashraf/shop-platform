using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shop.Modules.Identity.Application;
using Shop.Modules.Identity.Domain;
using Shop.Shared.Application;

namespace Shop.Modules.Identity.Infrastructure.Services;

public class JwtTokenIssuer(IConfiguration configuration, IDateTimeProvider clock) : ITokenIssuer
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public string CreateAccessToken(User user, Account account, Guid? orgId, List<string> permissions)
    {
        // البيانات اللي جوه الـ token
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("principal_type", "Human"),
            new("account_id", account.Id.ToString()),
            new("account_type", account.Type.ToString())
        };

        if (orgId is not null)
            claims.Add(new Claim("org_id", orgId.Value.ToString()));

        foreach (var permission in permissions)
            claims.Add(new Claim("perms", permission));

        // التوقيع بالمفتاح السري
        var secret = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Configuration 'Jwt:Key' is missing.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
            Subject = new ClaimsIdentity(claims),
            Expires = clock.UtcNow.Add(AccessTokenLifetime),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}