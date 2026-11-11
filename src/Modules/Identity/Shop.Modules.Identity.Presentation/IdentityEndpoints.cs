using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shop.Modules.Identity.Application;
using Shop.Modules.Identity.Domain;
using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Presentation;

public record RegisterRequest(string Phone);
public record VerifyRequest(string Phone, string Code);
public record RefreshRequest(string RefreshToken);
public record SwitchAccountRequest(Guid AccountId);
public record MerchantRegisterRequest(string StoreName);

public static class IdentityEndpoints
{
    public const string RoutePrefix = "/api/identity";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Identity");

        group.MapGet("/ping", () => Results.Ok(new { module = "Identity", status = "ok" }));

        // ---------------- الدخول والجلسات (مفتوحة) ----------------
        group.MapPost("/register", async (RegisterRequest request, AuthService auth) =>
        {
            var result = await auth.RequestOtpAsync(request.Phone);
            return result.IsFailure ? ToHttpError(result.Error) : Results.Ok(new { message = "OTP sent" });
        });

        group.MapPost("/verify", async (VerifyRequest request, AuthService auth) =>
        {
            var result = await auth.VerifyOtpAsync(request.Phone, request.Code);
            return result.IsFailure ? ToHttpError(result.Error) : Results.Ok(result.Value);
        });

        group.MapPost("/refresh", async (RefreshRequest request, AuthService auth) =>
        {
            var result = await auth.RefreshAsync(request.RefreshToken);
            return result.IsFailure ? ToHttpError(result.Error) : Results.Ok(result.Value);
        });

        group.MapPost("/logout", async (RefreshRequest request, AuthService auth) =>
        {
            await auth.LogoutAsync(request.RefreshToken);
            return Results.NoContent();
        });

        // ---------------- محمية: لازم access token ----------------
        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            userId = user.FindFirstValue("sub"),
            accountId = user.FindFirstValue("account_id"),
            accountType = user.FindFirstValue("account_type"),
            orgId = user.FindFirstValue("org_id"),
            permissions = user.FindAll("perms").Select(claim => claim.Value).ToList()
        })).RequireAuthorization();

        group.MapGet("/accounts", async (ClaimsPrincipal user, AuthService auth) =>
        {
            var accounts = await auth.ListAccountsAsync(GetUserId(user));
            return Results.Ok(accounts);
        }).RequireAuthorization();

        group.MapPost("/switch-account", async (SwitchAccountRequest request, ClaimsPrincipal user, AuthService auth) =>
        {
            var result = await auth.SwitchAccountAsync(GetUserId(user), request.AccountId);
            return result.IsFailure ? ToHttpError(result.Error) : Results.Ok(result.Value);
        }).RequireAuthorization();

        group.MapPost("/merchant/register", async (MerchantRegisterRequest request, ClaimsPrincipal user, MerchantService merchants) =>
        {
            var result = await merchants.RegisterAsync(GetUserId(user), request.StoreName);
            return result.IsFailure ? ToHttpError(result.Error) : Results.Ok(result.Value);
        }).RequireAuthorization();

        // ---------------- موظفي المنصة بس ----------------
        group.MapPost("/admin/merchants/{organizationId:guid}/approve", async (Guid organizationId, MerchantService merchants) =>
        {
            var result = await merchants.ApproveAsync(organizationId);
            return result.IsFailure ? ToHttpError(result.Error) : Results.Ok(new { organizationId, status = "Active" });
        }).RequireAuthorization("PlatformStaffOnly");

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub")!);

    // بنحوّل سبب الفشل لكود HTTP مناسب
    private static IResult ToHttpError(Error error)
    {
        var body = new { error = error.Code, message = error.Description };

        if (error == IdentityErrors.User.PhoneRequired || error == IdentityErrors.Organization.NameRequired)
            return Results.BadRequest(body);

        if (error == AuthErrors.AccountNotAvailable)
            return Results.Json(body, statusCode: StatusCodes.Status403Forbidden);

        if (error == MerchantErrors.OrganizationNotFound)
            return Results.NotFound(body);

        if (error == MerchantErrors.AlreadyMerchant || error == IdentityErrors.Organization.NotPending)
            return Results.Conflict(body);

        // أي سبب تاني (كود غلط، توكن منتهي...) = غير مسموح
        return Results.Json(body, statusCode: StatusCodes.Status401Unauthorized);
    }
}