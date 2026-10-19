using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class ConditionRefreshChecks
{
    public static void Run(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, M3StartupCatalog startups)
    {
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("refresh-" + preset, 1, home.Homeworld, home.Plan);
            var initial = content.Create(startup, profile.CreateConstructionContributions(startup));
            var slot = initial.Slots.Values.First(item => item.Kind == ConstructionKind.Building && !initial.Installed.ContainsKey(item.Id));
            var modifier = new ConstructionInstalled(slot.Id, "building_generator_districts_1", ConstructionKind.Building, new Dictionary<string, decimal>(), new Dictionary<string, decimal>());
            var state = new ConstructionLiveRuntime(initial.ColonyId, 1, initial.Slots.Values, initial.Installed.Values.Append(modifier));
            var adapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get(preset), fromBinding: M3ConstructionContent.FromBinding.ActingCountry(false));
            var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = new Dictionary<string, decimal> { ["minerals"] = 10000 } },
                new Dictionary<string, ConstructionStorage> { ["minerals"] = new(false, 10000) });
            var actor = new M3ConstructionActor(1, true, true, true);
            var context = content.Context(startup, ConstructionOrdinaryTemplate.Get(preset), state);
            var facts = M3ConstructionContent.EconomyFacts(startup);
            var originalCap = M3DistrictCapacity.Evaluate(content, startup, state, "district_generator", context, facts).Capacity!.Value;
            var installed = state.Installed.Values.Count(item => item.DefinitionId == "district_generator");
            foreach (var districtSlot in state.Slots.Values.Where(item => item.Kind == ConstructionKind.District && !state.Installed.ContainsKey(item.Id)).ToArray())
            {
                var command = new M3ClientCommand("reserve-" + districtSlot.Id, "enqueueConstruction", ColonyId: state.ColonyId, ExpectedColonyRevision: state.Revision,
                    DefinitionId: "district_generator", SlotId: districtSlot.Id, OrderId: "order-" + state.Orders.Count);
                var paid = adapter.Execute(state, bank, actor, command);
                if (!paid.Accepted) { Check(paid.Code == "district-type-capacity-exhausted", "Unexpected rural gate: " + paid.Code); break; }
                var staged = ConstructionLiveTransaction.Stage(paid, bank);
                Check(staged.Accepted, "Reservation stage"); state = staged.Runtime!; bank = staged.Bank!;
            }
            Check(state.PendingOrderIds.Count == originalCap - installed, "Fixture не заполнила source cap");
            var before = state;
            var off = state.SetBuildingEnabled("fixture-disable-cap", 1, state.Revision, modifier.SlotId, modifier.DefinitionId, false);
            Check(off.Accepted, "Trusted cap fixture disable");
            var reducedCap = M3DistrictCapacity.Evaluate(content, startup, off.State, "district_generator", context, facts).Capacity!.Value;
            Check(reducedCap < originalCap, "Source modifier не уменьшил pool");
            var refresh = adapter.RefreshPendingConditions(off.State, bank);
            Check(refresh.Accepted && refresh.State.Revision == off.State.Revision && refresh.State.PendingOrderIds.Count == Math.Max(0, reducedCap - installed), "Candidate не отменил excess reservations");
            var cancelled = refresh.State.Orders.Values.Where(item => item.Status == ConstructionOrderStatus.Cancelled).ToArray();
            Check(cancelled.Length > 0 && cancelled.All(item => item.CancellationReason == ConstructionCancellationReason.ConditionLost), "Причина отмены не condition-lost");
            Check(refresh.State.Orders.Values.All(item => item.CompletedWork == before.Orders[item.Id].CompletedWork && item.Quote == before.Orders[item.Id].Quote), "Recheck изменил работу/quote");
            var credited = ConstructionLiveTransaction.Stage(refresh, bank);
            Check(credited.Accepted && credited.Bank!.Accounts[1]["minerals"] == bank.Accounts[1]["minerals"] + cancelled.Sum(item => item.Quote.Costs["minerals"]), "Refund не соответствует оплаченной цене");
            var again = adapter.RefreshPendingConditions(refresh.State, credited.Bank!);
            Check(again.Accepted && again.ResourceDeltas.Count == 0 && again.State.Revision == refresh.State.Revision, "Recheck повторно вернул ресурсы/revision");
        }
        Console.WriteLine("PASS Q04 source: cap-building disabled fixture cancels excess pending in same revision, no daily work/quote change, exact refund");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
