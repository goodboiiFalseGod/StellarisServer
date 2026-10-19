using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Stellaris.Session.M3;

/// <summary>Источник стартовых планов для явно выбранных preset M3.</summary>
public sealed class M3StartupCatalog
{
    private readonly IReadOnlyDictionary<(string Template, int Size), StartupBuildingPlan> _plans;
    public StartupHomeworldInitializerCatalog Homeworlds { get; }

    private M3StartupCatalog(StartupHomeworldInitializerCatalog homeworlds, IDictionary<(string Template, int Size), StartupBuildingPlan> plans)
    {
        Homeworlds = homeworlds;
        _plans = new System.Collections.ObjectModel.ReadOnlyDictionary<(string Template, int Size), StartupBuildingPlan>(new Dictionary<(string Template, int Size), StartupBuildingPlan>(plans));
    }

    public static async Task<M3StartupCatalog> ImportAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        var homes = await new StartupHomeworldInitializerImporter().ImportAsync(gameRoot, cancellationToken);
        var plans = new Dictionary<(string Template, int Size), StartupBuildingPlan>();
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            for (var size = homes.MinimumSize; size <= homes.MaximumSize; size++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(templateId + "|" + size.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                var context = new StartupOwnerContext(StartupAuthority.Regular, template.Ethics, template.Civics, template.OriginId, size,
                    planetClass: template.IdealPlanetClass, countryUsesFoodGameStart: true, blockerChoiceSeed: BinaryPrimitives.ReadInt32LittleEndian(seedBytes),
                    sourcePredicateFacts: OrdinarySourcePredicateFacts(template));
                plans.Add((templateId, size), await new StartupBuildingImporter().GenerateAsync(gameRoot, context, cancellationToken));
            }
        }
        return new M3StartupCatalog(homes, plans);
    }

    private static IReadOnlyDictionary<string, bool?> OrdinarySourcePredicateFacts(ConstructionOrdinaryTemplate template)
    {
        if (template.OriginId != "origin_default") { throw new StartupBuildingImportException("Source-world predicates are only known for origin_default presets."); }
        var environmentalCivics = new[] { "civic_environmental_architects", "civic_environmental_architects_hive", "civic_environmental_architects_machine", "civic_environmental_architects_megacorp" };
        return new Dictionary<string, bool?>(StringComparer.Ordinal)
        {
            // В поддержанных обычных пресетах основатель не lithoid/infernal; перечисленные особые civics отсутствуют.
            ["is_apocalyptic_empire"] = false,
            ["is_environmental_architects_empire"] = template.Civics.Any(environmentalCivics.Contains),
            ["is_lithoid_empire"] = false,
            ["is_lithoid_or_infernal_empire"] = false,
            ["is_lithoid_devouring_swarm"] = false,
            ["has_carrier_flag:planet_unity"] = false,
            ["has_carrier_flag:uncapped_energy"] = false,
            ["has_carrier_flag:wilderness_planet"] = false,
            ["has_carrier_flag:uncapped_mining"] = false,
            ["has_carrier_flag:uncapped_farming"] = false
        };
    }

    public (StartupHomeworldSelection Homeworld, StartupBuildingPlan Plan) Select(string templateId, int seed)
    {
        var homeworld = Homeworlds.Select(ConstructionOrdinaryTemplate.Get(templateId).IdealPlanetClass, seed);
        return (homeworld, _plans.TryGetValue((templateId, homeworld.PlanetSize), out var plan)
            ? plan
            : throw new InvalidOperationException("Нет проверенного startup plan для выбранного template/size."));
    }

    public StartupBuildingPlan Exact(string templateId, string planetClass, int planetSize)
    {
        if (!_plans.TryGetValue((templateId, planetSize), out var plan) || plan.Context.PlanetClass != planetClass)
            throw new InvalidOperationException("Нет проверенного startup plan для exact generated template/class/size.");
        return plan;
    }
}

public sealed record M3StartupDistrict(string InstanceId, string DefinitionId, int Level);
public sealed record M3StartupSlot(string InstanceId, string DistrictInstanceId, string SlotType, int Occurrence, string? InitialZoneId);
public sealed record M3StartupZone(string InstanceId, string DistrictInstanceId, string SlotInstanceId, string ZoneId, int SlotNumber);
public sealed record M3StartupBuilding(string InstanceId, string DefinitionId, string? SlotInstanceId, string? ZoneId, int Order, IReadOnlyList<string> CompatibleZoneInstanceIds);
public sealed record M3StartupFeature(string InstanceId, string DefinitionId, bool IsBlocker, bool IsHidden, string? HiddenByBlockerInstanceId, string? HiddenFeatureInstanceId, int Order);

/// <summary>Неизменяемое установленное состояние, созданное выбранным source plan при старте.</summary>
public sealed record M3StartupColony(string ColonyId, int OwnerParticipantId, StartupHomeworldSelection Homeworld, StartupBuildingPlan Plan,
    IReadOnlyList<M3StartupDistrict> Districts, IReadOnlyList<M3StartupSlot> Slots, IReadOnlyList<M3StartupBuilding> Buildings,
    IReadOnlyList<M3StartupZone> Zones, IReadOnlyList<M3StartupFeature> Features)
{
    public static M3StartupColony Create(string colonyId, int ownerParticipantId, StartupHomeworldSelection homeworld, StartupBuildingPlan plan)
    {
        var districts = Array.AsReadOnly(plan.Districts.Select(item => new M3StartupDistrict(colonyId + "/district/" + item.DefinitionId, item.DefinitionId, item.Level)).ToArray());
        var slots = Array.AsReadOnly(plan.Slots.Select(item => new M3StartupSlot(colonyId + "/slot/" + item.DistrictId + "/" + item.SlotType + "/" + item.Occurrence, colonyId + "/district/" + item.DistrictId, item.SlotType, item.Occurrence, item.InitialZoneId)).ToArray());
        var zones = Array.AsReadOnly(plan.Zones.Select(item => new M3StartupZone(colonyId + "/zone/" + item.DistrictId + "/" + item.SlotType + "/" + item.Occurrence,
            colonyId + "/district/" + item.DistrictId,
            colonyId + "/slot/" + item.DistrictId + "/" + item.SlotType + "/" + item.Occurrence, item.ZoneId, item.SlotNumber)).ToArray());
        var buildings = Array.AsReadOnly(plan.Buildings.Select(item => new M3StartupBuilding(colonyId + "/building/" + item.Order, item.DefinitionId,
            item.DistrictId is null || item.SlotNumber is null ? null : slots.FirstOrDefault(slot => slot.DistrictInstanceId == colonyId + "/district/" + item.DistrictId && slot.Occurrence == item.SlotNumber)?.InstanceId,
            item.ZoneId, item.Order, Array.AsReadOnly((item.CompatibleZoneKeys ?? Array.Empty<string>()).Select(key =>
            {
                var parts = key.Split('/');
                return zones.FirstOrDefault(zone => zone.DistrictInstanceId == colonyId + "/district/" + parts[0] && zone.SlotInstanceId == colonyId + "/slot/" + parts[0] + "/" + parts[1] + "/" + parts[2])?.InstanceId;
            }).Where(id => id is not null).Select(id => id!).Distinct(StringComparer.Ordinal).ToArray()))).ToArray());
        var features = Array.AsReadOnly(plan.Features.Select(item => new M3StartupFeature(colonyId + "/" + item.InstanceId, item.DefinitionId, item.IsBlocker, item.IsHidden,
            item.HiddenByBlockerInstanceId is null ? null : colonyId + "/" + item.HiddenByBlockerInstanceId,
            item.HiddenFeatureInstanceId is null ? null : colonyId + "/" + item.HiddenFeatureInstanceId, item.Order)).ToArray());
        return new(colonyId, ownerParticipantId, homeworld, plan, districts, slots, buildings, zones, features);
    }
}
