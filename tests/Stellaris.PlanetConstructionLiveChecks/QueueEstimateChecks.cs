using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class QueueEstimateChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var binding = M3ConstructionContent.FromBinding.ActingCountry(false);
        var storage = economy.Resources.Where(pair => pair.Value.StoragePolicy != PlanetEconomyStoragePolicy.Unspecified)
            .ToDictionary(pair => pair.Key, pair => new ConstructionStorage(pair.Value.StoragePolicy == PlanetEconomyStoragePolicy.UnlimitedReserve, pair.Value.SourceMaximum));
        var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = economy.Resources.Keys.ToDictionary(id => id, _ => 10000m) }, storage);
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("queue-estimate-" + preset, 1, home.Homeworld, home.Plan);
            var initial = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(preset);
            var adapter = new M3ConstructionAdapter(content, startup, template, fromBinding: binding);
            var free = initial.Slots.Values.Where(slot => slot.Kind == ConstructionKind.District && !initial.Installed.ContainsKey(slot.Id)).Take(2).ToArray();
            var actor = new M3ConstructionActor(1, true, true, true);
            var first = adapter.Execute(initial, bank, actor, new("estimate-a", "enqueueConstruction", ColonyId: initial.ColonyId,
                ExpectedColonyRevision: initial.Revision, DefinitionId: "district_city", SlotId: free[0].Id, OrderId: "order-a"));
            Check(first.Accepted, "Source district для estimate: " + first.Code);
            var second = adapter.Execute(first.State, bank, actor, new("estimate-b", "enqueueConstruction", ColonyId: initial.ColonyId,
                ExpectedColonyRevision: first.State.Revision, DefinitionId: "district_city", SlotId: free[1].Id, OrderId: "order-b"));
            Check(second.Accepted, "Второй source order: " + second.Code);
            var step = adapter.AdvanceDay(second.State, bank, "first-day");
            Check(step.Accepted, "Дневная работа estimate fixture");
            var state = step.State;
            var original = state.Orders["order-a"];
            var before = adapter.Project(state, 1, bank: bank);
            var active = before.Queue.Single(order => order.OrderId == "order-a");
            var waiting = before.Queue.Single(order => order.OrderId == "order-b");
            Check(active is { ExecutionState: "active", WorkEstimatePolicyId: "accepted-daily-work-v1", DailyWork: 1m }
                && active.RemainingWorkDays == decimal.Ceiling(original.Quote.RequiredWork - original.CompletedWork)
                && waiting.ExecutionState == "waiting" && waiting.RemainingWorkDays == waiting.RequiredWork, "Source active/waiting estimate");
            M3ConstructionEvaluation Evaluation(decimal districtSpeed) => new(content.Context(startup, template, state, binding),
                M3ConstructionContent.EconomyFacts(startup), new("estimate-test-only", -.15m, .15m, districtSpeed), binding);
            var faster = adapter.Project(state, 1, evaluation: Evaluation(.5m), bank: bank).Queue.Single(order => order.OrderId == "order-a");
            Check(faster.DailyWork == 1.5m && faster.RemainingWorkDays == decimal.Ceiling((active.RequiredWork - active.CompletedWork) / 1.5m)
                && faster.PaidCosts.OrderBy(pair => pair.Key).SequenceEqual(active.PaidCosts.OrderBy(pair => pair.Key))
                && faster.RequiredWork == active.RequiredWork && faster.CompletedWork == active.CompletedWork
                && ReferenceEquals(state.Orders["order-a"], original), "Live estimate изменила fixed quote/work/runtime");
            Check(adapter.Project(state, 1, activeCapacity: 2).Queue.Where(order => order.Status == "pending").All(order => order.ExecutionState == "active"), "N2 states");
            Check(adapter.Project(state, 1, activeCapacity: 0).Queue.Where(order => order.Status == "pending")
                .All(order => order.ExecutionState == "paused" && order.RemainingWorkDays is null), "N0 обещает дату completion");
            foreach (var speed in new[] { -1m, decimal.MaxValue, -.9999999999999999999999999999m })
            {
                var invalid = adapter.Project(state, 1, evaluation: Evaluation(speed)).Queue.Single(order => order.OrderId == "order-a");
                Check(invalid.DailyWork is null && invalid.RemainingWorkDays is null, "Неположительный/непредставимый темп стал ETA");
            }
            var failedCalculation = adapter.RefreshPendingConditions(state, bank, Evaluation(decimal.MaxValue));
            Check(failedCalculation.Accepted && failedCalculation.State.PendingOrderIds.Count == 0
                && adapter.Project(failedCalculation.State, 1).Queue.All(order => order.CancellationReason == "calculation-failed")
                && failedCalculation.State.Orders["order-a"].CompletedWork == original.CompletedWork,
                "Source adapter не публикует численную автоотмену без потери работы");
            // Доверенная placement/скорость fixture создаёт zone order; production проекция не угадывает native speed.
            var zoneSlot = initial.Slots.Values.Single(slot => slot.ZoneSlotDefinitionId == "slot_energy");
            var zoneAdapter = new M3ConstructionAdapter(content, startup, template, new("zone-estimate-fixture-only", -.15m, .15m, 0, 0), binding);
            var zone = zoneAdapter.Execute(initial, bank, actor, new("zone-estimate", "enqueueConstruction", ColonyId: initial.ColonyId,
                ExpectedColonyRevision: initial.Revision, DefinitionId: "zone_energy", SlotId: zoneSlot.Id, OrderId: "zone-estimate-order"));
            Check(zone.Accepted, "Zone estimate fixture: " + zone.Code);
            var unknown = adapter.Project(zone.State, 1, bank: bank).Queue.Single(order => order.OrderId == "zone-estimate-order");
            Check(unknown.ExecutionState == "active" && unknown.DailyWork is null && unknown.RemainingWorkDays is null, "Unknown native speed превратился в базовую скорость");
        }
        Console.WriteLine("PASS Q02 source3presets: active/waiting/N0, live workday estimate/fixed quote, Unknown/nonpositive/overflow projection without mutation");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
