using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class ZoneChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        var traits = new StartupTraitWorkforceImporter().Import(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root), traits.BonusesByTemplate,
            new TechnologyImporter().Import(root).CategoryParents);
        var binding = M3ConstructionContent.FromBinding.ActingCountry(false);
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("zone-" + preset, 1, home.Homeworld, home.Plan);
            var initial = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(preset);
            var production = new M3ConstructionAdapter(content, startup, template, fromBinding: binding);
            // Явная fixture сохраняет нулевую скорость именно по принятому BuildingSpeedFraction.
            var adapter = new M3ConstructionAdapter(content, startup, template, new("zone-test-only", -.15m, 0, 0, 0), binding);
            var storage = economy.Resources.Where(pair => pair.Value.StoragePolicy != PlanetEconomyStoragePolicy.Unspecified)
                .ToDictionary(pair => pair.Key, pair => new ConstructionStorage(pair.Value.StoragePolicy == PlanetEconomyStoragePolicy.UnlimitedReserve, pair.Value.SourceMaximum));
            var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
                { [1] = economy.Resources.Keys.ToDictionary(id => id, _ => 10000m) }, storage);
            var actor = new M3ConstructionActor(1, true, true, true);
            Check(initial.Slots.Values.Count(slot => slot.Kind == ConstructionKind.Zone && slot.ZoneSlotDefinitionId is "slot_energy" or "slot_minerals" or "slot_food") == 3, "Stacked levels размножили rural zone slots");
            foreach (var (district, zone, job) in new[] { ("district_generator", "zone_energy", "technician"), ("district_mining", "zone_minerals", "miner"), ("district_farming", "zone_food", "farmer") })
            {
                var state = initial;
                var slot = state.Slots.Values.Single(slot => slot.Kind == ConstructionKind.Zone && slot.DistrictInstanceId == state.ColonyId + "/district/" + district);
                var context = content.Context(startup, template, state, binding);
                var unlocked = M3ZoneGraph.Eligibility(content, slot, context);
                Check(unlocked.Gate is null && unlocked.Requirements.Count == 1, "Source civic unlock/requirement потерян");
                ConstructionContext CivicContext(bool unknown) => new(context.OwnerExists, context.OwnerFacts, context.FromNomadic, context.PlanetClass,
                    context.Modifiers, context.CarrierFlags, [], context.BuildingCounts, new("standard", "default", [], validCivics: unknown ? null : []));
                Check(M3ZoneGraph.Eligibility(content, slot, CivicContext(false)).Gate == "zone-slot-locked", "Отсутствующие tech/civic открыли место");
                Check(M3ZoneGraph.Eligibility(content, slot, CivicContext(true)).Gate == "zone-slot-condition-unresolved", "Unknown civic превратился в false");
                if (zone == "zone_food")
                {
                    Check(content.Content.Quote(zone, CivicContext(true), -.15m, 0, M3ConstructionContent.EconomyFacts(startup)).Code == "eligibility-unresolved",
                        "Unknown if limit выбрал else food branch");
                    var wildernessFacts = context.OwnerFacts.ToDictionary(); wildernessFacts[ConstructionOwnerPredicate.Wilderness] = true;
                    var wilderness = new ConstructionContext(true, wildernessFacts, false, context.PlanetClass, context.Modifiers, context.CarrierFlags,
                        context.Technologies, context.BuildingCounts, context.SourceFacts);
                    Check(content.Content.Quote(zone, wilderness, -.15m, 0, M3ConstructionContent.EconomyFacts(startup)).Code == "not-eligible",
                        "Food else owner? потерял wilderness exclusion");
                }
                var productionQuote = production.Quote(state, zone, slot.Id);
                var buildingModifiers = content.PresetModifiers(template, ConstructionKind.Building);
                var projectedQuote = production.Project(state, 1, bank: bank).Catalog.Single(item => item.DefinitionId == zone).Quote;
                Check(productionQuote.Quote is { } sourceQuote
                    && sourceQuote.Costs["minerals"] == 1000m * (1 + buildingModifiers.Cost)
                    && sourceQuote.RequiredWork == 360
                    && projectedQuote is { } projected
                    && projected.DurationDays == 360m / (1 + buildingModifiers.Speed),
                    "Production zone quote не использует source modifiers здания " + preset + "/" + zone + ": " + productionQuote.Code);
                Check(adapter.Quote(state, zone == "zone_energy" ? "zone_food" : "zone_energy", slot.Id).Code == "zone-set-excluded", "Чужая специализация прошла slot set");
                var quote = adapter.Quote(state, zone, slot.Id).Quote ?? throw new Exception("Source zone quote failed: " + adapter.Quote(state, zone, slot.Id).Code);
                Check(quote.RequiredWork == 360 && quote.Costs["minerals"] == 850
                    && quote.JobCapacity[job] == 100 * M3ZoneGraph.AttachedLevel(startup, state, slot.DistrictInstanceId!), "Source zone work/price/attached jobs расходится");
                var command = new M3ClientCommand("zone-enqueue", "enqueueConstruction", ColonyId: state.ColonyId, ExpectedColonyRevision: state.Revision,
                    DefinitionId: zone, SlotId: slot.Id, OrderId: "zone-order");
                var accepted = adapter.Execute(state, bank, actor, command);
                Check(accepted.Accepted && accepted.ResourceDeltas.Single().Amount == -850 && accepted.State.Slots.Count == state.Slots.Count
                    && M3ZoneGraph.Current(startup, accepted.State).Count == startup.Zones.Count, "Pay/reserve дал ранние effects/slots");
                Check(adapter.Execute(accepted.State, bank, actor, command).Code == "duplicate", "Zone enqueue replay");
                var faster = new M3ConstructionAdapter(content, startup, template, new("zone-live-speed-test-only", -.15m, .5m, 0, 0), binding);
                var speedChanged = faster.AdvanceDay(accepted.State, bank, "zone-faster-day");
                Check(speedChanged.Accepted && speedChanged.State.Orders["zone-order"].CompletedWork == 1.5m
                    && speedChanged.State.Orders["zone-order"].Quote == accepted.State.Orders["zone-order"].Quote, "Fixture live speed изменила fixed quote или не изменила прирост");
                Check(adapter.Execute(state, bank, actor with { ParticipantId = 2 }, command).Code == "OUT_OF_SCOPE", "Чужой enqueue");
                var cancelled = adapter.Execute(accepted.State, bank, actor, new("zone-cancel", "cancelConstruction", ColonyId: state.ColonyId,
                    ExpectedColonyRevision: accepted.State.Revision, OrderId: "zone-order"));
                Check(cancelled.Accepted && cancelled.ResourceDeltas.Single().Amount == 850 && adapter.Quote(cancelled.State, zone, slot.Id).Quote is not null,
                    "Cancel не вернул оплату/место");
                var world = profile.CreateWorld(); var country = world.CreateCountry(); var planet = world.CreatePlanet(country);
                profile.InitializeColony(world, country, planet, preset, home.Plan);
                var before = profile.RefreshInstalledGraph(world, planet, startup, accepted.State, preset)[job];
                var baseSlots = world.GetInstalledZoneBuildingSlotsAdd(planet);
                var beforeUpkeep = world.PreviewPlanetReport(planet).ZoneUpkeep.ToDictionary();
                state = accepted.State;
                for (var day = 0; day < 360; day++)
                {
                    var result = adapter.AdvanceDay(state, bank, "zone-day-" + day);
                    Check(result.Accepted, "Zone daily: " + result.Code); state = result.State;
                    if (day == 358) Check(state.Slots.Count == initial.Slots.Count && state.Installed.Count == initial.Installed.Count, "Раннее completion");
                }
                var zoneInstance = M3ZoneGraph.Current(startup, state).Single(item => item.ZoneId == zone);
                Check(state.Orders["zone-order"].Status == ConstructionOrderStatus.Completed && state.Slots.Count == initial.Slots.Count + 3
                    && state.Slots.Values.Count(item => item.ZoneInstanceId == zoneInstance.InstanceId) == 3, "Completion не создал ровно3 принадлежащих места");
                var replay = adapter.AdvanceDay(state, bank, "zone-day-359");
                Check(!replay.Accepted && replay.Code == "command-id-conflict" && replay.State.Slots.Count == state.Slots.Count
                    && M3ZoneGraph.Materialize(content, startup, state).Slots.Count == state.Slots.Count, "Повтор дня/материализации размножил slots");
                var capacity = profile.RefreshInstalledGraph(world, planet, startup, state, preset)[job];
                var level = M3ZoneGraph.AttachedLevel(startup, state, slot.DistrictInstanceId!);
                Check(capacity == before + 100 * level && world.GetInstalledZoneBuildingSlotsAdd(planet) == baseSlots + 3,
                    "Zone effects не соответствуют attached level/static slots");
                profile.RefreshInstalledGraph(world, planet, startup, state, preset);
                Check(world.GetInstalledJobCapacity(planet)![job] == capacity, "Повтор graph refresh удвоил jobs");
                world.AdvanceMonth();
                Check(world.GetPlanetReport(planet).ZoneUpkeep.OrderBy(pair => pair.Key).SequenceEqual(beforeUpkeep.OrderBy(pair => pair.Key)),
                    "Source зона без upkeep изменила monthly zone upkeep");
                var view = adapter.Project(state, 1, bank: bank);
                Check(view.StartupZones!.Single(item => item.InstanceId == zoneInstance.InstanceId).DistrictInstanceId == slot.DistrictInstanceId
                    && view.Slots.Count(item => item.ZoneInstanceId == zoneInstance.InstanceId) == 3
                    && view.Installed.Single(item => item.SlotId == slot.Id).JobCapacity[job] == 100 * level, "Zone graph DTO потерял ownership/attached effects");
                var districtEntry = view.Catalog.Single(item => item.DefinitionId == district);
                var next = adapter.Execute(state, bank, actor, new("district-after-zone", "enqueueConstruction", ColonyId: state.ColonyId,
                    ExpectedColonyRevision: state.Revision, DefinitionId: district, SlotId: districtEntry.SlotIds[0], OrderId: "district-after-zone"));
                Check(next.Accepted, "District после zone completion: " + next.Code); state = next.State;
                for (var day = 0; day < next.State.Orders["district-after-zone"].Quote.RequiredWork; day++)
                {
                    var advanced = adapter.AdvanceDay(state, bank, "district-zone-day-" + day);
                    Check(advanced.Accepted, "District attached-level refresh: " + advanced.Code); state = advanced.State;
                }
                var increased = adapter.Project(state, 1, bank: bank);
                Check(state.Slots.Count == initial.Slots.Count + 3 && M3ZoneGraph.Current(startup, state).Count == startup.Zones.Count + 1
                    && increased.Installed.Single(item => item.SlotId == slot.Id).JobCapacity[job] == 100 * (level + 1),
                    "Новый district level размножил zone/slots либо заморозил её effects");
                Check(profile.RefreshInstalledGraph(world, planet, startup, state, preset)[job]
                    == capacity + next.State.Orders["district-after-zone"].Quote.JobCapacity[job] + 100,
                    "Source graph не пересчитал per-level zone jobs после нового уровня");
            }
        }
        Console.WriteLine("PASS C02: source rural slots/unlock, building speed/cost quote, explicit lifecycle fixture, atomic3slots, attached jobs/month");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
