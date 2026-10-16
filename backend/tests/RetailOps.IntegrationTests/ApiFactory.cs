using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using RetailOps.Application.Auth;
using RetailOps.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace RetailOps.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway SQL Server container. The app migrates and seeds itself on startup,
/// so tests run against the same schema, constraints and demo data as production.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__RetailOps", _sql.GetConnectionString() + ";Database=RetailOpsTests");
        Environment.SetEnvironmentVariable("RateLimiting__LoginPerMinute", "1000");
        _ = Server; // force startup (migrate + seed) once, outside individual test timings
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    public async Task<HttpClient> LoginAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, DataSeeder.DemoPassword));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public static class Users
{
    public const string Admin = "admin@andesretail.demo";
    public const string ManagerLaPaz = "gerente.lpz@andesretail.demo";
    public const string CashierLaPaz = "cajero.lpz@andesretail.demo";
    public const string ManagerElAlto = "gerente.eal@andesretail.demo";
    public const string OtherTenantAdmin = "admin@altiplano.demo";
}

public static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);
}
