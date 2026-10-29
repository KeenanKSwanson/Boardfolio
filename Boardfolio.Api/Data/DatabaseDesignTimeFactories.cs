using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Boardfolio.Api.Data;

public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<BoardfolioDbContext>
{
    public BoardfolioDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<BoardfolioDbContext>().UseSqlite(
            Environment.GetEnvironmentVariable("ConnectionStrings__Boardfolio")
                ?? "Data Source=App_Data/boardfolio.db").Options);
}

public class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerBoardfolioDbContext>
{
    public SqlServerBoardfolioDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqlServerBoardfolioDbContext>().UseSqlServer(
            Environment.GetEnvironmentVariable("ConnectionStrings__Boardfolio")
                ?? "Server=localhost;Database=BoardfolioDesignTime;Integrated Security=True;Encrypt=True").Options);
}
