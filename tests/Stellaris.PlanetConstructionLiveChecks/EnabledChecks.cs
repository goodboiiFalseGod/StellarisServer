using System.Text.Json;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class EnabledChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        CoreLifecycle();
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var flags = economy.Buildings.Keys.Select(id => content.Content.Catalogs[id].Definitions[id].Fields.Where(field => field.Key == "can_be_disabled").ToArray()).ToArray();
        Check(flags.Count(fields => fields is [{ Scalar: "yes" }]) == 0 && flags.Count(fields => fields is [{ Scalar: "no" }]) == 12
            && flags.Count(fields => fields.Length == 0) == 153, "4.5.2 ordinary permission inventory изменился");
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("enabled-" + preset, 1, home.Homeworld, home.Plan);
            var state = content.Create(startup, profile.CreateConstructionContributions(startup));
            var adapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get(preset), fromBinding: M3ConstructionContent.FromBinding.ActingCountry(false));
            var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = new Dictionary<string, decimal>() }, new Dictionary<string, ConstructionStorage>());
            var actor = new M3ConstructionActor(1, true, true, true);
            var view = adapter.Project(state, 1);
            Check(view.Installed.Where(item => item.Kind == "building").All(item => item.Enabled == true
                && item.Toggle is { TargetEnabled: false } toggle && toggle.CanToggle == (item.DefinitionId == "building_factory_1")),
                "Initial enabled/capabilities потеряны или Unknown открыл toggle");
            foreach (var item in state.Installed.Values.Where(item => item.Kind == ConstructionKind.Building))
            {
                var command = new M3ClientCommand("toggle-" + item.DefinitionId, "setBuildingEnabled", ColonyId: state.ColonyId, ExpectedColonyRevision: state.Revision,
                    DefinitionId: item.DefinitionId, SlotId: item.SlotId, Enabled: false);
                var result = adapter.Execute(state, bank, actor, command);
                var explicitNo = content.Content.Catalogs[item.DefinitionId].Definitions[item.DefinitionId].Fields.Any(field => field.Key == "can_be_disabled" && field.Scalar == "no");
                if (item.DefinitionId == "building_factory_1")
                {
                    Check(!explicitNo && result.Accepted && result.State.Installed[item.SlotId] is { Enabled: false, DefinitionId: "building_factory_1" }
                        && result.ResourceDeltas.Count == 0 && result.State.PendingOrderIds.Count == 0, "Разрешённая factory toggle не изменила только enabled state");
                    var factoryView = adapter.Project(result.State, 1).Installed.Single(installed => installed.SlotId == item.SlotId);
                    Check(factoryView.Enabled == false && factoryView.Toggle is { CanToggle: true, TargetEnabled: true }
                        && factoryView.JobCapacity.Count == 0 && factoryView.InfrastructureUpkeep.Count == 0, "Factory off DTO не отражает authoritative state");
                    continue;
                }
                Check(!result.Accepted && result.Code == (explicitNo ? "building-disable-forbidden" : "building-disable-permission-unresolved")
                    && result.State == state && result.ResourceDeltas.Count == 0, "Native permission no/Unknown изменил state");
                Check(adapter.Execute(state, bank, actor with { ParticipantId = 2 }, command).Code == "OUT_OF_SCOPE", "Foreign toggle");
                Check(adapter.Execute(state, bank, actor, command with { ExpectedColonyRevision = state.Revision + 1 }).Code == "STALE_REVISION", "Stale toggle");
                Check(adapter.Execute(state, bank, actor, command with { Enabled = null }).Code == "INVALID_COMMAND", "Missing desired enabled");
            }
            // Доверенная state fixture обходит только native permission для проверки принятых effect/inheritance правил.
            var capital = state.Installed.Values.Single(item => item.DefinitionId == "building_capital");
            var world = profile.CreateWorld(); var country = world.CreateCountry(); var planet = world.CreatePlanet(country);
            profile.InitializeColony(world, country, planet, preset, home.Plan);
            var initialCapacity = profile.RefreshInstalledGraph(world, planet, startup, state, preset);
            var initialUpkeep = world.PreviewPlanetReport(planet).BuildingUpkeep;
            var initialOutput = world.GetInstalledJobEconomyEffects(planet)!.OutputMultiplier;
            var factory = state.Installed.Values.SingleOrDefault(item => item.DefinitionId == "building_factory_1");
            if (factory is not null)
            {
                var factoryOff = adapter.Execute(state, bank, actor, new M3ClientCommand("factory-off", "setBuildingEnabled", ColonyId: state.ColonyId,
                    ExpectedColonyRevision: state.Revision, DefinitionId: factory.DefinitionId, SlotId: factory.SlotId, Enabled: false));
                Check(factoryOff.Accepted && factoryOff.State.Installed[factory.SlotId].Enabled == false, "Source factory off не принят через adapter");
                var disabledFactoryCapacity = profile.RefreshInstalledGraph(world, planet, startup, factoryOff.State, preset);
                foreach (var job in factory.JobCapacity) Check(disabledFactoryCapacity.GetValueOrDefault(job.Key) == initialCapacity.GetValueOrDefault(job.Key) - job.Value,
                    "Factory off не убрал source jobs ровно один раз");
                foreach (var upkeep in factory.InfrastructureUpkeep) Check(world.PreviewPlanetReport(planet).BuildingUpkeep.GetValueOrDefault(upkeep.Key) == initialUpkeep.GetValueOrDefault(upkeep.Key) - upkeep.Value,
                    "Factory off не убрал source upkeep");
                var factoryOn = adapter.Execute(factoryOff.State, bank, actor, new M3ClientCommand("factory-on", "setBuildingEnabled", ColonyId: state.ColonyId,
                    ExpectedColonyRevision: factoryOff.State.Revision, DefinitionId: factory.DefinitionId, SlotId: factory.SlotId, Enabled: true));
                var restoredFactoryCapacity = profile.RefreshInstalledGraph(world, planet, startup, factoryOn.State, preset);
                Check(factoryOn.Accepted && factoryOn.State.Installed[factory.SlotId].Enabled == true
                    && restoredFactoryCapacity.OrderBy(pair => pair.Key).SequenceEqual(initialCapacity.OrderBy(pair => pair.Key))
                    && world.PreviewPlanetReport(planet).BuildingUpkeep.OrderBy(pair => pair.Key).SequenceEqual(initialUpkeep.OrderBy(pair => pair.Key)),
                    "Factory on не восстановил исходные source jobs/upkeep ровно один раз");
            }
            var disabled = state.SetBuildingEnabled("fixture-disable", 1, state.Revision, capital.SlotId, capital.DefinitionId, false);
            Check(disabled.Accepted && disabled.ResourceDeltas.Count == 0 && disabled.State.PendingOrderIds.Count == 0, "Toggle создал оплату/заказ");
            var disabledCapacity = profile.RefreshInstalledGraph(world, planet, startup, disabled.State, preset);
            foreach (var job in capital.JobCapacity) Check(disabledCapacity.GetValueOrDefault(job.Key) == initialCapacity.GetValueOrDefault(job.Key) - job.Value, "Disabled не убрал jobs ровно один раз");
            foreach (var upkeep in capital.InfrastructureUpkeep) Check(world.PreviewPlanetReport(planet).BuildingUpkeep.GetValueOrDefault(upkeep.Key) == initialUpkeep.GetValueOrDefault(upkeep.Key) - upkeep.Value, "Disabled не убрал building upkeep");
            Check(world.GetInstalledJobEconomyEffects(planet)!.OutputMultiplier < initialOutput, "Disabled capital сохранил global job effect");
            var disabledView = adapter.Project(disabled.State, 1).Installed.Single(item => item.SlotId == capital.SlotId);
            Check(disabledView.Enabled == false && disabledView.JobCapacity.Count == 0 && disabledView.InfrastructureUpkeep.Count == 0, "Disabled DTO показывает active contributions");
            var enabled = disabled.State.SetBuildingEnabled("fixture-enable", 1, disabled.State.Revision, capital.SlotId, capital.DefinitionId, true);
            var restored = profile.RefreshInstalledGraph(world, planet, startup, enabled.State, preset);
            Check(restored.OrderBy(pair => pair.Key).SequenceEqual(initialCapacity.OrderBy(pair => pair.Key))
                && world.PreviewPlanetReport(planet).BuildingUpkeep.OrderBy(pair => pair.Key).SequenceEqual(initialUpkeep.OrderBy(pair => pair.Key))
                && world.GetInstalledJobEconomyEffects(planet)!.OutputMultiplier == initialOutput, "Re-enable удвоил либо потерял contributions");
            var citySlot = state.Slots.Values.First(slot => slot.Kind == ConstructionKind.Building && !state.Installed.ContainsKey(slot.Id));
            var capBuilding = new ConstructionInstalled(citySlot.Id, "building_generator_districts_1", ConstructionKind.Building, new Dictionary<string, decimal>(), new Dictionary<string, decimal>());
            var capped = new ConstructionLiveRuntime(state.ColonyId, 1, state.Slots.Values, state.Installed.Values.Append(capBuilding));
            var context = content.Context(startup, ConstructionOrdinaryTemplate.Get(preset), state);
            var facts = M3ConstructionContent.EconomyFacts(startup);
            var activeCap = M3DistrictCapacity.Evaluate(content, startup, capped, "district_generator", context, facts).Capacity;
            var withoutCap = capped.SetBuildingEnabled("fixture-cap-disable", 1, capped.Revision, capBuilding.SlotId, capBuilding.DefinitionId, false).State;
            Check(activeCap > M3DistrictCapacity.Evaluate(content, startup, withoutCap, "district_generator", context, facts).Capacity, "Disabled building сохранил district capacity modifier");
        }
        Console.WriteLine("PASS B02: source permission no/Unknown, explicit enabled fixture jobs/upkeep/modifiers, upgrade current-state inheritance");
    }

    private static void CoreLifecycle()
    {
        var slot = new ConstructionLiveSlot("slot", ConstructionKind.Building, "zone", "zone-definition");
        ConstructionLiveRuntime Initial() => new("colony", 1, [slot], [new("slot", "old", ConstructionKind.Building, new Dictionary<string, decimal>(), new Dictionary<string, decimal>())]);
        var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = new Dictionary<string, decimal> { ["minerals"] = 100 } }, new Dictionary<string, ConstructionStorage> { ["minerals"] = new(true, null) });
        var quote = new ConstructionQuote("fixture", "fixture", "new", ConstructionKind.Building, 2, new Dictionary<string, decimal> { ["minerals"] = 10 }, new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), []);
        foreach (var desired in new[] { false, true })
        {
            var initial = Initial();
            var pending = initial.Enqueue("upgrade", 1, 0, "order", "slot", quote, bank, upgradeFrom: "old", upgradeZoneInstanceId: "zone").State;
            var day = pending.AdvanceDay("day1", pending.Revision, 1, new Dictionary<string, decimal?> { ["order"] = 0 }, new Dictionary<string, bool?> { ["order"] = true }, bank).State;
            var off = day.SetBuildingEnabled("off", 1, day.Revision, "slot", "old", false);
            Check(off.Accepted && off.State.Orders["order"].Quote == quote && off.ResourceDeltas.Count == 0, "Pending upgrade заблокировал toggle/изменил quote");
            Check(off.State.SetBuildingEnabled("off", 1, day.Revision, "slot", "old", false).Code == "duplicate", "Toggle replay");
            Check(off.State.SetBuildingEnabled("off", 1, day.Revision, "slot", "old", true).Code == "command-id-conflict", "Toggle conflicting replay");
            Check(off.State.SetBuildingEnabled("noop", 1, off.State.Revision, "slot", "old", false).Code == "building-state-unchanged", "No-op toggle создал revision");
            Check(off.State.SetBuildingEnabled("stale", 1, day.Revision, "slot", "old", true).Code == "stale-revision", "Core stale toggle");
            Check(off.State.SetBuildingEnabled("foreign", 2, off.State.Revision, "slot", "old", true).Code == "not-owner", "Core foreign toggle");
            var current = desired ? off.State.SetBuildingEnabled("on", 1, off.State.Revision, "slot", "old", true).State : off.State;
            var cancelled = current.Cancel("cancel", 1, current.Revision, "order", bank);
            Check(cancelled.Accepted && cancelled.State.Installed["slot"].Enabled == desired, "Cancel потерял current enabled");
            var complete = current.AdvanceDay("day2", current.Revision, 1, new Dictionary<string, decimal?> { ["order"] = 0 }, new Dictionary<string, bool?> { ["order"] = true }, bank);
            Check(complete.Accepted && complete.State.Installed["slot"].DefinitionId == "new" && complete.State.Installed["slot"].Enabled == desired, "Upgrade не наследует enabled на completion");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
