namespace Stellaris.Core.Economy;

public sealed record PlanetHoldingInstance(string InstanceId, string DefinitionId, EntityId OwnerCountry);

/// <summary>Полный реестр связей планеты; создаётся только authoritative миром.</summary>
public sealed class PlanetForeignOwnershipSnapshot
{
    public EntityId Planet { get; }
    public EntityId PlanetOwner { get; }
    public EntityId? BranchOfficeOwner { get; }
    public IReadOnlyList<PlanetHoldingInstance> Holdings { get; }

    internal PlanetForeignOwnershipSnapshot(EntityId planet, EntityId planetOwner, EntityId? branchOfficeOwner,
        IEnumerable<PlanetHoldingInstance> holdings)
    {
        Planet = planet;
        PlanetOwner = planetOwner;
        BranchOfficeOwner = branchOfficeOwner;
        Holdings = Array.AsReadOnly(holdings.OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray());
    }
}

public sealed partial class EconomyWorld
{
    /// <summary>Регистрирует полный пустой реестр новой колонии; повторный вызов не стирает данные.</summary>
    public void InitializePlanetForeignOwnership(EntityId planet)
    {
        var target = ResolvePlanet(planet);
        if (target.ForeignOwnership is not null) throw new InvalidOperationException("Planet foreign ownership is already initialized.");
        target.ForeignOwnership = new(planet, _countries[target.OwnerIndex].Id, null, []);
    }

    public PlanetForeignOwnershipSnapshot? GetPlanetForeignOwnership(EntityId planet) => ResolvePlanet(planet).ForeignOwnership;

    /// <summary>Запись authoritative факта; native разрешение открытия филиала проверяет отдельный executor.</summary>
    public void SetPlanetBranchOfficeOwner(EntityId planet, EntityId? country)
    {
        var target = ResolvePlanet(planet);
        var snapshot = target.ForeignOwnership ?? throw new InvalidOperationException("Planet foreign ownership is unknown.");
        if (country is { } owner) _ = ResolveCountry(owner);
        target.ForeignOwnership = new(planet, snapshot.PlanetOwner, country, snapshot.Holdings);
    }

    /// <summary>Регистрирует установленное владение без выдачи технологии или обхода native executor.</summary>
    public void RegisterPlanetHolding(EntityId planet, string instanceId, string definitionId, EntityId country)
    {
        var target = ResolvePlanet(planet);
        var snapshot = target.ForeignOwnership ?? throw new InvalidOperationException("Planet foreign ownership is unknown.");
        _ = ResolveCountry(country);
        ValidateForeignId(instanceId);
        ValidateForeignId(definitionId);
        if (definitionId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("Invalid holding source identifier.", nameof(definitionId));
        if (snapshot.Holdings.Count >= 4096 || snapshot.Holdings.Any(item => item.InstanceId == instanceId))
            throw new ArgumentException("Duplicate holding instance or registry limit exceeded.");
        target.ForeignOwnership = new(planet, snapshot.PlanetOwner, snapshot.BranchOfficeOwner,
            snapshot.Holdings.Append(new(instanceId, definitionId, country)));
    }

    public void RemovePlanetHolding(EntityId planet, string instanceId)
    {
        var target = ResolvePlanet(planet);
        var snapshot = target.ForeignOwnership ?? throw new InvalidOperationException("Planet foreign ownership is unknown.");
        ValidateForeignId(instanceId);
        if (!snapshot.Holdings.Any(item => item.InstanceId == instanceId)) throw new ArgumentException("Unknown holding instance.");
        target.ForeignOwnership = new(planet, snapshot.PlanetOwner, snapshot.BranchOfficeOwner,
            snapshot.Holdings.Where(item => item.InstanceId != instanceId));
    }

    private static void ValidateForeignId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.Any(char.IsControl))
            throw new ArgumentException("Invalid foreign ownership identifier.");
    }
}
