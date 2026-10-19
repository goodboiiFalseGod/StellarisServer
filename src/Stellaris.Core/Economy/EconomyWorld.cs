using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;

namespace Stellaris.Core.Economy;

public readonly record struct EntityId(int Index, int Generation);
public enum ResourceStoragePolicy { SourceCapped, UnlimitedReserve, Unspecified }
public sealed record ResourceDefinition(string Id, decimal? Capacity, ResourceStoragePolicy StoragePolicy = ResourceStoragePolicy.SourceCapped);
public sealed record JobDefinition(string Id, string Category, decimal WorkforcePerJob, IReadOnlyDictionary<string, decimal> Production, IReadOnlyDictionary<string, decimal> Upkeep, string? SourceEconomicCategory = null);
public sealed class JobResourceClosure(IEnumerable<string> produced, IEnumerable<string> upkeep)
{
    public IReadOnlySet<string> Produced { get; } = produced.ToFrozenSet(StringComparer.Ordinal);
    public IReadOnlySet<string> Upkeep { get; } = upkeep.ToFrozenSet(StringComparer.Ordinal);
}
public sealed class ResourceStorageUnresolvedException(string resource) : InvalidOperationException("resource-storage-unresolved:" + resource)
{
    public string ResourceId { get; } = resource;
}
public sealed record CategoryDefinition(string Id, string? Parent);
public sealed record Modifier(string Category, string Resource, string Operation, decimal Add, decimal Mult);
internal sealed record InstalledCategoryJobModifier(string Category, string? Resource, string Operation, decimal Add, decimal Mult);
public sealed record InstalledJobEconomyEffects(decimal OutputMultiplier, decimal BonusWorkforceMultiplier)
{
    public IReadOnlyList<Modifier> Modifiers { get; } = Array.Empty<Modifier>();
    public IReadOnlyDictionary<string, decimal> JobBonusWorkforceMultipliers { get; } = new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>(StringComparer.Ordinal));

    public InstalledJobEconomyEffects(decimal outputMultiplier, decimal bonusWorkforceMultiplier, IEnumerable<Modifier> modifiers, IReadOnlyDictionary<string, decimal>? jobBonusWorkforceMultipliers = null) : this(outputMultiplier, bonusWorkforceMultiplier)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        var copy = modifiers.Take(4097).ToArray();
        if (copy.Length > 4096 || copy.Any(modifier => modifier is null)) { throw new ArgumentException("Invalid installed job modifier snapshot.", nameof(modifiers)); }
        Modifiers = Array.AsReadOnly(copy);
        if (jobBonusWorkforceMultipliers is not null)
        {
            if (jobBonusWorkforceMultipliers.Count > 4096) { throw new ArgumentException("Installed job Workforce snapshot exceeds limit.", nameof(jobBonusWorkforceMultipliers)); }
            JobBonusWorkforceMultipliers = new ReadOnlyDictionary<string, decimal>(new SortedDictionary<string, decimal>(jobBonusWorkforceMultipliers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal));
        }
    }
}
public sealed record JobAllocation(decimal Count, decimal Workforce, decimal BonusWorkforce = 0)
{
    public decimal EffectiveWorkforce => checked(Workforce + BonusWorkforce);
}
public sealed record JobFlowReport(IReadOnlyDictionary<string, decimal> Produced, IReadOnlyDictionary<string, decimal> Upkeep);
public sealed record StructuralProductionSource(string Id, string DefinitionId, string Category, IReadOnlyDictionary<string, decimal> Amounts);
public sealed record StructuralFlowReport(string DefinitionId, string Category, IReadOnlyDictionary<string, decimal> Produced);
public sealed record PlanetReport(IReadOnlyDictionary<string, JobAllocation> Allocated, IReadOnlyDictionary<string, decimal> Produced, IReadOnlyDictionary<string, decimal> JobUpkeep, IReadOnlyDictionary<string, decimal> BuildingUpkeep, IReadOnlyDictionary<string, decimal> DistrictUpkeep, IReadOnlyDictionary<string, decimal> ZoneUpkeep, IReadOnlyDictionary<string, decimal> Upkeep, IReadOnlyDictionary<string, decimal> Net, decimal UnemployedWorkforce, string Profile)
{
    public IReadOnlyDictionary<string, JobFlowReport>? JobFlows { get; init; }
    public IReadOnlyDictionary<string, decimal>? StructuralProduction { get; init; }
    public IReadOnlyDictionary<string, StructuralFlowReport>? StructuralFlows { get; init; }
}

/// <summary>
/// Экспериментальный профиль M1: однородная Workforce распределяется по порядку определений профессий.
/// Это не подтверждённая семантика Stellaris. Add относится только к точной категории, Mult — также к предкам.
/// Бонус Workforce не занимает вакансии; выпуск и содержание масштабируются эффективной Workforce до Mult.
/// Месяц применяется атомарно; превышение ёмкости ресурса отбрасывается, дефицит вызывает исключение.
/// Поколение идентификаторов фиксировано: удаление и повторное использование слотов пока не реализованы.
/// Идентификаторы локальны миру; передавать идентификаторы между мирами нельзя.
/// Изменение мира одновременно с AdvanceMonth не поддерживается.
/// </summary>
public sealed partial class EconomyWorld
{
    public const string EXPERIMENTAL_PROFILE = "M1 v3 experimental allocation: homogeneous workforce; definition-order allocation; bonus workforce outside vacancy cap; effective workforce scales base plus exact add before ancestor mult; isolated output and job upkeep; separate building upkeep; capacity clamp; deficit rejects month";

    private readonly ResourceDefinition[] _resources;
    private readonly JobDefinition[] _jobs;
    private readonly CategoryDefinition[] _categories;
    private readonly Dictionary<string, ResourceDefinition> _resourceById;
    private readonly Dictionary<string, JobDefinition> _jobById;
    private readonly Dictionary<string, CategoryDefinition> _categoryById;
    private readonly List<EntityMetadata> _entities = new();
    private readonly List<Country> _countries = new();
    private readonly List<Planet> _planets = new();
    private readonly List<Group> _groups = new();
    private readonly List<PopulationSpeciesSnapshot> _species = new();
    private int _month;

    public int Month => _month;

    public EconomyWorld(IReadOnlyList<ResourceDefinition> resources, IReadOnlyList<JobDefinition> jobs, IReadOnlyList<CategoryDefinition> categories)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(categories);
        _resources = resources.ToArray();
        _categories = categories.ToArray();
        _resourceById = Index(_resources, item => item.Id);
        _categoryById = Index(_categories, item => item.Id);
        foreach (var resource in _resources)
        {
            if (!Enum.IsDefined(resource.StoragePolicy) || resource.StoragePolicy == ResourceStoragePolicy.SourceCapped && resource.Capacity is null
                || resource.StoragePolicy == ResourceStoragePolicy.Unspecified && resource.Capacity is not null) { throw new ArgumentException("Invalid resource storage policy."); }
            if (resource.Capacity is { } capacity) { Nonnegative(capacity, nameof(resource.Capacity)); }
            if (resource.StoragePolicy == ResourceStoragePolicy.UnlimitedReserve && resource.Capacity != 0)
            {
                throw new ArgumentException("UnlimitedReserve не принимает числовую ёмкость.", nameof(resources));
            }
        }
        foreach (var category in _categories)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string? current = category.Id;
            while (current != null)
            {
                if (!seen.Add(current))
                {
                    throw new ArgumentException($"Цикл категорий: {category.Id}.");
                }
                current = Require(_categoryById, current, "категория").Parent;
            }
        }
        _jobs = jobs.Select(job => new JobDefinition(job.Id, job.Category, job.WorkforcePerJob, CopyAmounts(job.Production), CopyAmounts(job.Upkeep), job.SourceEconomicCategory)).ToArray();
        _jobById = Index(_jobs, item => item.Id);
        foreach (var job in _jobs)
        {
            Require(_categoryById, job.Category, "категория");
            if (job.SourceEconomicCategory is { } sourceCategory) { Require(_categoryById, sourceCategory, "source category"); }
            if (job.WorkforcePerJob <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(jobs), "Стоимость профессии в Workforce должна быть положительной.");
            }
        }
    }

    private EconomyWorld(EconomyWorld source)
    {
        _resources = source._resources;
        _jobs = source._jobs;
        _categories = source._categories;
        _resourceById = source._resourceById;
        _jobById = source._jobById;
        _categoryById = source._categoryById;
        _month = source._month;
        _entities.AddRange(source._entities);
        _groups.AddRange(source._groups);
        _species.AddRange(source._species);
        foreach (var country in source._countries)
        {
            _countries.Add(new Country { Id = country.Id, FounderSpecies = country.FounderSpecies, Balances = new Dictionary<string, decimal>(country.Balances, StringComparer.Ordinal) });
        }
        foreach (var planet in source._planets)
        {
            var copy = new Planet(planet.OwnerIndex) { Id = planet.Id, Modifiers = planet.Modifiers.ToArray(), BuildingUpkeep = CopyAmounts(planet.BuildingUpkeep), DistrictUpkeep = CopyAmounts(planet.DistrictUpkeep), ZoneUpkeep = CopyAmounts(planet.ZoneUpkeep), StructuralProductionSources = planet.StructuralProductionSources, InstalledCountryResourceCapacityAdditions = planet.InstalledCountryResourceCapacityAdditions, Report = planet.Report, InstalledJobCapacity = planet.InstalledJobCapacity is null ? null : ReadOnly(planet.InstalledJobCapacity.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), InstalledHousing = planet.InstalledHousing, InstalledZoneBuildingSlotsAdd = planet.InstalledZoneBuildingSlotsAdd, InstalledJobEconomyEffects = planet.InstalledJobEconomyEffects };
            copy.GroupIndices.AddRange(planet.GroupIndices);
            copy.ForeignOwnership = planet.ForeignOwnership;
            foreach (var pair in planet.Jobs) { copy.Jobs.Add(pair.Key, pair.Value); }
            foreach (var pair in planet.BonusWorkforceMultipliers) { copy.BonusWorkforceMultipliers.Add(pair.Key, pair.Value); }
            _planets.Add(copy);
        }
    }

    /// <summary>Copies mutable state for an isolated candidate; callers still serialize access.</summary>
    public EconomyWorld Fork()
    {
        return new EconomyWorld(this);
    }

    public EntityId CreateCountry()
    {
        var country = new Country();
        foreach (var resource in _resources)
        {
            country.Balances.Add(resource.Id, 0);
        }
        var id = AddEntity(EntityKind.Country, _countries.Count);
        country.Id = id;
        _countries.Add(country);
        return id;
    }

    public EntityId CreatePlanet(EntityId country)
    {
        var ownerIndex = ResolveIndex(country, EntityKind.Country);
        var planet = new Planet(ownerIndex);
        var id = AddEntity(EntityKind.Planet, _planets.Count);
        planet.Id = id;
        _planets.Add(planet);
        return id;
    }

    public EntityId CreateFounderSpecies(EntityId country, PopulationSpeciesDefinition definition)
    {
        var owner = ResolveCountry(country);
        ArgumentNullException.ThrowIfNull(definition);
        if (owner.FounderSpecies is not null) throw new InvalidOperationException("Founder species already exists.");
        var id = AddEntity(EntityKind.Species, _species.Count);
        _species.Add(new(id, country, definition));
        owner.FounderSpecies = id;
        return id;
    }

    public PopulationSpeciesSnapshot? GetFounderSpecies(EntityId country)
        => ResolveCountry(country).FounderSpecies is { } id ? _species[ResolveIndex(id, EntityKind.Species)] : null;

    /// <summary>Полный видовой состав связанных групп; отсутствующая связь означает Unknown, не пустой состав.</summary>
    public IReadOnlyList<PopulationSpeciesSnapshot>? GetOwnedSpeciesSnapshot(EntityId country)
    {
        var ownerIndex = ResolveIndex(country, EntityKind.Country);
        var groups = _planets.Where(planet => planet.OwnerIndex == ownerIndex).SelectMany(planet => planet.GroupIndices)
            .Select(index => _groups[index]).ToArray();
        if (groups.Length == 0 || groups.Any(group => group.Species is null)) return null;
        var species = groups.Select(group => group.Species!.Value).Distinct()
            .Select(id => _species[ResolveIndex(id, EntityKind.Species)]).OrderBy(item => item.Id.Index).ToArray();
        if (species.Any(item => item.OwnerCountry != country)) throw new InvalidOperationException("Species owner binding is invalid.");
        return Array.AsReadOnly(species);
    }

    public EntityId? GetGroupSpecies(EntityId group) => _groups[ResolveIndex(group, EntityKind.Group)].Species;

    public EntityId AddGroup(EntityId planet, decimal population, decimal workforcePerPop, EntityId? species = null)
    {
        var target = ResolvePlanet(planet);
        if (species is { } speciesId && _species[ResolveIndex(speciesId, EntityKind.Species)].OwnerCountry != _countries[target.OwnerIndex].Id)
            throw new ArgumentException("Species does not belong to the population owner.", nameof(species));
        Nonnegative(population, nameof(population));
        Nonnegative(workforcePerPop, nameof(workforcePerPop));
        var workforce = checked(population * workforcePerPop);
        // Проверяем агрегат до изменения коллекций.
        _ = checked(target.GroupIndices.Sum(index => _groups[index].Workforce) + workforce);
        var id = AddEntity(EntityKind.Group, _groups.Count);
        target.GroupIndices.Add(_groups.Count);
        _groups.Add(new Group(id, planet, population, workforcePerPop, workforce, species));
        return id;
    }

    public void SetJobs(EntityId planet, string job, decimal count)
    {
        var target = ResolvePlanet(planet);
        var definition = Require(_jobById, job, "профессия");
        Nonnegative(count, nameof(count));
        _ = checked(count * definition.WorkforcePerJob);
        target.Jobs[job] = count;
    }

    /// <summary>Raw additive installed contributions only; null means the snapshot has not been supplied.</summary>
    public IReadOnlyDictionary<string, decimal>? GetInstalledJobCapacity(EntityId planet)
    {
        var capacity = ResolvePlanet(planet).InstalledJobCapacity;
        return capacity is null ? null : ReadOnly(capacity.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    public decimal? GetInstalledHousing(EntityId planet) => ResolvePlanet(planet).InstalledHousing;
    public decimal? GetInstalledZoneBuildingSlotsAdd(EntityId planet) => ResolvePlanet(planet).InstalledZoneBuildingSlotsAdd;

    /// <summary>Separate installed-source channel; absence does not resolve a missing source condition.</summary>
    public InstalledJobEconomyEffects? GetInstalledJobEconomyEffects(EntityId planet) => ResolvePlanet(planet).InstalledJobEconomyEffects;

    /// <summary>Каналы base+installed Add; research overlay исключён, snapshot не меняет экономику.</summary>
    public IReadOnlyDictionary<string, JobResourceClosure> GetEffectiveJobResourceClosure(EntityId planet)
    {
        var installed = ResolvePlanet(planet).InstalledJobEconomyEffects?.Modifiers ?? Array.Empty<Modifier>();
        return _jobs.ToFrozenDictionary(job => job.Id, job => new JobResourceClosure(Keys(job, "produces", job.Production.Keys), Keys(job, "upkeep", job.Upkeep.Keys)), StringComparer.Ordinal);

        IEnumerable<string> Keys(JobDefinition job, string operation, IEnumerable<string> baseline)
        {
            var additions = installed.Where(modifier => modifier.Operation == operation && (modifier.Category == job.Category || modifier.Category == job.SourceEconomicCategory))
                .GroupBy(modifier => modifier.Resource, StringComparer.Ordinal).Where(group => group.Aggregate(0m, (sum, modifier) => checked(sum + modifier.Add)) != 0).Select(group => group.Key);
            return baseline.Concat(additions);
        }
    }

    public void SetInstalledJobEconomyEffects(EntityId planet, InstalledJobEconomyEffects? effects)
    {
        var target = ResolvePlanet(planet);
        if (effects is not null)
        {
            Nonnegative(effects.BonusWorkforceMultiplier, nameof(effects.BonusWorkforceMultiplier));
            foreach (var (job, multiplier) in effects.JobBonusWorkforceMultipliers)
            {
                Require(_jobById, job, "профессия");
                Nonnegative(multiplier, nameof(effects.JobBonusWorkforceMultipliers));
            }
            foreach (var job in _jobs) { _ = checked(target.BonusWorkforceMultipliers.GetValueOrDefault(job.Id) + effects.BonusWorkforceMultiplier + effects.JobBonusWorkforceMultipliers.GetValueOrDefault(job.Id)); }
            foreach (var modifier in effects.Modifiers) { ValidateModifier(modifier); }
        }
        target.InstalledJobEconomyEffects = effects;
    }

    internal InstalledJobEconomyEffects? ResolveInstalledJobEconomyEffects(InstalledJobEconomyEffects? effects, IReadOnlyList<InstalledCategoryJobModifier> categories)
    {
        if (categories.Count == 0 && (effects is null || effects.JobBonusWorkforceMultipliers.Keys.All(_jobById.ContainsKey))) { return effects; }
        var modifiers = effects?.Modifiers.ToList() ?? new List<Modifier>();
        foreach (var category in categories)
        {
            Require(_categoryById, category.Category, "category");
            var count = category.Resource is null ? _resources.Length : 1;
            if (count > 4096 - modifiers.Count) { throw new ArgumentException("Installed category expansion exceeds modifier budget."); }
            foreach (var resource in category.Resource is null ? _resources.Select(item => item.Id) : new[] { category.Resource })
            {
                var modifier = new Modifier(category.Category, resource, category.Operation, category.Add, category.Mult);
                ValidateModifier(modifier);
                modifiers.Add(modifier);
            }
        }
        var jobBonuses = effects?.JobBonusWorkforceMultipliers.Where(pair => _jobById.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return new(effects?.OutputMultiplier ?? 0, effects?.BonusWorkforceMultiplier ?? 0, modifiers, jobBonuses);
    }

    public void SetInstalledCapacity(EntityId planet, IReadOnlyDictionary<string, decimal> jobs, decimal housing, decimal zoneBuildingSlotsAdd)
    {
        var target = ResolvePlanet(planet);
        Nonnegative(housing, nameof(housing));
        Nonnegative(zoneBuildingSlotsAdd, nameof(zoneBuildingSlotsAdd));
        if (jobs.Count > 256 || jobs.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 160 || pair.Key.Any(char.IsControl) || pair.Value < 0)) { throw new ArgumentException("Invalid installed job-capacity snapshot."); }
        var capacity = ReadOnly(jobs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        target.InstalledJobCapacity = capacity;
        target.InstalledHousing = housing;
        target.InstalledZoneBuildingSlotsAdd = zoneBuildingSlotsAdd;
    }

    public void SetModifiers(EntityId planet, IReadOnlyList<Modifier> modifiers)
    {
        var target = ResolvePlanet(planet);
        ArgumentNullException.ThrowIfNull(modifiers);
        var copy = modifiers.ToArray();
        foreach (var modifier in copy)
        {
            ValidateModifier(modifier);
        }
        target.Modifiers = copy;
    }

    public void SetJobBonusWorkforceMultiplier(EntityId planet, string job, decimal multiplier)
    {
        var target = ResolvePlanet(planet);
        Require(_jobById, job, "профессия");
        Nonnegative(multiplier, nameof(multiplier));
        _ = checked(multiplier + (target.InstalledJobEconomyEffects?.BonusWorkforceMultiplier ?? 0) + (target.InstalledJobEconomyEffects?.JobBonusWorkforceMultipliers.GetValueOrDefault(job) ?? 0));
        target.BonusWorkforceMultipliers[job] = multiplier;
    }

    [Obsolete("Используйте SetBuildingUpkeep: расходы других источников должны храниться отдельно.")]
    public void SetFixedUpkeep(EntityId planet, IReadOnlyDictionary<string, decimal> upkeep)
    {
        SetBuildingUpkeep(planet, upkeep);
    }

    public void SetBuildingUpkeep(EntityId planet, IReadOnlyDictionary<string, decimal> upkeep)
    {
        ResolvePlanet(planet).BuildingUpkeep = CopyAmounts(upkeep);
    }

    public void SetDistrictUpkeep(EntityId planet, IReadOnlyDictionary<string, decimal> upkeep) => ResolvePlanet(planet).DistrictUpkeep = CopyAmounts(upkeep);
    /// <summary>Полная замена доверенных прямых потоков; входные коллекции не сохраняются по ссылке.</summary>
    public void SetStructuralProductionSources(EntityId planet, IEnumerable<StructuralProductionSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var target = ResolvePlanet(planet);
        var copied = sources.Take(4097).ToArray();
        if (copied.Length > 4096) { throw new ArgumentException("Structural source snapshot exceeds limit.", nameof(sources)); }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < copied.Length; index++)
        {
            var source = copied[index];
            if (source is null || string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 512 || source.Id.Any(char.IsControl)
                || string.IsNullOrWhiteSpace(source.DefinitionId) || source.DefinitionId.Length > 160 || source.DefinitionId.Any(char.IsControl)
                || !ids.Add(source.Id)) { throw new ArgumentException("Invalid structural source identity.", nameof(sources)); }
            Require(_categoryById, source.Category, "категория");
            copied[index] = source with { Amounts = CopyAmounts(source.Amounts) };
        }
        target.StructuralProductionSources = Array.AsReadOnly(copied.OrderBy(source => source.Id, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<StructuralProductionSource> GetStructuralProductionSources(EntityId planet) => ResolvePlanet(planet).StructuralProductionSources;
    public void SetZoneUpkeep(EntityId planet, IReadOnlyDictionary<string, decimal> upkeep) => ResolvePlanet(planet).ZoneUpkeep = CopyAmounts(upkeep);

    public IReadOnlyDictionary<string, decimal> GetResourceCapacities() => ReadOnly(_resources.Where(resource => resource.StoragePolicy == ResourceStoragePolicy.SourceCapped)
        .ToDictionary(resource => resource.Id, resource => resource.Capacity!.Value, StringComparer.Ordinal));

    /// <summary>Полная замена доверенного вклада колонии в лимиты её владельца.</summary>
    public void SetInstalledCountryResourceCapacityAdditions(EntityId planet, IReadOnlyDictionary<string, decimal> additions)
    {
        var target = ResolvePlanet(planet);
        var copied = CopyAmounts(additions);
        foreach (var pair in copied)
        {
            if (GetStoragePolicy(pair.Key) != ResourceStoragePolicy.SourceCapped)
                throw new ResourceStorageUnresolvedException(pair.Key);
        }
        var owner = _countries[target.OwnerIndex];
        foreach (var resource in _resources.Where(item => item.StoragePolicy == ResourceStoragePolicy.SourceCapped))
        {
            var cap = resource.Capacity!.Value;
            foreach (var other in _planets.Where(item => item.OwnerIndex == target.OwnerIndex))
                cap = checked(cap + (ReferenceEquals(other, target) ? copied : other.InstalledCountryResourceCapacityAdditions).GetValueOrDefault(resource.Id));
            if (owner.Balances[resource.Id] > cap)
                throw new InvalidOperationException("resource-capacity-reduction-unresolved:" + resource.Id);
        }
        target.InstalledCountryResourceCapacityAdditions = copied;
    }

    public IReadOnlyDictionary<string, decimal> GetInstalledCountryResourceCapacityAdditions(EntityId planet)
        => ResolvePlanet(planet).InstalledCountryResourceCapacityAdditions;

    public decimal GetEffectiveResourceCapacity(EntityId country, string resource)
    {
        ResolveCountry(country);
        var definition = Require(_resourceById, resource, "ресурс");
        if (definition.StoragePolicy != ResourceStoragePolicy.SourceCapped) throw new ResourceStorageUnresolvedException(resource);
        var cap = definition.Capacity!.Value;
        foreach (var planet in _planets.Where(item => _countries[item.OwnerIndex].Id == country))
            cap = checked(cap + planet.InstalledCountryResourceCapacityAdditions.GetValueOrDefault(resource));
        return cap;
    }

    public ResourceStoragePolicy GetStoragePolicy(string resource) => Require(_resourceById, resource, "ресурс").StoragePolicy;

    public IReadOnlyDictionary<string, decimal> GetBuildingUpkeep(EntityId planet)
    {
        return CopyAmounts(ResolvePlanet(planet).BuildingUpkeep);
    }

    public void SetBalance(EntityId country, string resource, decimal value)
    {
        var target = ResolveCountry(country);
        var definition = Require(_resourceById, resource, "ресурс");
        Nonnegative(value, nameof(value));
        if (definition.StoragePolicy == ResourceStoragePolicy.Unspecified && value > target.Balances[resource]) { throw new ResourceStorageUnresolvedException(resource); }
        if (definition.StoragePolicy == ResourceStoragePolicy.SourceCapped && value > GetEffectiveResourceCapacity(country, resource))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Баланс превышает ёмкость ресурса.");
        }
        target.Balances[resource] = value;
    }

    public decimal GetBalance(EntityId country, string resource)
    {
        var target = ResolveCountry(country);
        Require(_resourceById, resource, "ресурс");
        return target.Balances[resource];
    }

    public IReadOnlyDictionary<string, decimal> GetBalances(EntityId country)
    {
        return CopyAmounts(ResolveCountry(country).Balances);
    }

    internal decimal GetResourceCapacity(string resource) => Require(_resourceById, resource, "resource").Capacity ?? throw new ResourceStorageUnresolvedException(resource);

    public EntityId GetPlanetOwner(EntityId planet)
    {
        return _countries[ResolvePlanet(planet).OwnerIndex].Id;
    }

    public PlanetReport GetPlanetReport(EntityId planet)
    {
        return ResolvePlanet(planet).Report ?? throw new InvalidOperationException("Планета ещё не прошла успешный месячный расчёт.");
    }

    /// <summary>Текущие потоки без смены месяца, отчёта и списания запасов.</summary>
    public PlanetReport PreviewPlanetReport(EntityId planet) => Calculate(ResolvePlanet(planet));

    /// <summary>Текущая предварительная аллокация без расчёта потоков и списания запасов.</summary>
    public IReadOnlyDictionary<string, JobAllocation> PreviewPlanetAllocations(EntityId planet)
    {
        var allocations = Allocate(ResolvePlanet(planet), out _);
        return new ReadOnlyDictionary<string, JobAllocation>(allocations);
    }

    public void AdvanceMonth(bool parallel = false)
    {
        var nextMonth = checked(_month + 1);
        var reports = new PlanetReport[_planets.Count];
        if (parallel)
        {
            Parallel.For(0, _planets.Count, index => reports[index] = Calculate(_planets[index]));
        }
        else
        {
            for (var index = 0; index < _planets.Count; index++)
            {
                reports[index] = Calculate(_planets[index]);
            }
        }
        var totals = _countries.ToDictionary(country => country, _ => ZeroAmounts());
        for (var index = 0; index < _planets.Count; index++)
        {
            var total = totals[_countries[_planets[index].OwnerIndex]];
            foreach (var resource in _resources)
            {
                total[resource.Id] = checked(total[resource.Id] + reports[index].Net[resource.Id]);
            }
        }
        var balances = new Dictionary<Country, Dictionary<string, decimal>>();
        foreach (var country in _countries)
        {
            var next = ZeroAmounts();
            foreach (var resource in _resources)
            {
                var value = checked(country.Balances[resource.Id] + totals[country][resource.Id]);
                if (value < 0)
                {
                    // Принятое правило: дефицит минералов обнуляет запас без остановки месяца.
                    if (resource.Id == "minerals") { value = 0; }
                    else { throw new InvalidOperationException($"Дефицит ресурса {resource.Id} у государства {country.Id.Index}: {value}."); }
                }
                if (resource.StoragePolicy == ResourceStoragePolicy.Unspecified && value > country.Balances[resource.Id]) { throw new ResourceStorageUnresolvedException(resource.Id); }
                next[resource.Id] = resource.StoragePolicy == ResourceStoragePolicy.SourceCapped ? Math.Min(value, GetEffectiveResourceCapacity(country.Id, resource.Id)) : value;
            }
            balances.Add(country, next);
        }
        // Все потенциально ошибочные вычисления завершены до применения.
        foreach (var country in _countries)
        {
            country.Balances = balances[country];
        }
        for (var index = 0; index < _planets.Count; index++)
        {
            _planets[index].Report = reports[index];
        }
        _month = nextMonth;
    }

    public string StateHash()
    {
        // SortedDictionary и массивы сохраняют канонический порядок независимо от культуры процесса.
        var state = new
        {
            Profile = EXPERIMENTAL_PROFILE,
            Month,
            Entities = _entities,
            Groups = _groups,
            Species = _species.Select(species => new { species.Id, species.OwnerCountry, species.Definition.ClassId,
                species.Definition.ArchetypeId, Traits = species.Definition.Traits.Order(StringComparer.Ordinal).ToArray() }),
            Resources = _resources,
            Categories = _categories,
            Jobs = _jobs.Select(job => new { job.Id, job.Category, job.SourceEconomicCategory, job.WorkforcePerJob, Production = Sorted(job.Production), Upkeep = Sorted(job.Upkeep) }),
            Countries = _countries.Select(country => new { country.Id, country.FounderSpecies, Balances = Sorted(country.Balances) }),
            Planets = _planets.Select(planet => new
            {
                planet.Id,
                Owner = _countries[planet.OwnerIndex].Id,
                planet.ForeignOwnership,
                planet.GroupIndices,
                Jobs = Sorted(planet.Jobs),
                InstalledJobCapacity = planet.InstalledJobCapacity is null ? null : Sorted(planet.InstalledJobCapacity),
                planet.InstalledHousing,
                planet.InstalledZoneBuildingSlotsAdd,
                planet.InstalledJobEconomyEffects,
                BonusWorkforceMultipliers = Sorted(planet.BonusWorkforceMultipliers),
                planet.Modifiers,
                BuildingUpkeep = Sorted(planet.BuildingUpkeep),
                StructuralProductionSources = planet.StructuralProductionSources.Select(source => new { source.Id, source.DefinitionId, source.Category, Amounts = Sorted(source.Amounts) }),
                CountryResourceCapacityAdditions = Sorted(planet.InstalledCountryResourceCapacityAdditions),
                DistrictUpkeep = Sorted(planet.DistrictUpkeep),
                ZoneUpkeep = Sorted(planet.ZoneUpkeep),
                Report = planet.Report == null ? null : new
                {
                    Allocated = new SortedDictionary<string, JobAllocation>(planet.Report.Allocated.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal),
                    Produced = Sorted(planet.Report.Produced),
                    JobFlows = planet.Report.JobFlows?.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { JobId = pair.Key, Produced = Sorted(pair.Value.Produced), Upkeep = Sorted(pair.Value.Upkeep) }),
                    JobUpkeep = Sorted(planet.Report.JobUpkeep),
                    StructuralProduction = planet.Report.StructuralProduction is null ? null : Sorted(planet.Report.StructuralProduction),
                    StructuralFlows = planet.Report.StructuralFlows?.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { Id = pair.Key, pair.Value.DefinitionId, pair.Value.Category, Produced = Sorted(pair.Value.Produced) }),
                    BuildingUpkeep = Sorted(planet.Report.BuildingUpkeep),
                    DistrictUpkeep = Sorted(planet.Report.DistrictUpkeep),
                    ZoneUpkeep = Sorted(planet.Report.ZoneUpkeep),
                    Upkeep = Sorted(planet.Report.Upkeep),
                    Net = Sorted(planet.Report.Net),
                    planet.Report.UnemployedWorkforce,
                    planet.Report.Profile
                }
            })
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    }

    private PlanetReport Calculate(Planet planet)
    {
        var allocated = Allocate(planet, out var available);
        var produced = ZeroAmounts();
        var jobUpkeep = ZeroAmounts();
        var buildingUpkeep = ZeroAmounts();
        var districtUpkeep = ZeroAmounts();
        var zoneUpkeep = ZeroAmounts();
        var jobFlows = new Dictionary<string, JobFlowReport>(StringComparer.Ordinal);
        var structuralProduction = ZeroAmounts();
        var structuralFlows = new Dictionary<string, StructuralFlowReport>(StringComparer.Ordinal);
        foreach (var source in planet.StructuralProductionSources)
        {
            // README задаёт охват modifiers, но не native порядок структурного Add/Mult.
            if (planet.Modifiers.Any(modifier => modifier.Operation == "produces" && source.Amounts.ContainsKey(modifier.Resource)
                && (modifier.Add != 0 && modifier.Category == source.Category
                    || modifier.Mult != 0 && IsCategoryOrDescendant(source.Category, modifier.Category))))
            { throw new InvalidOperationException("Structural production modifier order is unresolved: " + source.Id); }
            var flow = ZeroAmounts();
            foreach (var pair in source.Amounts)
            {
                produced[pair.Key] = checked(produced[pair.Key] + pair.Value);
                structuralProduction[pair.Key] = checked(structuralProduction[pair.Key] + pair.Value);
                flow[pair.Key] = pair.Value;
            }
            structuralFlows.Add(source.Id, new(source.DefinitionId, source.Category, ReadOnly(flow)));
        }
        foreach (var job in _jobs)
        {
            var allocation = allocated[job.Id];
            var effectiveEmployed = allocation.EffectiveWorkforce / job.WorkforcePerJob;
            var installedOutput = IsCategoryOrDescendant(job.SourceEconomicCategory ?? job.Category, "planet_jobs") ? planet.InstalledJobEconomyEffects?.OutputMultiplier ?? 0 : 0;
            var productionFlow = ZeroAmounts(); var upkeepFlow = ZeroAmounts();
            Apply(job, effectiveEmployed, job.Production, "produces", planet.Modifiers, planet.InstalledJobEconomyEffects?.Modifiers ?? Array.Empty<Modifier>(), installedOutput, produced, productionFlow);
            Apply(job, effectiveEmployed, job.Upkeep, "upkeep", planet.Modifiers, planet.InstalledJobEconomyEffects?.Modifiers ?? Array.Empty<Modifier>(), 0, jobUpkeep, upkeepFlow);
            jobFlows.Add(job.Id, new(ReadOnly(productionFlow), ReadOnly(upkeepFlow)));
        }
        foreach (var pair in planet.BuildingUpkeep)
        {
            buildingUpkeep[pair.Key] = checked(buildingUpkeep[pair.Key] + pair.Value);
        }
        foreach (var pair in planet.DistrictUpkeep) { districtUpkeep[pair.Key] = checked(districtUpkeep[pair.Key] + pair.Value); }
        foreach (var pair in planet.ZoneUpkeep) { zoneUpkeep[pair.Key] = checked(zoneUpkeep[pair.Key] + pair.Value); }
        var upkeep = ZeroAmounts();
        var net = ZeroAmounts();
        foreach (var resource in _resources)
        {
            upkeep[resource.Id] = checked(jobUpkeep[resource.Id] + buildingUpkeep[resource.Id] + districtUpkeep[resource.Id] + zoneUpkeep[resource.Id]);
            net[resource.Id] = checked(produced[resource.Id] - upkeep[resource.Id]);
        }
        return new PlanetReport(new ReadOnlyDictionary<string, JobAllocation>(allocated), ReadOnly(produced), ReadOnly(jobUpkeep), ReadOnly(buildingUpkeep), ReadOnly(districtUpkeep), ReadOnly(zoneUpkeep), ReadOnly(upkeep), ReadOnly(net), available, EXPERIMENTAL_PROFILE)
        {
            JobFlows = new ReadOnlyDictionary<string, JobFlowReport>(jobFlows),
            StructuralProduction = ReadOnly(structuralProduction),
            StructuralFlows = new ReadOnlyDictionary<string, StructuralFlowReport>(structuralFlows)
        };
    }

    private Dictionary<string, JobAllocation> Allocate(Planet planet, out decimal available)
    {
        available = planet.GroupIndices.Sum(index => _groups[index].Workforce);
        var allocations = new Dictionary<string, JobAllocation>(StringComparer.Ordinal);
        foreach (var job in _jobs)
        {
            var workforce = Math.Min(available, checked(planet.Jobs.GetValueOrDefault(job.Id) * job.WorkforcePerJob));
            var bonusMultiplier = checked(planet.BonusWorkforceMultipliers.GetValueOrDefault(job.Id) + (planet.InstalledJobEconomyEffects?.BonusWorkforceMultiplier ?? 0) + (planet.InstalledJobEconomyEffects?.JobBonusWorkforceMultipliers.GetValueOrDefault(job.Id) ?? 0));
            allocations.Add(job.Id, new JobAllocation(workforce / job.WorkforcePerJob, workforce, checked(workforce * bonusMultiplier)));
            available -= workforce;
        }
        return allocations;
    }

    private bool IsCategoryOrDescendant(string category, string ancestor)
    {
        string? current = category;
        while (current is not null)
        {
            if (current == ancestor) { return true; }
            current = _categoryById[current].Parent;
        }
        return false;
    }

    private void ValidateModifier(Modifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        Require(_categoryById, modifier.Category, "category");
        Require(_resourceById, modifier.Resource, "resource");
        if (modifier.Operation != "produces" && modifier.Operation != "upkeep") { throw new ArgumentException($"Unknown modifier operation: {modifier.Operation}."); }
    }

    private void Apply(JobDefinition job, decimal employed, IReadOnlyDictionary<string, decimal> amounts, string operation, Modifier[] modifiers, IReadOnlyList<Modifier> installedModifiers, decimal installedOutputMultiplier, Dictionary<string, decimal> target, Dictionary<string, decimal> flow)
    {
        // Обходим все ресурсы: точный Add может создать выпуск, отсутствующий в базовой таблице.
        foreach (var resource in _resources)
        {
            decimal add = 0;
            var mult = installedOutputMultiplier;
            foreach (var modifier in modifiers.Concat(installedModifiers))
            {
                if (modifier.Operation != operation || modifier.Resource != resource.Id)
                {
                    continue;
                }
                // Synthetic runtime ID не меняет исходную category resources table.
                if (modifier.Category == job.Category || modifier.Category == job.SourceEconomicCategory)
                {
                    add = checked(add + modifier.Add);
                }
                if (modifier.Category == job.Category)
                {
                    mult = checked(mult + modifier.Mult);
                    continue;
                }
                string? category = job.SourceEconomicCategory ?? job.Category;
                while (category != null)
                {
                    if (category == modifier.Category)
                    {
                        mult = checked(mult + modifier.Mult);
                        break;
                    }
                    category = _categoryById[category].Parent;
                }
            }
            var baseAmount = checked(amounts.GetValueOrDefault(resource.Id) + add);
            var multiplier = checked(1 + mult);
            if (baseAmount < 0)
            {
                throw new InvalidOperationException($"Отрицательная база {operation}: {job.Id}/{resource.Id}.");
            }
            if (multiplier < 0)
            {
                throw new InvalidOperationException($"Отрицательный множитель {operation}: {job.Id}/{resource.Id}.");
            }
            var scaledAmount = checked(employed * baseAmount);
            var result = checked(scaledAmount * multiplier);
            target[resource.Id] = checked(target[resource.Id] + result);
            flow[resource.Id] = result;
        }
    }

    private IReadOnlyDictionary<string, decimal> CopyAmounts(IReadOnlyDictionary<string, decimal> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        var copy = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var pair in amounts)
        {
            Require(_resourceById, pair.Key, "ресурс");
            Nonnegative(pair.Value, pair.Key);
            copy.Add(pair.Key, pair.Value);
        }
        return ReadOnly(copy);
    }

    private Dictionary<string, decimal> ZeroAmounts()
    {
        return _resources.ToDictionary(resource => resource.Id, _ => 0m, StringComparer.Ordinal);
    }

    private EntityId AddEntity(EntityKind kind, int storeIndex)
    {
        var id = new EntityId(_entities.Count, 1);
        _entities.Add(new EntityMetadata(kind, storeIndex));
        return id;
    }

    private Country ResolveCountry(EntityId id)
    {
        return _countries[ResolveIndex(id, EntityKind.Country)];
    }

    private Planet ResolvePlanet(EntityId id)
    {
        return _planets[ResolveIndex(id, EntityKind.Planet)];
    }

    private int ResolveIndex(EntityId id, EntityKind kind)
    {
        if (id.Generation != 1 || id.Index < 0 || id.Index >= _entities.Count || _entities[id.Index].Kind != kind)
        {
            throw new ArgumentException($"Недопустимая сущность {id.Index}:{id.Generation}, ожидается {kind}.", nameof(id));
        }
        return _entities[id.Index].StoreIndex;
    }

    private static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            var id = key(item);
            if (string.IsNullOrWhiteSpace(id) || !result.TryAdd(id, item))
            {
                throw new ArgumentException($"Пустой либо повторяющийся идентификатор: {id}.");
            }
        }
        return result;
    }

    private static T Require<T>(Dictionary<string, T> items, string key, string kind)
    {
        if (key == null || !items.TryGetValue(key, out var value))
        {
            throw new ArgumentException($"Неизвестный {kind}: {key}.");
        }
        return value;
    }

    private static void Nonnegative(decimal value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, "Значение не может быть отрицательным.");
        }
    }

    private static IReadOnlyDictionary<string, decimal> ReadOnly(Dictionary<string, decimal> values)
    {
        return new ReadOnlyDictionary<string, decimal>(values);
    }

    private static SortedDictionary<string, decimal> Sorted(IReadOnlyDictionary<string, decimal> values)
    {
        return new SortedDictionary<string, decimal>(values.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal);
    }

    private sealed class Country
    {
        public EntityId? FounderSpecies;
        public EntityId Id;
        public Dictionary<string, decimal> Balances = new(StringComparer.Ordinal);
    }

    private sealed class Planet
    {
        public EntityId Id;
        public readonly int OwnerIndex;
        public PlanetForeignOwnershipSnapshot? ForeignOwnership;
        public readonly List<int> GroupIndices = new();
        public readonly Dictionary<string, decimal> Jobs = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, decimal>? InstalledJobCapacity;
        public decimal? InstalledHousing;
        public decimal? InstalledZoneBuildingSlotsAdd;
        public InstalledJobEconomyEffects? InstalledJobEconomyEffects;
        public readonly Dictionary<string, decimal> BonusWorkforceMultipliers = new(StringComparer.Ordinal);
        public Modifier[] Modifiers = Array.Empty<Modifier>();
        public IReadOnlyDictionary<string, decimal> BuildingUpkeep = new Dictionary<string, decimal>();
        public IReadOnlyList<StructuralProductionSource> StructuralProductionSources = Array.Empty<StructuralProductionSource>();
        public IReadOnlyDictionary<string, decimal> InstalledCountryResourceCapacityAdditions = ReadOnly(new Dictionary<string, decimal>(StringComparer.Ordinal));
        public IReadOnlyDictionary<string, decimal> DistrictUpkeep = new Dictionary<string, decimal>();
        public IReadOnlyDictionary<string, decimal> ZoneUpkeep = new Dictionary<string, decimal>();
        public PlanetReport? Report;

        public Planet(int ownerIndex)
        {
            OwnerIndex = ownerIndex;
        }
    }

    private readonly record struct Group(EntityId Id, EntityId Planet, decimal Population, decimal WorkforcePerPop, decimal Workforce, EntityId? Species);
    private readonly record struct EntityMetadata(EntityKind Kind, int StoreIndex);
    private enum EntityKind
    {
        Country,
        Planet,
        Group,
        Species
    }
}
