using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Economy;

// Synthetic source-body fixtures prove machinery, not vanilla eligibility or realized startup.
static class SourceOwnedEconomyChecks
{
    public static Task Application()
    {
        var (initial, planets) = Fixture();
        var planet = planets[0];
        var context = new ConstructionContext();
        var first = Source(context, .1m, .05m);
        var second = Source(context, .2m, .1m);
        var original = initial.EconomyHash();
        var installed = initial.ReplaceInstalledCapacity("install", 0, "colony0", [new("a", first), new("b", second)]);
        Check(installed.Accepted, "Complete synthetic source effects refused.");
        var snapshot = installed.State.InstalledCapacity["colony0"];
        var world = installed.State.ForkEconomy();
        Check(snapshot.JobEconomyEffects == new InstalledJobEconomyEffects(.3m, .15m) && world.GetInstalledJobEconomyEffects(planet) == snapshot.JobEconomyEffects && snapshot.ActiveContributions.Count == 2, "Source totals or source-owned economic channel lost.");
        Check(world.GetInstalledJobCapacity(planet)!["farmer"] == 400 && world.GetInstalledHousing(planet) == 400 && world.Month == 0 && NoReport(world, planet) && world.GetBuildingUpkeep(planet)["energy"] == 5 && installed.State.GetBalance(1, "energy") == 1000, "Installation altered raw capacity, staffing, reports, time, accounts or infrastructure upkeep.");
        var settled = Month(installed.State, "month");
        Check(settled.Accepted && settled.State.Month == 1 && settled.State.ForkEconomy().GetPlanetReport(planet)!.Produced["food"] == 13.5625m && settled.State.ForkEconomy().GetPlanetReport(planet)!.JobUpkeep["energy"] == 1.875m, "Source-owned output/Workforce missed actual monthly calculation or configured channels.");
        Check(settled.State.ForkEconomy().GetPlanetReport(planet)!.Allocated["farmer"].Workforce == 100 && settled.State.ForkEconomy().GetPlanetReport(planet)!.Allocated["farmer"].BonusWorkforce == 25, "Raw capacity became employment or bonus stacked multiplicatively.");
        var retry = settled.State.ReplaceInstalledCapacity("install", 0, "colony0", [new("a", first), new("b", second)]);
        Check(retry.Accepted && retry.Code == "duplicate" && ReferenceEquals(retry.State, settled.State), "Exact source retry reapplied installed effects after settlement.");
        Check(settled.State.ReplaceInstalledCapacity("install", 0, "colony0", [new("a", Source(context, .1m, .05m)), new("b", second)]).Code == "command-id-conflict", "Equivalent new assessment bypassed exact receipt identity.");
        var replaced = settled.State.ReplaceInstalledCapacity("replace", settled.State.Revision, "colony0", [new("a", first)]);
        Check(replaced.Accepted && replaced.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 200, "Replacement retained prior capacity.");
        var replacementMonth = Month(replaced.State, "replacement-month");
        Check(replacementMonth.Accepted && replacementMonth.State.ForkEconomy().GetPlanetReport(planet)!.Produced["food"] == 10.8675m && replacementMonth.State.ForkEconomy().GetPlanetReport(planet)!.JobUpkeep["energy"] == 1.725m, "Replacement double counted or erased configured/trait effects.");
        var removed = replaced.State.ReplaceInstalledCapacity("remove", replaced.State.Revision, "colony0", []);
        Check(removed.Accepted && removed.State.ForkEconomy().GetInstalledJobEconomyEffects(planet) is null && removed.State.ForkEconomy().GetInstalledJobCapacity(planet)!.Count == 0, "Removal retained installed economic/capacity effects.");
        var removalMonth = Month(removed.State, "removal-month");
        Check(removalMonth.Accepted && removalMonth.State.ForkEconomy().GetPlanetReport(planet)!.Produced["food"] == 9.625m && removalMonth.State.ForkEconomy().GetPlanetReport(planet)!.JobUpkeep["energy"] == 1.65m, "Removal erased configured channels or fabricated staffing adapter.");
        var zero = initial.ReplaceInstalledCapacity("zero", 0, "colony0", [new("zero", Source(context, 0, 0))]);
        Check(zero.Accepted && zero.State.ForkEconomy().GetInstalledJobEconomyEffects(planet) == new InstalledJobEconomyEffects(0, 0), "Explicit zero source effects became absent state.");
        var inactive = initial.ReplaceInstalledCapacity("inactive", 0, "colony0", [new("off", Source(context, 1, 1, condition: false, unsupported: true))]);
        Check(inactive.Accepted && inactive.State.ForkEconomy().GetInstalledJobEconomyEffects(planet) is null, "Inactive source supplied economic effects or blocked on inactive diagnostics.");
        var stable = installed.State.EconomyHash();
        foreach (var later in new[] { Source(new(), .1m, .05m), Source(context, .1m, .05m, condition: null), Source(context, .1m, .05m, unsupported: true), Source(context, .1m, .05m, hash: 'b'), Source(context, .1m, .05m, profile: "wrong"), Source(context, .1m, .05m, malformedScalar: true), Source(context, .1m, -.05m), Source(context, decimal.MaxValue, .05m) })
        {
            var failed = installed.State.ReplaceInstalledCapacity("failed", installed.State.Revision, "colony0", [new("first", first), new("later", later)]);
            Check(!failed.Accepted && ReferenceEquals(failed.State, installed.State) && failed.State.EconomyHash() == stable, "Later source/context/mandatory/numeric failure partially published economic effects.");
        }
        Check(!installed.State.ReplaceInstalledCapacity("foreign", installed.State.Revision, "foreign", [new("a", first)]).Accepted && installed.State.EconomyHash() == stable, "Wrong colony binding mutated installed effects.");
        var cancellation = initial.ReplaceInstalledCapacity("cancel", 0, "colony0", [new("positive", Source(context, decimal.MaxValue, 0)), new("negative", Source(context, -decimal.MaxValue, 0))]);
        Check(cancellation.Accepted && cancellation.State.ForkEconomy().GetInstalledJobEconomyEffects(planet)!.OutputMultiplier == 0, "Exact signed output cancellation rounded or failed.");
        var impreciseOutput = initial.ReplaceInstalledCapacity("output-precision", 0, "colony0", [new("max", Source(context, decimal.MaxValue, 0)), new("epsilon", Source(context, .0000000000000000000000000001m, 0))]);
        Check(!impreciseOutput.Accepted && ReferenceEquals(impreciseOutput.State, initial), "Unrepresentable output sum rounded or partially published.");
        foreach (var bonus in new[] { decimal.MaxValue, .0000000000000000000000000001m })
        {
            var failed = initial.ReplaceInstalledCapacity("bonus-precision", 0, "colony0", [new("a", Source(context, 0, decimal.MaxValue)), new("b", Source(context, 0, bonus))]);
            Check(!failed.Accepted && ReferenceEquals(failed.State, initial), "Bonus total overflow/precision loss published a rounded candidate.");
        }
        Check(initial.EconomyHash() == original && installed.State.EconomyHash() == stable, "Source-owned snapshots leaked across forks.");
        LateMonthFailures();
        Hydration();
        ScopeRefusal();
        return Task.CompletedTask;
    }

    private static void LateMonthFailures()
    {
        foreach (var output in new[] { -2m, decimal.MaxValue })
        {
            var (initial, planets) = Fixture(2);
            var context = new ConstructionContext();
            var first = initial.ReplaceInstalledCapacity("first", 0, "colony0", [new("a", Source(context, .1m, .05m))]);
            var later = first.State.ReplaceInstalledCapacity("later", 1, "colony1", [new("b", Source(context, output, .05m))]);
            Check(first.Accepted && later.Accepted, "Signed output candidate was clamped or monthly validation moved into raw snapshot installation.");
            var stable = later.State.EconomyHash();
            var failed = Month(later.State, "failed-month");
            Check(!failed.Accepted && ReferenceEquals(failed.State, later.State) && later.State.Month == 0 && later.State.EconomyHash() == stable && later.State.GetBalance(1, "energy") == 1000 && planets.All(planet => NoReport(later.State.ForkEconomy(), planet)), "Late colony negative factor/overflow published earlier colony reports, accounts or time.");
        }
    }

    private static void Hydration()
    {
        var (initial, planets) = Fixture();
        var world = initial.ForkEconomy();
        world.SetInstalledJobEconomyEffects(planets[0], new(.1m, .05m));
        var rejected = false;
        try { _ = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = world.GetPlanetOwner(planets[0]) }, [new("colony0", 1, planets[0])], initial.Ledgers.Values); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Source-less monthly effects hydration accepted without retained source snapshot.");
    }

    private static void ScopeRefusal()
    {
        foreach (var kind in new[] { "triggered_country_modifier", "triggered_planet_pop_group_modifier_for_species", "triggered_district_planet_modifier" })
        {
            var source = Source(new(), .1m, .05m, kind: kind);
            Check(source.Contributions.Where(item => item.Kind == kind).All(item => item.BlockingGates.Count != 0 && item.Amounts.All(amount => amount.Resource is not ("planet_jobs_produces_mult" or "pop_bonus_workforce_mult"))), "Wrong target context gained global monthly consumer.");
        }
    }

    private static ConstructionAggregateTransition Month(ConstructionAggregate aggregate, string id) => aggregate.Advance(id, aggregate.Revision, "synthetic-explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, []);

    private static ConstructionCatalogAssessment Source(ConstructionContext context, decimal output, decimal bonus, bool? condition = true, bool unsupported = false, char hash = 'a', string kind = "triggered_planet_modifier", string? profile = null, bool malformedScalar = false)
    {
        const string ID = "building_hydroponics_farm";
        var origin = new ConstructionOrigin("common/buildings/03_resource_buildings.txt", 1, 1);
        ConstructionField Scalar(string key, string value) => new(key, "=", value, [], origin, []);
        var fields = new List<ConstructionField> { new("potential", "=", null, [Scalar(condition is null ? "unknown" : "always", condition == false ? "no" : "yes")], origin, []), Scalar("job_farmer_add", "200"), Scalar("planet_housing_add", "200"), Scalar("planet_jobs_produces_mult", output.ToString(CultureInfo.InvariantCulture)), Scalar("pop_bonus_workforce_mult", bonus.ToString(CultureInfo.InvariantCulture)) };
        if (unsupported) { fields.Add(Scalar("planet_amenities_add", "300")); }
        if (malformedScalar) { fields[3] = new(fields[3].Key, fields[3].Operator, fields[3].Scalar, [Scalar("unexpected", "1")], fields[3].Origin, fields[3].InlineCallSites); }
        var definition = new ConstructionPlacementDefinition(ID, "building", origin, [new(kind, "=", null, fields, origin, [])]);
        var numbers = fields.Select((field, index) => (field, index)).Where(pair => pair.field.Scalar is not null).Select(pair => new ConstructionCatalogParameter(ID, "/0:" + kind + "/" + pair.index + ":" + pair.field.Key, "=", decimal.Parse(pair.field.Scalar!, CultureInfo.InvariantCulture), origin));
        var projection = new ConstructionCatalogProjection(profile ?? "construction-building-building_hydroponics_farm-catalog-452-v1", [definition], numbers, [new("launcher-settings.json", 10, new string(hash, 64))], []);
        return ConstructionCatalogAssessor.Assess(projection, ID, context);
    }

    private static (ConstructionAggregate Aggregate, EntityId[] Planets) Fixture(int count = 1)
    {
        var world = new EconomyWorld([new("energy", 10000), new("food", 10000)], [new("farmer", "worker", 100, Amounts("food", 6), Amounts("energy", 1))], [new("planet_jobs", null), new("worker", "planet_jobs")]);
        var country = world.CreateCountry();
        world.SetBalance(country, "energy", 1000);
        var planets = Enumerable.Range(0, count).Select(_ => world.CreatePlanet(country)).ToArray();
        foreach (var planet in planets)
        {
            world.SetBuildingUpkeep(planet, Amounts("energy", 5));
            world.AddGroup(planet, 300, 1);
            world.SetJobs(planet, "farmer", 1);
            world.SetJobBonusWorkforceMultiplier(planet, "farmer", .1m);
            world.SetModifiers(planet, [new("worker", "food", "produces", 1, .25m), new("worker", "energy", "upkeep", 1, -.25m)]);
        }
        var ledgers = planets.Select((planet, index) => new ConstructionLedger(new("colony" + index, ConstructionAuthorityMode.Individualist, false, null, "system"), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = world.GetBalances(country) }, ["slot"])).ToArray();
        return (new(world, new Dictionary<int, EntityId> { [1] = country }, planets.Select((planet, index) => new ConstructionColonyBinding("colony" + index, 1, planet)), ledgers), planets);
    }

    private static IReadOnlyDictionary<string, decimal> Amounts(string key, decimal value) => new Dictionary<string, decimal> { [key] = value };
    private static bool NoReport(EconomyWorld world, EntityId planet)
    {
        try { _ = world.GetPlanetReport(planet); return false; }
        catch (InvalidOperationException) { return true; }
    }
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
