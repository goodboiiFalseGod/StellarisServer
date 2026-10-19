using System.Text.Json;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class BuildingReplacementProjectionChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var storage = economy.Resources.Where(pair => pair.Value.StoragePolicy != PlanetEconomyStoragePolicy.Unspecified)
            .ToDictionary(pair => pair.Key, pair => new ConstructionStorage(pair.Value.StoragePolicy == PlanetEconomyStoragePolicy.UnlimitedReserve, pair.Value.SourceMaximum));
        var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = economy.Resources.Keys.ToDictionary(id => id, _ => 10000m) }, storage);
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("building-replacement-view-" + preset, 1, home.Homeworld, home.Plan);
            var initial = content.Create(startup, profile.CreateConstructionContributions(startup));
            var binding = M3ConstructionContent.FromBinding.ActingCountry(false);
            var adapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get(preset), fromBinding: binding);
            var factory = initial.Installed.Values.Single(item => item.DefinitionId == "building_factory_1");
            var slot = initial.Slots[factory.SlotId];
            var disabled = initial.SetBuildingEnabled("fixture-disable", 1, initial.Revision, slot.Id, factory.DefinitionId, false).State;
            var target = content.Content.Quote("building_foundry_1", content.Context(startup, ConstructionOrdinaryTemplate.Get(preset), disabled, binding),
                0, 0, M3ConstructionContent.EconomyFacts(startup)).Quote ?? throw new InvalidOperationException("Нет source target quote");
            // Только trusted Core fixture: permission и двухдневный срок не заявляются native.
            var quote = new ConstructionQuote(target.ContentProfileId, "building-replacement-view-test-only", target.DefinitionId, target.Kind,
                2m, target.Costs, target.InfrastructureUpkeep, target.JobCapacity, target.Gates, target.PlanetLimit);
            var accepted = disabled.Enqueue("fixture-replace", 1, disabled.Revision, "replace-order", slot.Id, quote, bank,
                buildingReplacement: new(factory.DefinitionId, slot.ZoneInstanceId!));
            Check(accepted.Accepted, "Core fixture не принята: " + accepted.Code);
            var pending = accepted.State;
            var view = adapter.Project(pending, 1, bank: bank);
            var order = view.Queue.Single(item => item.OrderId == "replace-order");
            Check(order.BuildingReplacement is { SourceDefinitionId: "building_factory_1" } && order.BuildingReplacement.ZoneInstanceId == slot.ZoneInstanceId
                && order.UpgradeFromDefinitionId is null && order.ZoneReplacement is null && order.DefinitionId == "building_foundry_1",
                "Replacement DTO смешан с upgrade/zone или потерял точный source");
            Check(view.Installed.Single(item => item.SlotId == slot.Id) is { DefinitionId: "building_factory_1", Enabled: false }, "Pending projection потеряла старое enabled");
            var json = JsonSerializer.Serialize(order, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using (var document = JsonDocument.Parse(json))
                Check(document.RootElement.GetProperty("buildingReplacement").GetProperty("zoneInstanceId").GetString() == slot.ZoneInstanceId,
                    "Wire projection потеряла binding");
            var cancel = pending.Cancel("fixture-cancel", 1, pending.Revision, "replace-order", bank);
            Check(cancel.Accepted && adapter.Project(cancel.State, 1).Installed.Single(item => item.SlotId == slot.Id) is { DefinitionId: "building_factory_1", Enabled: false }
                && adapter.Project(cancel.State, 1).Queue.Single(item => item.OrderId == "replace-order").Status == "cancelled", "Cancel projection не сохранила старое здание");
            var speeds = new Dictionary<string, decimal?> { ["replace-order"] = 0m };
            var eligibility = new Dictionary<string, bool?> { ["replace-order"] = true };
            var dayOne = pending.AdvanceDay("fixture-day-1", pending.Revision, 1, speeds, eligibility, bank);
            Check(dayOne.Accepted && adapter.Project(dayOne.State, 1).Installed.Single(item => item.SlotId == slot.Id).DefinitionId == "building_factory_1", "Ранний replacement DTO");
            var dayTwo = dayOne.State.AdvanceDay("fixture-day-2", dayOne.State.Revision, 1, speeds, eligibility, bank);
            var completed = adapter.Project(dayTwo.State, 1);
            Check(dayTwo.Accepted && completed.Installed.Single(item => item.SlotId == slot.Id) is { DefinitionId: "building_foundry_1", Enabled: true }
                && completed.Queue.Single(item => item.OrderId == "replace-order").Status == "completed", "Completion не сохранил slot/new enabled");
            Check(adapter.Execute(initial, bank, new(1, true, true, true), new("unsupported", "enqueueBuildingReplacement", ColonyId: initial.ColonyId,
                ExpectedColonyRevision: initial.Revision, SlotId: slot.Id, DefinitionId: target.DefinitionId, SourceDefinitionId: factory.DefinitionId,
                ZoneInstanceId: slot.ZoneInstanceId, OrderId: "unsupported-order")).Code == "INVALID_COMMAND", "Projection открыла production command без permission");
        }
        Console.WriteLine("PASS B01 replacement projection: source 3 presets, trusted Core pending/disabled/cancel/completion, exact wire binding; production command закрыта");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
