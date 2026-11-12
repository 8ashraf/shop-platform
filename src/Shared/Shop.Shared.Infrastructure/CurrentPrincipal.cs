using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shop.Shared.Application;
using Shop.Shared.Kernel;

namespace Shop.Shared.Infrastructure;

public class CurrentPrincipal(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipal
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.Parse(GetRequired(ClaimNames.UserId));

    public Guid AccountId => Guid.Parse(GetRequired(ClaimNames.AccountId));

    public string AccountType => GetRequired(ClaimNames.AccountType);

    public Guid? OrgId =>
        Guid.TryParse(User?.FindFirstValue(ClaimNames.OrgId), out var orgId) ? orgId : null;

    public IReadOnlyList<string> Permissions =>
        User?.FindAll(ClaimNames.Permission).Select(claim => claim.Value).ToList() ?? [];

    public bool HasPermission(string permission) => Permissions.Contains(permission);

    // لو الـ claim مش موجود نرمي خطأ واضح بدل null غامض
    private string GetRequired(string claimName) =>
        User?.FindFirstValue(claimName)
        ?? throw new InvalidOperationException($"Claim '{claimName}' is missing. Is the request authenticated?");
}