using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Boardfolio.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Boardfolio.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection;
    public ApiFactory(string? path = null)
    {
        connection = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = path ?? ":memory:", Pooling = false }.ToString());
        connection.Open();
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BoardfolioDbContext>();
            services.RemoveAll<DbContextOptions<BoardfolioDbContext>>();
            services.AddDbContext<BoardfolioDbContext>(options => options.UseSqlite(connection));
        });
    }
    public void Initialize()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BoardfolioDbContext>().Database.Migrate();
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

[TestClass]
public class TrackingApiTests
{
    private ApiFactory factory = null!;
    [TestInitialize]
    public void Setup() { factory = new ApiFactory(); factory.Initialize(); }
    [TestCleanup]
    public void Cleanup() => factory.Dispose();
    [TestMethod]
    public void ApiReferenceDeclaresSuccessBodiesAndStatusCodes()
    {
        var explorer = factory.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.ApiExplorer.IApiDescriptionGroupCollectionProvider>();
        var endpoints = explorer.ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .Where(api => api.RelativePath?.StartsWith("api/") == true
                && !api.RelativePath.StartsWith("api/auth/"))
            .ToList();
        Assert.IsTrue(endpoints.Count > 0);
        foreach (var endpoint in endpoints)
        {
            var expectedStatus = endpoint.HttpMethod == "DELETE" ? 204
                : endpoint.HttpMethod == "POST" && endpoint.RelativePath != "api/me/import" ? 201 : 200;
            var success = endpoint.SupportedResponseTypes.Where(response => response.StatusCode < 300).ToList();
            Assert.HasCount(1, success, endpoint.RelativePath);
            Assert.AreEqual(expectedStatus, success[0].StatusCode, endpoint.RelativePath);
            if (expectedStatus != 204)
                Assert.IsTrue(success[0].Type is not null && success[0].Type != typeof(void), endpoint.RelativePath);
            if (!endpoint.RelativePath!.StartsWith("api/switches"))
                Assert.IsTrue(endpoint.SupportedResponseTypes.Any(response => response.StatusCode == 401), endpoint.RelativePath);
        }
    }

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

    private async Task<HttpClient> Account()
    {
        var client = Client();
        var credentials = new { email = $"{Guid.NewGuid():N}@example.test", password = "Test-only-Password42!" };
        (await client.PostAsJsonAsync("/api/auth/register", credentials)).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=false", credentials);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private static async Task<string> Build(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/builds", new { name = " My board ", keyboardModel = " Kit " });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["keyboardBuild"]!["id"]!.GetValue<string>();
    }

    [TestMethod]
    public async Task AnonymousCanReadCatalogButCannotAccessPrivateBuilds()
    {
        using var client = Client();
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/builds")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/costs")).StatusCode);
        var catalog = await client.GetFromJsonAsync<JsonObject>("/api/switches?search=MX");
        Assert.AreEqual(3, catalog!["switches"]!.AsArray().Count);
    }

    [TestMethod]
    public async Task BuildsPersistAcrossRequestsAndOtherAccountsCannotReadOrModifyThem()
    {
        using var owner = await Account(); using var other = await Account();
        var id = await Build(owner);
        var build = await owner.GetFromJsonAsync<JsonObject>($"/api/builds/{id}");
        Assert.AreEqual("My board", build!["keyboardBuild"]!["name"]!.GetValue<string>());
        Assert.IsFalse(build.ToJsonString().Contains("ownerUserId"));
        Assert.AreEqual(HttpStatusCode.NotFound, (await other.GetAsync($"/api/builds/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/builds/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/builds/{id}", new { name = "Stolen", keyboardModel = "X" })).StatusCode);
        Assert.AreEqual(0, (await other.GetFromJsonAsync<JsonObject>("/api/builds"))!["totalCount"]!.GetValue<int>());
        Assert.AreEqual(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/builds/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/builds/{id}")).StatusCode);
    }

    [TestMethod]
    public async Task InvalidInputReturnsBadRequestAndPaginationHandlesLargeOffsets()
    {
        using var client = await Account();
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/builds", new { name = " ", keyboardModel = "Kit" })).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.GetAsync("/api/builds?page=0")).StatusCode);
        await Build(client);
        var page = await client.GetFromJsonAsync<JsonObject>("/api/builds?page=2147483647&pageSize=100");
        Assert.AreEqual(0, page!["keyboardBuilds"]!.AsArray().Count);
        Assert.AreEqual(1, page["totalCount"]!.GetValue<int>());
    }

    [TestMethod]
    public async Task SwitchSelectionRejectsConflictsAndOtherOwnersRecipes()
    {
        using var owner = await Account(); using var other = await Account();
        var recipeResponse = await owner.PostAsJsonAsync("/api/custom-switches", new {
            name = "My Frankenswitch", type = "Tactile", topHousing = "Top", bottomHousing = "Bottom", stem = "Stem", spring = "Spring" });
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = (await recipeResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
        var id = await Build(owner); var otherId = await Build(other);
        var valid = new { type = "Switches", name = "Custom", quantity = 70, customSwitchId = recipe };
        Assert.AreEqual(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/builds/{id}/components", valid)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync($"/api/builds/{otherId}/components", valid)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/builds/{id}/components", new {
            type = "Switches", name = "Both", customSwitchId = recipe, keyboardSwitchId = "675a1063-7bbd-4559-b452-0d56f8867bc1" })).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/builds/{id}/components", new { type = "Case", name = "Case", customSwitchId = recipe })).StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/custom-switches/{recipe}")).StatusCode);
    }

    [TestMethod]
    public async Task PrivateCostsAvoidDoubleCountingAndSurviveBuildDeletion()
    {
        using var owner = await Account(); using var other = await Account(); var id = await Build(owner);
        var response = await owner.PutAsJsonAsync($"/api/builds/{id}/costs", new {
            currencyCode = "USD", trackingMethod = "OverallAmount", initialAmount = 200m,
            entries = new[] { new { description = "Initial breakdown", amount = 50m, stage = "InitialBuild" },
                new { description = "New switches", amount = 25.5m, stage = "LaterSpending" } } });
        response.EnsureSuccessStatusCode();
        Assert.AreEqual(225.5m, (await response.Content.ReadFromJsonAsync<JsonObject>())!["totalPaid"]!.GetValue<decimal>());
        Assert.AreEqual(HttpStatusCode.NotFound, (await other.GetAsync($"/api/builds/{id}/costs")).StatusCode);
        Assert.IsFalse((await owner.GetStringAsync($"/api/builds/{id}")).Contains("initialAmount"));
        (await owner.DeleteAsync($"/api/builds/{id}")).EnsureSuccessStatusCode();
        Assert.AreEqual(HttpStatusCode.OK, (await owner.GetAsync($"/api/builds/{id}/costs")).StatusCode);
        var export = await owner.GetFromJsonAsync<JsonObject>("/api/me/export");
        Assert.AreEqual(1, export!["archivedCosts"]!.AsArray().Count);
    }

    [TestMethod]
    public async Task CostValidationAndCurrencyTotalsAreSafe()
    {
        using var owner = await Account(); var usd = await Build(owner); var eur = await Build(owner);
        foreach (var (id, currency) in new[] { (usd, "USD"), (eur, "EUR") })
            (await owner.PutAsJsonAsync($"/api/builds/{id}/costs", new { currencyCode = currency, trackingMethod = "Itemized",
                entries = new[] { new { description = "Parts", amount = 12.34m, stage = "InitialBuild" } } })).EnsureSuccessStatusCode();
        var totals = await owner.GetFromJsonAsync<JsonArray>("/api/me/cost-totals");
        Assert.AreEqual(2, totals!.Count);
        Assert.AreEqual(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/builds/{usd}/costs", new {
            currencyCode = "EUR", trackingMethod = "OverallAmount", initialAmount = 10m })).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/builds/{usd}/costs", new {
            currencyCode = "USD", entries = new[] { new { description = "Invalid", amount = -1m } } })).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/builds/{usd}/costs", new {
            currencyCode = "USD", entries = new[] { new { description = "Invalid link", amount = 1m, buildComponentId = Guid.NewGuid() } } })).StatusCode);
    }

    [TestMethod]
    public async Task ImportIsIdempotentAndExportsRoundTrip()
    {
        using var client = await Account();
        var id = Guid.NewGuid(); var recipeId = Guid.NewGuid();
        var input = new { version = 1, customSwitches = new[] { new {
            id = recipeId, name = "Recipe", type = "Linear", topHousing = "Top", bottomHousing = "Bottom", stem = "Stem", spring = "Spring" } },
            builds = new[] { new { id, name = "Phone build", keyboardModel = "Kit",
                components = new[] { new { id = Guid.NewGuid(), type = "Switches", name = "Recipe", customSwitchId = recipeId, quantity = 70 } },
                costs = new { currencyCode = "USD", trackingMethod = "OverallAmount", initialAmount = 100m } } } };
        var first = await client.PostAsJsonAsync("/api/me/import", input);
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode, await first.Content.ReadAsStringAsync());
        var second = await client.PostAsJsonAsync("/api/me/import", input);
        Assert.AreEqual(1, (await second.Content.ReadFromJsonAsync<JsonObject>())!["skippedBuilds"]!.GetValue<int>());
        Assert.AreEqual(1, (await client.GetFromJsonAsync<JsonObject>("/api/builds"))!["totalCount"]!.GetValue<int>());
        var exported = await client.GetFromJsonAsync<JsonObject>("/api/me/export");
        (await client.PostAsJsonAsync("/api/me/import", exported)).EnsureSuccessStatusCode();
        Assert.AreEqual(100m, exported!["builds"]![0]!["costs"]!["initialAmount"]!.GetValue<decimal>());
    }

    [TestMethod]
    public async Task InvalidImportRollsBackTheWholeBatch()
    {
        using var client = await Account();
        var input = new { builds = new[] {
            new { id = Guid.NewGuid(), name = "Valid", keyboardModel = "Kit", components = Array.Empty<object>() },
            new { id = Guid.NewGuid(), name = "Invalid", keyboardModel = "Kit", components = new object[] {
                new { id = Guid.NewGuid(), type = "Switches", name = "Missing switch", keyboardSwitchId = Guid.NewGuid() } } } } };
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/me/import", input)).StatusCode);
        Assert.AreEqual(0, (await client.GetFromJsonAsync<JsonObject>("/api/builds"))!["totalCount"]!.GetValue<int>());
    }

    [TestMethod]
    public async Task EditingCostsAndDeletingComponentsPreservesExpenses()
    {
        using var client = await Account(); var id = await Build(client);
        var componentResponse = await client.PostAsJsonAsync($"/api/builds/{id}/components", new { type = "Case", name = "Case" });
        componentResponse.EnsureSuccessStatusCode();
        var componentId = (await componentResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
        foreach (var amount in new[] { 20m, 30m })
        {
            var response = await client.PutAsJsonAsync($"/api/builds/{id}/costs", new { currencyCode = "USD",
                entries = new[] { new { description = "Case", amount, buildComponentId = componentId } } });
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            Assert.AreEqual(amount, (await response.Content.ReadFromJsonAsync<JsonObject>())!["totalPaid"]!.GetValue<decimal>());
        }
        (await client.DeleteAsync($"/api/builds/{id}/components/{componentId}")).EnsureSuccessStatusCode();
        Assert.AreEqual(30m, (await client.GetFromJsonAsync<JsonObject>($"/api/builds/{id}/costs"))!["totalPaid"]!.GetValue<decimal>());
        var export = await client.GetFromJsonAsync<JsonObject>("/api/me/export");
        Assert.IsNull(export!["builds"]![0]!["costs"]!["entries"]![0]!["buildComponentId"]);
    }

    [TestMethod]
    public async Task MediaLinksAreValidatedAndRestrictedToTheOwner()
    {
        using var owner = await Account(); using var other = await Account(); var id = await Build(owner);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/builds/{id}/media",
            new { kind = "Photo", url = "javascript:alert(1)" })).StatusCode);
        var media = new { kind = "SoundTest", url = "https://example.test/typing.mp3", caption = "Sound test" };
        var response = await owner.PostAsJsonAsync($"/api/builds/{id}/media", media);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/builds/{id}/media", media)).StatusCode);
        Assert.AreEqual(1, (await owner.GetFromJsonAsync<JsonObject>($"/api/builds/{id}"))!["keyboardBuild"]!["media"]!.AsArray().Count);
    }

    [TestMethod]
    public async Task ExportRestoresActiveAndArchivedCostsIntoAFreshDatabase()
    {
        using var client = await Account(); var active = await Build(client); var archived = await Build(client);
        foreach (var id in new[] { active, archived })
            (await client.PutAsJsonAsync($"/api/builds/{id}/costs", new { currencyCode = "USD", trackingMethod = "OverallAmount", initialAmount = 45m })).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/builds/{archived}")).EnsureSuccessStatusCode();
        var export = await client.GetFromJsonAsync<JsonObject>("/api/me/export");
        factory.Dispose(); factory = new ApiFactory(); factory.Initialize();
        using var restored = await Account();
        (await restored.PostAsJsonAsync("/api/me/import", export)).EnsureSuccessStatusCode();
        Assert.AreEqual(1, (await restored.GetFromJsonAsync<JsonObject>("/api/builds"))!["totalCount"]!.GetValue<int>());
        Assert.AreEqual(2, (await restored.GetFromJsonAsync<JsonArray>("/api/me/costs"))!.Count);
        Assert.AreEqual(90m, (await restored.GetFromJsonAsync<JsonArray>("/api/me/cost-totals"))![0]!["totalPaid"]!.GetValue<decimal>());
    }

    [TestMethod]
    public async Task AccountAndBuildSurviveServerRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"boardfolio-test-{Guid.NewGuid():N}.db");
        try
        {
            factory.Dispose(); factory = new ApiFactory(path); factory.Initialize();
            var credentials = new { email = "restart@example.test", password = "Test-only-Password42!" };
            using var client = Client();
            (await client.PostAsJsonAsync("/api/auth/register", credentials)).EnsureSuccessStatusCode();
            var login = await client.PostAsJsonAsync("/api/auth/login", credentials);
            var token = (await login.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var id = await Build(client);
            factory.Dispose(); factory = new ApiFactory(path); factory.Initialize();
            using var afterRestart = Client();
            var newLogin = await afterRestart.PostAsJsonAsync("/api/auth/login", credentials);
            newLogin.EnsureSuccessStatusCode();
            afterRestart.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await newLogin.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>());
            Assert.AreEqual(HttpStatusCode.OK, (await afterRestart.GetAsync($"/api/builds/{id}")).StatusCode);
        }
        finally { factory.Dispose(); File.Delete(path); }
    }
}
