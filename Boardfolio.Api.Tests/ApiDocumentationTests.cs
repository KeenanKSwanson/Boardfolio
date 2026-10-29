using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Boardfolio.Api.Tests;

[TestClass]
public class ApiDocumentationTests
{
    [TestMethod]
    public async Task ProductionServesDocumentationAndSchemasWithoutDatabaseAccess()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var docs = await client.GetAsync("/docs");
        Assert.AreEqual(HttpStatusCode.OK, docs.StatusCode);
        StringAssert.Contains(await docs.Content.ReadAsStringAsync(), "Boardfolio API");

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.IsNotNull(document["paths"]!["/api/builds"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]);
        Assert.IsNotNull(document["paths"]!["/api/builds"]!["post"]!["responses"]!["201"]);
        Assert.IsNotNull(document["paths"]!["/api/builds/{id}"]!["delete"]!["responses"]!["204"]);

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/builds")).StatusCode);
    }

    [TestMethod]
    public async Task ProductionCanDisableBothDocumentationEndpoints()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ApiDocumentation:Enabled", "false");
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/docs")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [TestMethod]
    [DataRow("Production", true)]
    [DataRow("Development", false)]
    public async Task TestRequestButtonIsOnlyAvailableInDevelopment(string environment, bool hidden)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // No database connection is needed to render docs. SQL Server avoids local startup migrations.
            builder.UseSetting("Database:Provider", "SqlServer");
            builder.UseSetting("ConnectionStrings:Boardfolio",
                "Server=localhost;Database=DocumentationTests;Integrated Security=True;Encrypt=True");
            builder.UseSetting("ApiDocumentation:Enabled", "true");
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/docs");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(hidden, Regex.IsMatch(html, "\"hideTestRequestButton\"\\s*:\\s*true"));
    }
}
