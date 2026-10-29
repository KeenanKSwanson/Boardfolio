using Microsoft.EntityFrameworkCore;

namespace Boardfolio.Api.Data;

// A separate context type keeps SQL Server migrations independent of the SQLite history.
public class SqlServerBoardfolioDbContext(DbContextOptions<SqlServerBoardfolioDbContext> options)
    : BoardfolioDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Models.PrivateOwnerCosts>().Property(x => x.InitialAmount).HasPrecision(28, 8);
        builder.Entity<Models.PrivateOwnerCosts>().OwnsMany(x => x.Entries,
            entry => entry.Property(x => x.Amount).HasPrecision(28, 8));
    }
}
