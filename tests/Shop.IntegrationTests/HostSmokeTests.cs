using System.Net;
using Shop.Host;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shop.IntegrationTests;

public class HostSmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    public static TheoryData<string> ModuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var module in ModuleCatalog.All)
        {
            data.Add(module.Name);
        }

        if (data.Count == 0)
        {
            data.Add(string.Empty); // no modules yet; xUnit fails a theory that has no data
        }

        return data;
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public async Task Every_module_answers_ping(string module)
    {
        if (module.Length == 0)
        {
            return;
        }

        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/{module.ToLowerInvariant()}/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
