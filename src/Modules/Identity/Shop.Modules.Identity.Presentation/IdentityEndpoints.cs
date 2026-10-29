using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Shop.Modules.Identity.Presentation;

public static class IdentityEndpoints
{
    public const string RoutePrefix = "/api/identity";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Identity");

        group.MapGet("/ping", () => Results.Ok(new { module = "Identity", status = "ok" }));

        return endpoints;
    }
}
