using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Boardfolio.Api.Contracts.Requests;
using Boardfolio.Api.Contracts.Responses;
using Boardfolio.Api.DataAccessors;
using Boardfolio.Api.Models;

namespace Boardfolio.Api.Services;

public class TrackingException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public class KeyboardBuildService(KeyboardBuildDataAccessor data)
{
    private static readonly HashSet<string> Currencies = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(c => new RegionInfo(c.Name).ISOCurrencySymbol).ToHashSet(StringComparer.Ordinal);

    public async Task<KeyboardBuildListResponse> List(Guid owner, int page, int pageSize)
    {
        var count = await data.CountBuilds(owner);
        var offset = ((long)page - 1) * pageSize;
        var builds = offset >= count ? [] : await data.ListBuilds(owner, (int)offset, pageSize);
        return new(builds, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }

    public async Task<KeyboardBuild> Get(Guid owner, Guid id) =>
        await data.FindBuild(owner, id) ?? throw new TrackingException(404, "Build not found.");

    public async Task<KeyboardBuild> Create(Guid owner, CreateKeyboardBuildRequest request)
    {
        Validate(request);
        var build = new KeyboardBuild { OwnerUserId = owner, Name = request.Name.Trim(),
            KeyboardModel = request.KeyboardModel.Trim(), Notes = request.Notes?.Trim() };
        data.Add(build);
        await data.Save();
        return build;
    }

    public async Task<KeyboardBuild> Update(Guid owner, Guid id, UpdateKeyboardBuildRequest request)
    {
        Validate(request);
        var build = await Get(owner, id);
        build.Name = request.Name.Trim();
        build.KeyboardModel = request.KeyboardModel.Trim();
        build.Notes = request.Notes?.Trim();
        await data.Save();
        return build;
    }

    public async Task Delete(Guid owner, Guid id)
    {
        data.Remove(await Get(owner, id));
        await data.Save();
    }

    public async Task<KeyboardBuildComponent> SaveComponent(Guid owner, Guid buildId, Guid? id, ComponentRequest request)
    {
        var build = await Get(owner, buildId);
        await ValidateSwitchSelection(owner, request);
        var component = id is null ? new KeyboardBuildComponent { KeyboardBuildId = buildId }
            : build.Components.SingleOrDefault(x => x.Id == id) ?? throw new TrackingException(404, "Component not found.");
        ApplyComponent(component, request);
        if (id is null) data.Add(component);
        await data.Save();
        return component;
    }

    public async Task DeleteComponent(Guid owner, Guid buildId, Guid id)
    {
        var build = await Get(owner, buildId);
        var component = build.Components.SingleOrDefault(x => x.Id == id)
            ?? throw new TrackingException(404, "Component not found.");
        data.Remove(component);
        await data.Save();
    }

    public async Task<BuildMedia> AddMedia(Guid owner, Guid buildId, MediaRequest request)
    {
        Validate(request);
        var build = await Get(owner, buildId);
        if (build.Media.Count >= 20) throw new TrackingException(400, "A build can have up to 20 media links.");
        var media = new BuildMedia { KeyboardBuildId = buildId, Kind = request.Kind, Url = request.Url.Trim(), Caption = request.Caption?.Trim() };
        data.Add(media);
        await data.Save();
        return media;
    }

    public async Task DeleteMedia(Guid owner, Guid buildId, Guid id)
    {
        var build = await Get(owner, buildId);
        var media = build.Media.SingleOrDefault(x => x.Id == id) ?? throw new TrackingException(404, "Media not found.");
        data.Remove(media);
        await data.Save();
    }

    public async Task<SwitchCatalogResponse> ListSwitches(string? search, int page = 1, int pageSize = 200,
        string? catalogVersion = null, CancellationToken cancellationToken = default)
    {
        var result = await data.ReadSwitchCatalogPage(search?.Trim(), page, pageSize, catalogVersion, cancellationToken);
        if (catalogVersion is not null && catalogVersion != result.CatalogVersion)
            throw new TrackingException(409, "The switch catalog changed. Restart the download from page 1.");
        return new(result.Switches, page, pageSize, result.TotalCount,
            (int)Math.Ceiling(result.TotalCount / (double)pageSize), result.CatalogVersion);
    }

    public Task<string> GetSwitchCatalogVersion(CancellationToken cancellationToken = default) =>
        data.GetSwitchCatalogVersion(cancellationToken);
    public Task<List<CustomSwitch>> ListCustomSwitches(Guid owner) => data.ListCustomSwitches(owner);

    public async Task<CustomSwitch> SaveCustomSwitch(Guid owner, Guid? id, CustomSwitchRequest request)
    {
        Validate(request);
        var recipe = id is null ? new CustomSwitch { OwnerUserId = owner }
            : await data.FindCustomSwitch(owner, id.Value) ?? throw new TrackingException(404, "Custom switch not found.");
        ApplySwitch(recipe, request);
        if (id is null) data.Add(recipe);
        await data.Save();
        return recipe;
    }

    public async Task DeleteCustomSwitch(Guid owner, Guid id)
    {
        var recipe = await data.FindCustomSwitch(owner, id) ?? throw new TrackingException(404, "Custom switch not found.");
        if (await data.CustomSwitchInUse(id)) throw new TrackingException(409, "Remove this switch from your builds before deleting its recipe.");
        data.Remove(recipe);
        await data.Save();
    }

    public async Task<OwnerCostsResponse> GetCosts(Guid owner, Guid buildId)
    {
        // Retained ledgers remain readable by their owner after the build is deleted.
        var costs = await data.FindCosts(owner, buildId) ?? throw new TrackingException(404, "Cost record not found.");
        return CostResponse(costs);
    }

    public async Task<OwnerCostsResponse> SaveCosts(Guid owner, Guid buildId, OwnerCostsRequest request)
    {
        var build = await Get(owner, buildId);
        var costs = await data.FindCosts(owner, buildId);
        ValidateCosts(request, build, costs);
        if (costs is null)
        {
            costs = new PrivateOwnerCosts { KeyboardBuildId = buildId, OwnerUserId = owner };
            data.Add(costs);
        }
        ApplyCosts(costs, request);
        await data.Save();
        return CostResponse(costs);
    }

    public async Task<List<OwnerCostsResponse>> ListCosts(Guid owner) =>
        (await data.ListCosts(owner)).Select(CostResponse).ToList();

    public async Task<List<CurrencyTotal>> Totals(Guid owner) => (await data.ListCosts(owner))
        .GroupBy(x => x.CurrencyCode).Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.TotalPaid))).ToList();

    // Import is additive and atomic. Existing IDs owned by this account are skipped, never overwritten.
    public async Task<ImportResponse> Import(Guid owner, ImportRequest request)
    {
        Validate(request);
        if (request.Builds.Any(x => x is null) || request.CustomSwitches.Any(x => x is null) || request.ArchivedCosts.Any(x => x is null))
            throw new TrackingException(400, "Import records cannot be null.");
        RequireUniqueIds(request.Builds.Select(x => x.Id));
        RequireUniqueIds(request.CustomSwitches.Select(x => x.Id));
        RequireUniqueIds(request.ArchivedCosts.Select(x => x.KeyboardBuildId));
        RequireUniqueIds(request.Builds.SelectMany(x => x.Components ?? []).Select(x => x?.Id ?? Guid.Empty));
        await using var transaction = await data.BeginTransaction();
        var recipes = 0;
        var imported = 0;
        foreach (var input in request.CustomSwitches)
        {
            Validate(input);
            if (await data.FindCustomSwitch(owner, input.Id) is not null) continue;
            if (await data.CustomSwitchIdExists(input.Id)) throw new TrackingException(409, "An imported ID is unavailable. No records were imported.");
            var recipe = new CustomSwitch { Id = input.Id, OwnerUserId = owner };
            ApplySwitch(recipe, input);
            data.Add(recipe);
            recipes++;
        }
        await data.Save();
        foreach (var input in request.Builds)
        {
            Validate(input);
            if (await data.FindBuild(owner, input.Id) is not null) continue;
            if (await data.BuildIdExists(input.Id) || await data.FindCosts(owner, input.Id) is not null)
                throw new TrackingException(409, "An imported ID is unavailable. No records were imported.");
            var build = new KeyboardBuild { Id = input.Id, OwnerUserId = owner,
                Name = input.Name.Trim(), KeyboardModel = input.KeyboardModel.Trim(), Notes = input.Notes?.Trim() };
            foreach (var item in input.Components)
            {
                await ValidateSwitchSelection(owner, item);
                if (await data.ComponentIdExists(item.Id)) throw new TrackingException(409, "An imported component ID is unavailable.");
                var component = new KeyboardBuildComponent { Id = item.Id, KeyboardBuildId = build.Id };
                ApplyComponent(component, item);
                build.Components.Add(component);
            }
            data.Add(build);
            foreach (var media in input.Media)
            {
                Validate(media);
                var link = new BuildMedia { KeyboardBuildId = build.Id, Kind = media.Kind, Url = media.Url.Trim(), Caption = media.Caption?.Trim() };
                data.Add(link);
            }
            if (input.Costs is not null)
            {
                ValidateCosts(input.Costs, build, null);
                var costs = new PrivateOwnerCosts { KeyboardBuildId = build.Id, OwnerUserId = owner };
                ApplyCosts(costs, input.Costs);
                data.Add(costs);
            }
            imported++;
        }
        foreach (var input in request.ArchivedCosts)
        {
            Validate(input);
            if (request.Builds.Any(x => x.Id == input.KeyboardBuildId) || await data.BuildIdExists(input.KeyboardBuildId))
                throw new TrackingException(400, "Archived costs cannot refer to an active build.");
            if (await data.FindCosts(owner, input.KeyboardBuildId) is not null) continue;
            if (!Currencies.Contains(input.CurrencyCode)) throw new TrackingException(400, "Select a supported currency code.");
            foreach (var entry in input.Entries)
            {
                Validate(entry);
                if (entry.BuildComponentId == Guid.Empty) throw new TrackingException(400, "Component IDs cannot be empty.");
            }
            var costs = new PrivateOwnerCosts { KeyboardBuildId = input.KeyboardBuildId, OwnerUserId = owner };
            ApplyCosts(costs, input);
            data.Add(costs);
        }
        await data.Save();
        await transaction.CommitAsync();
        return new(imported, request.Builds.Count - imported, recipes);
    }

    public async Task<ImportRequest> Export(Guid owner)
    {
        var builds = await data.ListBuilds(owner, 0, int.MaxValue);
        var costs = await data.ListCosts(owner);
        return new ImportRequest
        {
            Builds = builds.Select(b => new ImportBuildRequest
            {
                Id = b.Id, Name = b.Name, KeyboardModel = b.KeyboardModel, Notes = b.Notes,
                Components = b.Components.Select(c => new ImportComponentRequest { Id = c.Id, Type = c.Type,
                    Name = c.Name, Manufacturer = c.Manufacturer, Quantity = c.Quantity,
                    KeyboardSwitchId = c.KeyboardSwitchId, CustomSwitchId = c.CustomSwitchId, Notes = c.Notes }).ToList(),
                Costs = costs.Where(c => c.KeyboardBuildId == b.Id).Select(c => ExportCosts(c, b)).SingleOrDefault(),
                Media = b.Media.Select(m => new MediaRequest { Kind = m.Kind, Url = m.Url, Caption = m.Caption }).ToList()
            }).ToList(),
            CustomSwitches = (await data.ListCustomSwitches(owner)).Select(s => new ImportSwitchRequest
            {
                Id = s.Id, Name = s.Name, Type = s.Type, TopHousing = s.TopHousing,
                BottomHousing = s.BottomHousing, Stem = s.Stem, Spring = s.Spring, Notes = s.Notes
            }).ToList(),
            ArchivedCosts = costs.Where(c => builds.All(b => b.Id != c.KeyboardBuildId)).Select(c =>
            {
                var result = ExportCosts(c);
                return new ArchivedCostsRequest { KeyboardBuildId = c.KeyboardBuildId,
                    CurrencyCode = result.CurrencyCode, TrackingMethod = result.TrackingMethod,
                    InitialAmount = result.InitialAmount, Entries = result.Entries, Notes = result.Notes };
            }).ToList()
        };
    }

    private static OwnerCostsRequest ExportCosts(PrivateOwnerCosts c, KeyboardBuild? build = null) => new()
    {
        CurrencyCode = c.CurrencyCode, TrackingMethod = c.TrackingMethod, InitialAmount = c.InitialAmount,
        Notes = c.Notes, Entries = c.Entries.Select(e => new CostEntryRequest {
            // Removed components have no portable relationship; retain the expense itself.
            BuildComponentId = build is not null && !build.Components.Any(x => x.Id == e.BuildComponentId) ? null : e.BuildComponentId,
            Description = e.Description, Amount = e.Amount, Stage = e.Stage, PaidOn = e.PaidOn }).ToList()
    };

    private async Task ValidateSwitchSelection(Guid owner, ComponentRequest request)
    {
        Validate(request);
        if (request.KeyboardSwitchId is Guid catalog && !await data.CatalogSwitchExists(catalog))
            throw new TrackingException(400, "The catalog switch does not exist.");
        if (request.CustomSwitchId is Guid custom && await data.FindCustomSwitch(owner, custom) is null)
            throw new TrackingException(400, "The custom switch is not available to this account.");
    }

    private static void ValidateCosts(OwnerCostsRequest request, KeyboardBuild build, PrivateOwnerCosts? previous)
    {
        Validate(request);
        if (!Currencies.Contains(request.CurrencyCode)) throw new TrackingException(400, "Select a supported currency code.");
        if (previous is not null && previous.CurrencyCode != request.CurrencyCode &&
            (previous.Entries.Count > 0 || previous.InitialAmount is not null))
            throw new TrackingException(409, "The existing ledger contains amounts in another currency. Currency conversion is not supported.");
        foreach (var entry in request.Entries)
        {
            Validate(entry);
            if (entry.BuildComponentId is Guid component &&
                !build.Components.Any(x => x.Id == component) &&
                !(previous?.Entries.Any(x => x.BuildComponentId == component) ?? false))
                throw new TrackingException(400, "A cost entry references a component outside this build.");
        }
    }

    private static void ApplyComponent(KeyboardBuildComponent target, ComponentRequest source)
    {
        target.Type = source.Type; target.Name = source.Name.Trim(); target.Manufacturer = source.Manufacturer?.Trim();
        target.Quantity = source.Quantity; target.KeyboardSwitchId = source.KeyboardSwitchId;
        target.CustomSwitchId = source.CustomSwitchId; target.Notes = source.Notes?.Trim();
    }

    private static void ApplySwitch(CustomSwitch target, CustomSwitchRequest source)
    {
        target.Name = source.Name.Trim(); target.Type = source.Type; target.TopHousing = source.TopHousing.Trim();
        target.BottomHousing = source.BottomHousing.Trim(); target.Stem = source.Stem.Trim();
        target.Spring = source.Spring.Trim(); target.Notes = source.Notes?.Trim();
    }

    private static void ApplyCosts(PrivateOwnerCosts target, OwnerCostsRequest source)
    {
        target.CurrencyCode = source.CurrencyCode; target.TrackingMethod = source.TrackingMethod;
        target.InitialAmount = source.InitialAmount; target.Notes = source.Notes?.Trim();
        target.Entries.Clear();
        target.Entries.AddRange(source.Entries.Select(x => new OwnerCostEntry { BuildComponentId = x.BuildComponentId,
            Description = x.Description.Trim(), Amount = x.Amount, Stage = x.Stage, PaidOn = x.PaidOn }));
    }

    private static OwnerCostsResponse CostResponse(PrivateOwnerCosts x) => new(x.KeyboardBuildId,
        x.CurrencyCode, x.TrackingMethod, x.InitialAmount, x.Entries, x.Notes, x.TotalPaid);

    private static void RequireUniqueIds(IEnumerable<Guid> ids)
    {
        var seen = new HashSet<Guid>();
        foreach (var id in ids)
            if (id == Guid.Empty || !seen.Add(id)) throw new TrackingException(400, "Import IDs must be nonempty and unique.");
    }

    private static void Validate(object? value)
    {
        if (value is null) throw new TrackingException(400, "A record cannot be null.");
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, true))
            throw new TrackingException(400, string.Join(" ", errors.Select(x => x.ErrorMessage)));
    }
}
