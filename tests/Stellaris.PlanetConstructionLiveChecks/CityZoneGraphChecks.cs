using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class CityZoneGraphChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("city-graph-" + preset, 1, home.Homeworld, home.Plan);
            var state = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(preset);
            var binding = M3ConstructionContent.FromBinding.ActingCountry(false);
            var context = content.Context(startup, template, state, binding);
            var adapter = new M3ConstructionAdapter(content, startup, template, fromBinding: binding);
            var view = adapter.Project(state, 1);
            var zones = M3ZoneGraph.Current(startup, state);
            Check(zones.Count == startup.Zones.Count && zones.SequenceEqual(startup.Zones), "Startup/current зоны продублированы или изменили идентичность");
            Check(state.Installed.Values.Count(item => item.Kind == ConstructionKind.Zone) == 2, "Не выделены ровно две городские специализации");
            foreach (var zone in startup.Zones.Where(zone => zone.ZoneId != "zone_default"))
            {
                var slot = state.Slots[zone.SlotInstanceId];
                Check(slot.Kind == ConstructionKind.Zone && slot.ZoneInstanceId == zone.InstanceId && slot.DistrictInstanceId == zone.DistrictInstanceId
                    && state.Installed[slot.Id].DefinitionId == zone.ZoneId, "Потеряна source occurrence");
                var buildings = state.Slots.Values.Where(item => item.Kind == ConstructionKind.Building && item.ZoneInstanceId == zone.InstanceId).ToArray();
                Check(buildings.Length == 3 && buildings.Select(item => item.Id).Order().SequenceEqual(Enumerable.Range(0, 3).Select(index => zone.InstanceId + "/building-slot/" + index).Order()), "Изменены ID тройки мест");
                Check(M3ZoneGraph.Eligibility(content, slot, context).Gate is null, "Source city slot не разрешён известным исходом ветвей");
                Check(view.StartupZones!.Single(item => item.InstanceId == zone.InstanceId).SlotInstanceId == slot.Id
                    && view.Installed.Single(item => item.SlotId == slot.Id).Kind == "zone", "DTO не содержит единую городскую зону");
            }
            var government = startup.Zones.Single(zone => zone.ZoneId == "zone_default");
            Check(!state.Slots.ContainsKey(government.SlotInstanceId) && !state.Installed.Values.Any(item => item.DefinitionId == "zone_default"), "Government mismatch молча материализован");
            var slot02 = state.Slots.Values.Single(item => item.ZoneSlotDefinitionId == "slot_city_02");
            var planetFacts = context.SourceFacts!.PlanetFacts.ToDictionary(); planetFacts[ConstructionPlanetPredicate.UpgradedCapital] = false;
            var noCapital = new ConstructionContext(context.OwnerExists, context.OwnerFacts, context.FromNomadic, context.PlanetClass,
                context.Modifiers, context.CarrierFlags, context.Technologies, context.BuildingCounts,
                new("standard", "default", template.Civics, [], [], startup.Features.Select(item => item.DefinitionId), planetFacts, template.Civics));
            Check(M3ZoneGraph.Eligibility(content, slot02, noCapital).Gate == "zone-slot-condition-unresolved", "Unknown primitive заменён default false");
            var world = profile.CreateWorld(); var country = world.CreateCountry(); var planet = world.CreatePlanet(country);
            profile.InitializeColony(world, country, planet, preset, home.Plan);
            var capacity = world.GetInstalledJobCapacity(planet)!.ToDictionary();
            var upkeep = world.PreviewPlanetReport(planet).ZoneUpkeep.ToDictionary();
            var buildingSlots = world.GetInstalledZoneBuildingSlotsAdd(planet);
            var refreshed = profile.RefreshInstalledGraph(world, planet, startup, state, preset);
            Check(refreshed.OrderBy(item => item.Key).SequenceEqual(capacity.OrderBy(item => item.Key))
                && world.PreviewPlanetReport(planet).ZoneUpkeep.OrderBy(item => item.Key).SequenceEqual(upkeep.OrderBy(item => item.Key))
                && world.GetInstalledZoneBuildingSlotsAdd(planet) == buildingSlots, "Миграция удвоила jobs/upkeep/slots");
            Check(M3ZoneGraph.Materialize(content, startup, state).Slots.Count == state.Slots.Count, "Повторная материализация меняет graph");
            Check(M3ZoneGraph.BuildingFits(content, "building_factory_1", "zone_industrial"), "Factory не подходит исходной industrial");
            Check(!M3ZoneGraph.BuildingFits(content, "building_factory_1", "zone_foundry"), "Factory ошибочно подходит foundry");
            Check(M3ZoneGraph.BuildingFits(content, "building_foundry_1", "zone_foundry"), "Foundry не подходит foundry: " + string.Join(",", content.Content.Economy.Zones["zone_foundry"].IncludedBuildingSets));
            Check(content.Content.Quote("zone_foundry", context, -.15m, null, M3ConstructionContent.EconomyFacts(startup)).Code == "numerical-modifiers-unresolved", "Миграция открыла неизвестную native скорость");
        }
        Console.WriteLine("PASS C03 prerequisite: canonical city01/02 instances/3slots/current projection/economy dedup, source sets; government and native speed gated");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
