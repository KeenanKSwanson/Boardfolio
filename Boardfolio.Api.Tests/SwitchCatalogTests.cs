using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Boardfolio.Api.Contracts.Responses;
using Boardfolio.Api.Data;
using Boardfolio.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Boardfolio.Api.Tests;

[TestClass]
public class SwitchCatalogTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private ApiFactory factory = null!;
    private HttpClient client = null!;

    [TestInitialize]
    public void Setup()
    {
        factory = new ApiFactory();
        factory.Initialize();
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
    }

    [TestCleanup]
    public void Cleanup() { client.Dispose(); factory.Dispose(); }

    private async Task EditCatalog(Func<BoardfolioDbContext, Task> edit)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoardfolioDbContext>();
        await edit(db);
        await db.SaveChangesAsync();
    }

    private async Task<SwitchCatalogResponse> Page(string query = "") =>
        (await client.GetFromJsonAsync<SwitchCatalogResponse>("/api/switches" + query, JsonOptions))!;

    private async Task<string> Version() =>
        (await client.GetFromJsonAsync<SwitchCatalogVersionResponse>("/api/switches/version"))!.CatalogVersion;

    [TestMethod]
    public async Task FullDownloadIncludesMoreThan200SwitchesWithoutDuplicates()
    {
        await EditCatalog(db =>
        {
            // Identical names/manufacturers exercise the unique ID ordering tie-breaker.
            db.KeyboardSwitches.AddRange(Enumerable.Range(1, 205).Select(i => new KeyboardSwitch
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{i:D12}"),
                Manufacturer = "Test", Name = "Same name", Type = SwitchType.Linear
            }));
            return Task.CompletedTask;
        });
        var first = await Page("?pageSize=37");
        Assert.AreEqual(208, first.TotalCount);
        Assert.AreEqual(6, first.TotalPages);
        Assert.AreEqual(37, first.PageSize);
        Assert.AreEqual(await Version(), first.CatalogVersion);
        var ids = first.Switches.Select(x => x.Id).ToList();
        for (var page = 2; page <= first.TotalPages; page++)
        {
            var next = await Page($"?page={page}&pageSize=37&catalogVersion={first.CatalogVersion}");
            Assert.AreEqual(page, next.Page);
            Assert.AreEqual(first.CatalogVersion, next.CatalogVersion);
            Assert.AreEqual(first.TotalCount, next.TotalCount);
            ids.AddRange(next.Switches.Select(x => x.Id));
        }
        Assert.HasCount(208, ids);
        Assert.HasCount(208, ids.Distinct().ToList());
        var repeated = await Page("?pageSize=37");
        CollectionAssert.AreEqual(first.Switches.Select(x => x.Id).ToList(), repeated.Switches.Select(x => x.Id).ToList());
        Assert.HasCount(200, (await Page()).Switches);
    }

    [TestMethod]
    public async Task SearchAndEmptyOrOutOfRangePagesRetainGlobalVersion()
    {
        var version = await Version();
        var search = await Page("?search=%20Cherry%20&pageSize=2");
        Assert.AreEqual(3, search.TotalCount);
        Assert.AreEqual(2, search.TotalPages);
        Assert.HasCount(2, search.Switches);
        Assert.AreEqual(version, search.CatalogVersion);
        var noMatches = await Page("?search=does-not-exist");
        Assert.AreEqual(0, noMatches.TotalCount);
        Assert.AreEqual(0, noMatches.TotalPages);
        Assert.HasCount(0, noMatches.Switches);
        Assert.AreEqual(version, noMatches.CatalogVersion);
        var beyondEnd = await Page($"?page={int.MaxValue}&pageSize=200");
        Assert.HasCount(0, beyondEnd.Switches);
        Assert.AreEqual(3, beyondEnd.TotalCount);
        Assert.AreEqual(version, beyondEnd.CatalogVersion);
    }

    [TestMethod]
    [DataRow("Name")]
    [DataRow("Manufacturer")]
    [DataRow("Type")]
    public async Task EditingCatalogInvalidatesAnInProgressDownload(string field)
    {
        var previous = await Version();
        await EditCatalog(async db =>
        {
            var item = await db.KeyboardSwitches.FirstAsync();
            if (field == "Name") item.Name += " updated";
            else if (field == "Manufacturer") item.Manufacturer += " updated";
            else item.Type = SwitchType.Unknown;
        });
        var current = await Version();
        Assert.AreNotEqual(previous, current);
        Assert.AreEqual(current, (await Page()).CatalogVersion);
        var stale = await client.GetAsync($"/api/switches?page=2&pageSize=1&catalogVersion={previous}");
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        StringAssert.Contains(await stale.Content.ReadAsStringAsync(), "Restart the download");
    }

    [TestMethod]
    public async Task AddingAndRemovingSwitchesUpdatesVersionIncludingAnEmptyCatalog()
    {
        var original = await Version();
        var item = new KeyboardSwitch { Name = "New switch", Manufacturer = "Test" };
        await EditCatalog(db => { db.KeyboardSwitches.Add(item); return Task.CompletedTask; });
        Assert.AreNotEqual(original, await Version());
        await EditCatalog(async db => db.KeyboardSwitches.Remove(await db.KeyboardSwitches.SingleAsync(x => x.Id == item.Id)));
        Assert.AreEqual(original, await Version());
        await EditCatalog(async db => db.KeyboardSwitches.RemoveRange(await db.KeyboardSwitches.ToListAsync()));
        var empty = await Page();
        Assert.AreNotEqual(original, empty.CatalogVersion);
        Assert.AreEqual(await Version(), empty.CatalogVersion);
        Assert.HasCount(0, empty.Switches);
        Assert.AreEqual(0, empty.TotalPages);
    }

    [TestMethod]
    [DataRow("page=0")]
    [DataRow("page=-1")]
    [DataRow("pageSize=0")]
    [DataRow("pageSize=201")]
    [DataRow("page=2147483648")]
    [DataRow("catalogVersion=not-a-version")]
    public async Task InvalidPaginationOrVersionReturnsBadRequest(string query)
    {
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.GetAsync("/api/switches?" + query)).StatusCode);
    }
}
