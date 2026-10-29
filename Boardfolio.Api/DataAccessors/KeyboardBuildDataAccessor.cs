using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Boardfolio.Api.Data;
using Boardfolio.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Boardfolio.Api.DataAccessors;

public class KeyboardBuildDataAccessor(BoardfolioDbContext db)
{
    public Task<int> CountBuilds(Guid owner) => db.Builds.CountAsync(x => x.OwnerUserId == owner);
    public Task<List<KeyboardBuild>> ListBuilds(Guid owner, int skip, int take) => db.Builds.AsNoTracking()
        .Where(x => x.OwnerUserId == owner).OrderBy(x => x.Id).Skip(skip).Take(take).Include(x => x.Components).Include(x => x.Media).AsSplitQuery().ToListAsync();
    public Task<KeyboardBuild?> FindBuild(Guid owner, Guid id) => db.Builds.Include(x => x.Components).Include(x => x.Media).AsSplitQuery()
        .SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == owner);
    public Task<bool> BuildIdExists(Guid id) => db.Builds.AnyAsync(x => x.Id == id);
    public Task<bool> ComponentIdExists(Guid id) => db.Set<KeyboardBuildComponent>().AnyAsync(x => x.Id == id);
    public async Task<(List<KeyboardSwitch> Switches, int TotalCount, string CatalogVersion)> ReadSwitchCatalogPage(
        string? search, int page, int pageSize, string? expectedVersion, CancellationToken cancellationToken)
    {
        // Keep the fingerprint, count, and page consistent if a catalog update runs concurrently.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var version = await ComputeSwitchCatalogVersion(cancellationToken);
        if (expectedVersion is not null && version != expectedVersion)
        {
            await transaction.CommitAsync(cancellationToken);
            return ([], 0, version);
        }
        var query = db.KeyboardSwitches.AsNoTracking()
            .Where(x => search == null || x.Name.Contains(search) || x.Manufacturer.Contains(search));
        var count = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        var switches = offset >= count ? [] : await query.OrderBy(x => x.Manufacturer)
            .ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (switches, count, version);
    }

    public async Task<string> GetSwitchCatalogVersion(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var version = await ComputeSwitchCatalogVersion(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return version;
    }

    private async Task<string> ComputeSwitchCatalogVersion(CancellationToken cancellationToken)
    {
        // Stream the public catalog fields: no full catalog list needs to be held in memory.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await foreach (var item in db.KeyboardSwitches.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Manufacturer, x.Type })
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(item));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public Task<bool> CatalogSwitchExists(Guid id) => db.KeyboardSwitches.AnyAsync(x => x.Id == id);
    public Task<List<CustomSwitch>> ListCustomSwitches(Guid owner) => db.CustomSwitches.AsNoTracking()
        .Where(x => x.OwnerUserId == owner).OrderBy(x => x.Name).ToListAsync();
    public Task<CustomSwitch?> FindCustomSwitch(Guid owner, Guid id) => db.CustomSwitches
        .SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == owner);
    public Task<bool> CustomSwitchIdExists(Guid id) => db.CustomSwitches.AnyAsync(x => x.Id == id);
    public Task<bool> CustomSwitchInUse(Guid id) => db.Set<KeyboardBuildComponent>().AnyAsync(x => x.CustomSwitchId == id);
    public Task<PrivateOwnerCosts?> FindCosts(Guid owner, Guid buildId) => db.OwnerCosts.Include(x => x.Entries)
        .SingleOrDefaultAsync(x => x.OwnerUserId == owner && x.KeyboardBuildId == buildId);
    public Task<List<PrivateOwnerCosts>> ListCosts(Guid owner) => db.OwnerCosts.AsNoTracking().Include(x => x.Entries)
        .Where(x => x.OwnerUserId == owner).ToListAsync();
    public void Add<T>(T entity) where T : class => db.Add(entity);
    public void Remove<T>(T entity) where T : class => db.Remove(entity);
    public Task Save() => db.SaveChangesAsync();
    public Task<IDbContextTransaction> BeginTransaction() => db.Database.BeginTransactionAsync();
}
