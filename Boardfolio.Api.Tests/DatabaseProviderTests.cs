using Boardfolio.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Boardfolio.Api.Tests;

[TestClass]
public class DatabaseProviderTests
{
    [TestMethod]
    public void AzureConfigurationResolvesSqlServerContextWithoutConnecting()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:Provider", "SqlServer");
            builder.UseSetting("ConnectionStrings:Boardfolio",
                "Server=tcp:boardfolio-sql-dev.database.windows.net,1433;Database=boardfolio;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False");
        });
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BoardfolioDbContext>();
        Assert.IsInstanceOfType<SqlServerBoardfolioDbContext>(context);
        Assert.IsTrue(context.Database.IsSqlServer());
        Assert.IsFalse(context.Database.HasPendingModelChanges());
        Assert.AreSame(context, scope.ServiceProvider.GetRequiredService<SqlServerBoardfolioDbContext>());
    }

    [TestMethod]
    public void SqlServerMigrationGeneratesSchemaAndMatchesModelWithoutConnecting()
    {
        using var context = new SqlServerDesignTimeFactory().CreateDbContext([]);
        var migrations = context.Database.GetMigrations().ToList();
        Assert.HasCount(1, migrations);
        Assert.IsTrue(migrations[0].EndsWith("_InitialAzureSql"));
        Assert.IsFalse(context.Database.HasPendingModelChanges());
        var script = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        StringAssert.Contains(script, "CREATE TABLE [Users]");
        StringAssert.Contains(script, "CREATE TABLE [Builds]");
        StringAssert.Contains(script, "uniqueidentifier");
        StringAssert.Contains(script, "CK_Component_SwitchSelection");
        StringAssert.Contains(script, "IF NOT EXISTS");
    }

    [TestMethod]
    public void SqliteMigrationHistoryAndModelRemainUnchanged()
    {
        using var context = new BoardfolioDbContext(new DbContextOptionsBuilder<BoardfolioDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        Assert.HasCount(2, context.Database.GetMigrations().ToList());
        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }
}
