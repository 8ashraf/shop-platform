using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shop.IntegrationTests;

public class IdentityAuthSmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/api/identity/me")]
    [InlineData("/api/identity/accounts")]
    public async Task Protected_endpoints_without_token_return_401(string url)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ping_is_open_without_token()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/identity/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}