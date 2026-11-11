using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Shop.Modules.Catalog.Presentation;

public static class CatalogEndpoints
{
    public const string RoutePrefix = "/api/catalog";

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Catalog");

        group.MapGet("/ping", () => Results.Ok(new { module = "Catalog", status = "ok" }));

        return endpoints;
    }
}
