using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class DemolitionChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var fields = economy.Buildings.Keys.Select(id => content.Content.Catalogs[id].Definitions[id].Fields.Where(field => field.Key == "can_demolish").ToArray()).ToArray();
        Check(fields.Count(items => items is [{ Scalar: "yes" }]) == 0 && fields.Count(items => items is [{ Scalar: "no" }]) == 9
            && fields.Count(items => items.Length == 0) == 156, "Source demolition inventory изменился");
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("demolition-" + preset, 1, home.Homeworld, home.Plan);
            var state = content.Create(startup, profile.CreateConstructionContributions(startup));
            var adapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get(preset));
            var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = new Dictionary<string, decimal>() }, new Dictionary<string, ConstructionStorage>());
            var actor = new M3ConstructionActor(1, true, true, true);
            Check(adapter.Project(state, 1).Installed.Where(item => item.Kind == "building").All(item => item.Demolition is { CanDemolish: false }), "Unknown открыл demolition");
            foreach (var building in state.Installed.Values.Where(item => item.Kind == ConstructionKind.Building))
            {
                var command = new M3ClientCommand("demolish-" + building.DefinitionId, "demolishBuilding", ColonyId: state.ColonyId,
                    ExpectedColonyRevision: state.Revision, DefinitionId: building.DefinitionId, SlotId: building.SlotId, ConfirmDemolition: true);
                var result = adapter.Execute(state, bank, actor, command);
                var explicitNo = content.Content.Catalogs[building.DefinitionId].Definitions[building.DefinitionId].Fields.Any(field => field.Key == "can_demolish" && field.Scalar == "no");
                Check(!result.Accepted && result.Code == (explicitNo ? "building-demolition-forbidden" : "building-demolition-permission-unresolved")
                    && result.State == state && result.ResourceDeltas.Count == 0, "Permission отказ изменил граф/банк");
                Check(adapter.Execute(state, bank, actor, command with { ConfirmDemolition = false }).Code == "DEMOLITION_CONFIRMATION_REQUIRED", "Нет отдельного confirm");
                Check(adapter.Execute(state, bank, actor, command with { ConfirmDemolition = null }).Code == "INVALID_COMMAND", "Missing confirm");
                Check(adapter.Execute(state, bank, actor, command with { OrderId = "hidden-order" }).Code == "INVALID_COMMAND", "Demolition принял orderId");
                Check(adapter.Execute(state, bank, actor with { ParticipantId = 2 }, command).Code == "OUT_OF_SCOPE", "Foreign demolition");
                Check(adapter.Execute(state, bank, actor, command with { ExpectedColonyRevision = state.Revision + 1 }).Code == "STALE_REVISION", "Stale demolition");
            }
            // Core fixture обходит только native permission, чтобы проверить принятый lifecycle.
            var target = state.Installed.Values.First(item => item.Kind == ConstructionKind.Building && item.DefinitionId != "building_capital");
            var world = profile.CreateWorld(); var country = world.CreateCountry(); var planet = world.CreatePlanet(country);
            profile.InitializeColony(world, country, planet, preset, home.Plan);
            var original = profile.RefreshInstalledGraph(world, planet, startup, state, preset);
            var upkeep = world.PreviewPlanetReport(planet).BuildingUpkeep;
            var removed = state.DemolishBuilding("fixture-remove", 1, state.Revision, target.SlotId, target.DefinitionId);
            Check(removed.Accepted && removed.ResourceDeltas.Count == 0 && removed.State.Orders.Count == state.Orders.Count
                && removed.State.Slots.ContainsKey(target.SlotId) && !removed.State.Installed.ContainsKey(target.SlotId), "Removal потерял slot/создал refund/order");
            var after = profile.RefreshInstalledGraph(world, planet, startup, removed.State, preset);
            var removedView = adapter.Project(removed.State, 1);
            Check(removedView.Installed.All(item => item.SlotId != target.SlotId)
                && removedView.StartupBuildings!.All(item => item.SlotInstanceId != target.SlotId)
                && removedView.Slots.Single(item => item.SlotId == target.SlotId).InstalledDefinitionId is null,
                "Projection сохранила удалённый startup instance или потеряла свободное место");
            foreach (var job in target.JobCapacity) Check(after.GetValueOrDefault(job.Key) == original.GetValueOrDefault(job.Key) - job.Value, "Jobs не сняты ровно один раз");
            foreach (var resource in target.InfrastructureUpkeep) Check(world.PreviewPlanetReport(planet).BuildingUpkeep.GetValueOrDefault(resource.Key) == upkeep.GetValueOrDefault(resource.Key) - resource.Value, "Upkeep не снят");
            var repeat = profile.RefreshInstalledGraph(world, planet, startup, removed.State, preset);
            Check(repeat.OrderBy(pair => pair.Key).SequenceEqual(after.OrderBy(pair => pair.Key)), "Повтор refresh изменил contributions");
            Check(removed.State.DemolishBuilding("fixture-remove", 1, state.Revision, target.SlotId, target.DefinitionId).Code == "duplicate", "Removal replay");
            Check(removed.State.DemolishBuilding("fixture-remove", 1, state.Revision, target.SlotId, "other").Code == "command-id-conflict", "Removal conflict");
            Check(removed.State.DemolishBuilding("again", 1, removed.State.Revision, target.SlotId, target.DefinitionId).Code == "building-target-mismatch", "Double removal");
            Check(state.DemolishBuilding("foreign", 2, state.Revision, target.SlotId, target.DefinitionId).Code == "not-owner", "Core foreign");
            Check(state.DemolishBuilding("stale", 1, state.Revision + 1, target.SlotId, target.DefinitionId).Code == "stale-revision", "Core stale");
            var quote = new ConstructionQuote("fixture", "fixture", "replacement", ConstructionKind.Building, 1,
                new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), []);
            var pending = state.Enqueue("upgrade", 1, state.Revision, "related-order", target.SlotId, quote, bank,
                upgradeFrom: target.DefinitionId, upgradeZoneInstanceId: state.Slots[target.SlotId].ZoneInstanceId).State;
            var locked = pending.DemolishBuilding("locked", 1, pending.Revision, target.SlotId, target.DefinitionId);
            Check(!locked.Accepted && locked.Code == "building-order-cancel-required" && locked.State == pending
                && locked.ResourceDeltas.Count == 0 && pending.Orders["related-order"].Status == ConstructionOrderStatus.Pending, "Demolition скрыто отменил order");
            var cancelled = pending.Cancel("cancel", 1, pending.Revision, "related-order", bank).State;
            Check(cancelled.DemolishBuilding("after-cancel", 1, cancelled.Revision, target.SlotId, target.DefinitionId).Accepted, "Отдельный cancel не освободил demolition");
        }
        Console.WriteLine("PASS B03: source no/Unknown, confirmation/authority/stale, trusted Core removal/no-refund/slot/jobs/upkeep/replay/pending lock");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
