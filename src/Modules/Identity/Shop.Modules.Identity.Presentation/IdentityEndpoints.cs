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

public static class IdentityEndpoints
{
    public const string RoutePrefix = "/api/identity";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Identity");

        group.MapGet("/ping", () => Results.Ok(new { module = "Identity", status = "ok" }));

        // 1) طلب كود
        group.MapPost("/register", async (RegisterRequest request, AuthService auth) =>
        {
            var result = await auth.RequestOtpAsync(request.Phone);

            if (result.IsFailure)
                return ToHttpError(result.Error);

            return Results.Ok(new { message = "OTP sent" });
        });

        // 2) التحقق من الكود وتسجيل الدخول
        group.MapPost("/verify", async (VerifyRequest request, AuthService auth) =>
        {
            var result = await auth.VerifyOtpAsync(request.Phone, request.Code);

            if (result.IsFailure)
                return ToHttpError(result.Error);

            return Results.Ok(result.Value);
        });

        // 3) تجديد الجلسة
        group.MapPost("/refresh", async (RefreshRequest request, AuthService auth) =>
        {
            var result = await auth.RefreshAsync(request.RefreshToken);

            if (result.IsFailure)
                return ToHttpError(result.Error);

            return Results.Ok(result.Value);
        });

        // 4) تسجيل الخروج
        group.MapPost("/logout", async (RefreshRequest request, AuthService auth) =>
        {
            await auth.LogoutAsync(request.RefreshToken);
            return Results.NoContent();
        });

        // 5) أنا مين؟ (محمي: لازم access token صالح)
        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            userId = user.FindFirstValue("sub"),
            accountId = user.FindFirstValue("account_id"),
            accountType = user.FindFirstValue("account_type"),
            orgId = user.FindFirstValue("org_id"),
            permissions = user.FindAll("perms").Select(claim => claim.Value).ToList()
        })).RequireAuthorization();

        return endpoints;
    }

    // الموبايل فاضي = 400 (الطلب نفسه غلط)، أي سبب تاني = 401 (مش مسموح)
    private static IResult ToHttpError(Error error)
    {
        if (error == IdentityErrors.User.PhoneRequired)
            return Results.BadRequest(new { error = error.Code, message = error.Description });

        return Results.Json(
            new { error = error.Code, message = error.Description },
            statusCode: StatusCodes.Status401Unauthorized);
    }
}