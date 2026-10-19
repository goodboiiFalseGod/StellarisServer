using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Economy;

// Synthetic source bodies exercise existing machinery; no vanilla eligibility or startup claim.
static class SourceCategoryEconomyChecks
{
    private static readonly (string Key, decimal Value)[] _all = [("planet_farmers_food_produces_add", 1), ("planet_miners_minerals_produces_add", 1), ("planet_technician_energy_produces_add", 1), ("planet_farmers_produces_mult", .2m), ("planet_miners_produces_mult", .3m), ("planet_technician_produces_mult", .4m), ("planet_jobs_ruler_produces_mult", .5m), ("planet_jobs_unity_produces_mult", .1m), ("planet_augmentors_upkeep_mult", .5m), ("planet_pop_assemblers_upkeep_mult", -.25m)];

    public static Task Application()
    {
        var (initial, planets) = Fixture();
        var context = new ConstructionContext();
        var source = Source(context, [_all]);
        var installed = initial.ReplaceInstalledCapacity("install", 0, "c0", [new("a", source)]);
        Check(installed.Accepted && installed.State.InstalledCapacity["c0"].CategoryJobEffects.Count == 10, "Exact source category mapping failed.");
        var world = installed.State.ForkEconomy();
        Check(world.GetInstalledJobCapacity(planets[0])!.Count == 0 && world.GetInstalledHousing(planets[0]) == 0 && world.GetInstalledJobEconomyEffects(planets[0])!.Modifiers.Count == 28, "Category fields became raw capacity or lost all-resource expansion.");
        var month = Month(installed.State, "month");
        Check(month.Accepted, "Source category effects failed actual monthly calculation.");
        var report = month.State.ForkEconomy().GetPlanetReport(planets[0]);
        Check(report.Produced["food"] == 19.24m && report.Produced["minerals"] == 6.5m && report.Produced["energy"] == 9.8m && report.Produced["unity"] == 12.4m, "Exact-only base adds, descendant/all-resource multipliers or ruler/unity channels failed.");
        Check(report.JobUpkeep["energy"] == 14.45m && report.JobUpkeep["food"] == 3 && report.BuildingUpkeep["energy"] == 5 && report.Allocated["farmer"].BonusWorkforce == 10, "Category upkeep affected infrastructure, output affected upkeep or trait Workforce was erased.");
        Check(month.State.ReplaceInstalledCapacity("install", 0, "c0", [new("a", source)]).Code == "duplicate", "Source category retry reapplied effects.");
        var one = installed.State.ReplaceInstalledCapacity("one", 1, "c0", [new("one", Source(context, [[("planet_farmers_food_produces_add", 1)]]))]);
        Check(one.Accepted && Month(one.State, "one-month").State.ForkEconomy().GetPlanetReport(planets[0]).Produced["food"] == 16.28m, "Category replacement retained old entries or erased configured state.");
        var empty = one.State.ReplaceInstalledCapacity("empty", one.State.Revision, "c0", []);
        Check(empty.Accepted && empty.State.ForkEconomy().GetInstalledJobEconomyEffects(planets[0]) is null && Month(empty.State, "empty-month").State.ForkEconomy().GetPlanetReport(planets[0]).Produced["food"] == 15.07m, "Category removal retained installed entries or deleted configured/trait effects.");
        var independent = initial.ReplaceInstalledCapacity("independent", 0, "c0", [new("a", Source(context, [[("planet_farmers_food_produces_add", 1)], [("planet_farmers_food_produces_add", 1)]]))]);
        Check(independent.Accepted && Month(independent.State, "independent-month").State.ForkEconomy().GetPlanetReport(planets[0]).Produced["food"] == 17.49m, "Independent category branches were overwritten or applied twice.");
        var scaled = initial.ReplaceInstalledCapacity("scaled", 0, "c0", [new("a", Source(context, [[("planet_farmers_food_produces_add", 1), ("planet_farmers_produces_mult", 1)]], multiplier: .4m))]);
        Check(scaled.Accepted && Month(scaled.State, "scaled-month").State.ForkEconomy().GetPlanetReport(planets[0]).Produced["food"] == 21.21m, "Resolved category-only multiplier did not scale exact source amounts before calculation.");
        var stable = installed.State.EconomyHash();
        foreach (var invalid in new[] { Source(context, [[("planet_districts_upkeep_mult", .1m)]]), Source(context, [[("planet_technicians_energy_produces_add", 1)]]), Source(context, [_all], unknown: true), Source(context, [[("planet_farmers_food_produces_add", 1), ("planet_farmers_food_produces_add", 1)]]), Source(context, [[("planet_farmers_food_produces_add", decimal.MaxValue)], [("planet_farmers_food_produces_add", .0000000000000000000000000001m)]]), Source(context, [[("planet_farmers_food_produces_add", decimal.MaxValue)]], multiplier: 2), Source(context, [[("planet_farmers_food_produces_add", .0000000000000000000000000001m)]], multiplier: .1m), Source(context, [[("planet_farmers_food_produces_add", 1), ("job_farmer_add", 200)]], multiplier: .4m) })
        {
            var failed = installed.State.ReplaceInstalledCapacity("failed", 1, "c0", [new("known", source), new("late", invalid)]);
            Check(!failed.Accepted && ReferenceEquals(failed.State, installed.State) && failed.State.EconomyHash() == stable, "Later unsupported/unknown/duplicate/precision/overflow/mixed-multiplier effect partially published.");
        }
        var cancellation = initial.ReplaceInstalledCapacity("cancel", 0, "c0", [new("a", Source(context, [[("planet_farmers_food_produces_add", decimal.MaxValue)], [("planet_farmers_food_produces_add", -decimal.MaxValue)]]))]);
        Check(cancellation.Accepted && cancellation.State.ForkEconomy().GetInstalledJobEconomyEffects(planets[0])!.Modifiers.Single().Add == 0, "Exact source category cancellation rounded or failed.");
        var (two, _) = Fixture(2);
        var first = two.ReplaceInstalledCapacity("first", 0, "c0", [new("a", source)]).State;
        foreach (var (key, bad) in new[] { ("planet_farmers_produces_mult", -2m), ("planet_farmers_produces_mult", decimal.MaxValue), ("planet_farmers_food_produces_add", -100m), ("planet_farmers_food_produces_add", decimal.MaxValue) })
        {
            var later = first.ReplaceInstalledCapacity("later", first.Revision, "c1", [new("b", Source(context, [[(key, bad)]]))]);
            Check(later.Accepted, "Signed category modifier was clamped before monthly calculation.");
            var hash = later.State.EconomyHash();
            var failed = Month(later.State, "failed-month");
            Check(!failed.Accepted && ReferenceEquals(failed.State, later.State) && failed.State.EconomyHash() == hash && failed.State.Month == 0 && failed.State.GetBalance(1, "energy") == 1000, "Late category month failure published earlier colony state.");
        }
        RuntimeValidation();
        return Task.CompletedTask;
    }

    public static void GrowingSpecies(ConstructionCatalogProjection closure)
    {
        var (initial, planets) = Fixture();
        var origin = new ConstructionOrigin("checks/synthetic-growing-species", 1, 1);
        ConstructionField Field(string key, string? scalar = null, params ConstructionField[] children) => new(key, "=", scalar, children, origin, []);
        var potential = new[] { Field("exists", "owner"), Field("owner", children: [Field("has_modifier", "cyber_implant_taxes")]), Field("exists", "growing_species"), Field("growing_species", children: [Field("species", children: [Field("has_trait", "trait_cybernetic")])]) };
        ConstructionContext Context(IEnumerable<string>? traits) => new(ownerExists: true, sourceFacts: new(shelter: new(ownerModifiers: ["cyber_implant_taxes"], growingSpecies: new(true, "target", "species", traits))));
        ConstructionCatalogAssessment Assessment(IEnumerable<string>? traits) => Source(Context(traits), [[("planet_augmentors_upkeep_mult", .1m)]], potential: potential, conditions: closure);
        var active = Assessment(["trait_cybernetic"]);
        var installed = initial.ReplaceInstalledCapacity("growing-install", 0, "c0", [new("synthetic", active)]);
        Check(installed.Accepted && installed.State.InstalledCapacity["c0"].CategoryJobEffects.Single() is { Category: "planet_augmentors", Operation: "upkeep", Mult: .1m }, "Synthetic original-shape growing potential did not reach installed typed upkeep consumer.");
        var month = Month(installed.State, "growing-month");
        Check(month.Accepted, "Growing trait-selected category upkeep failed monthly calculation.");
        var report = month.State.ForkEconomy().GetPlanetReport(planets[0]);
        Check(report.JobUpkeep["energy"] == 14.4m && report.JobUpkeep["food"] == 3.1m && report.BuildingUpkeep["energy"] == 5 && report.Produced["food"] == 15.07m && report.Allocated["farmer"].BonusWorkforce == 10, "Growing trait-selected profession upkeep altered infrastructure, output or trait Workforce.");
        Check(installed.State.ReplaceInstalledCapacity("growing-install", 0, "c0", [new("synthetic", active)]).Code == "duplicate", "Growing source retry reapplied effects.");
        var before = installed.State.EconomyHash();
        var unknown = installed.State.ReplaceInstalledCapacity("growing-unknown", 1, "c0", [new("synthetic", Assessment(null))]);
        Check(!unknown.Accepted && ReferenceEquals(unknown.State, installed.State) && unknown.State.EconomyHash() == before, "Unknown growing trait membership partially replaced installed effects.");
        var absent = installed.State.ReplaceInstalledCapacity("growing-absent", 1, "c0", [new("synthetic", Assessment([]))]);
        Check(absent.Accepted && absent.State.ForkEconomy().GetInstalledJobEconomyEffects(planets[0]) is null, "Known absent growing trait retained installed upkeep.");
        var removed = Month(absent.State, "growing-absent-month");
        Check(removed.Accepted && removed.State.ForkEconomy().GetPlanetReport(planets[0]).JobUpkeep["energy"] == 14.2m && removed.State.ForkEconomy().GetPlanetReport(planets[0]).JobUpkeep["food"] == 3, "Inactive source replacement did not remove profession upkeep contribution.");
    }

    private static void RuntimeValidation()
    {
        var (initial, planets) = Fixture();
        var world = initial.ForkEconomy();
        foreach (var invalid in new[] { new InstalledCategoryJobModifier("missing", null, "produces", 0, 1), new("planet_jobs", "missing", "produces", 1, 0), new("planet_jobs", "food", "invalid", 0, 1) })
        {
            var rejected = false;
            try { _ = world.ResolveInstalledJobEconomyEffects(null, [invalid]); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unknown actual category/resource/operation was accepted.");
        }
        var huge = Enumerable.Range(0, 1025).Select(_ => new InstalledCategoryJobModifier("planet_jobs", null, "produces", 0, 1)).ToArray();
        var bounded = false;
        try { _ = world.ResolveInstalledJobEconomyEffects(null, huge); }
        catch (ArgumentException) { bounded = true; }
        Check(bounded && world.GetInstalledJobEconomyEffects(planets[0]) is null, "All-resource expansion escaped budget or mutated world.");
    }

    private static ConstructionCatalogAssessment Source(ConstructionContext context, (string Key, decimal Value)[][] blocks, decimal? multiplier = null, bool unknown = false, IReadOnlyList<ConstructionField>? potential = null, ConstructionCatalogProjection? conditions = null)
    {
        const string ID = "building_hydroponics_farm";
        var origin = new ConstructionOrigin("common/buildings/03_resource_buildings.txt", 1, 1);
        var parameters = new List<ConstructionCatalogParameter>();
        var roots = new List<ConstructionField>();
        foreach (var (values, block) in blocks.Select((values, block) => (values, block)))
        {
            var fields = new List<ConstructionField> { new("potential", "=", null, potential ?? [new(unknown ? "unknown" : "always", "=", "yes", [], origin, [])], origin, []) };
            foreach (var (key, value) in values.Concat(multiplier is { } mult ? new[] { ("mult", mult) } : Array.Empty<(string, decimal)>()))
            {
                parameters.Add(new(ID, "/" + block + ":triggered_planet_modifier/" + fields.Count + ":" + key, "=", value, origin));
                fields.Add(new(key, "=", value.ToString(CultureInfo.InvariantCulture), [], origin, []));
            }
            roots.Add(new("triggered_planet_modifier", "=", null, fields, origin, []));
        }
        var versionFiles = conditions is null ? new[] { new ConstructionFile("launcher-settings.json", 10, new string('a', 64)) } : conditions.Files.Where(file => file.RelativePath == "launcher-settings.json").ToArray();
        var projection = new ConstructionCatalogProjection("construction-building-building_hydroponics_farm-catalog-452-v1", [new(ID, "building", origin, roots)], parameters, versionFiles, []);
        return ConstructionCatalogAssessor.Assess(projection, ID, context, conditions);
    }

    private static ConstructionAggregateTransition Month(ConstructionAggregate state, string id) => state.Advance(id, state.Revision, "synthetic-explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, []);
    private static (ConstructionAggregate Aggregate, EntityId[] Planets) Fixture(int count = 1)
    {
        IReadOnlyDictionary<string, decimal> Amounts(params (string Id, decimal Value)[] values) => values.ToDictionary(value => value.Id, value => value.Value);
        var world = new EconomyWorld([new("food", 10000), new("energy", 10000), new("minerals", 10000), new("unity", 10000)], [new("farmer", "planet_farmers", 100, Amounts(("food", 6)), Amounts(("energy", 2))), new("child", "child_farmers", 100, Amounts(("food", 6)), Amounts(("energy", 2))), new("miner", "planet_miners", 100, Amounts(("minerals", 4)), Amounts(("energy", 2))), new("technician", "planet_technician", 100, Amounts(("energy", 6)), Amounts(("energy", 2))), new("augmentor", "planet_augmentors", 100, Amounts(("unity", 4)), Amounts(("energy", 2), ("food", 1))), new("assembler", "planet_pop_assemblers", 100, Amounts(), Amounts(("energy", 3), ("food", 2))), new("ruler", "planet_jobs_ruler", 100, Amounts(("unity", 5)), Amounts(("energy", 1)))], [new("planet_jobs", null), new("planet_farmers", "planet_jobs"), new("child_farmers", "planet_farmers"), new("planet_miners", "planet_jobs"), new("planet_technician", "planet_jobs"), new("planet_augmentors", "planet_jobs"), new("planet_pop_assemblers", "planet_jobs"), new("planet_jobs_ruler", "planet_jobs")]);
        var country = world.CreateCountry();
        world.SetBalance(country, "energy", 1000);
        world.SetBalance(country, "food", 1000);
        var planets = Enumerable.Range(0, count).Select(_ => world.CreatePlanet(country)).ToArray();
        foreach (var planet in planets)
        {
            world.AddGroup(planet, 700, 1);
            foreach (var job in new[] { "farmer", "child", "miner", "technician", "augmentor", "assembler", "ruler" }) { world.SetJobs(planet, job, 1); }
            world.SetBuildingUpkeep(planet, Amounts(("energy", 5)));
            world.SetJobBonusWorkforceMultiplier(planet, "farmer", .1m);
            world.SetModifiers(planet, [new("planet_farmers", "food", "produces", 1, 0), new("planet_jobs", "food", "produces", 0, .1m)]);
        }
        var ledgers = planets.Select((planet, index) => new ConstructionLedger(new("c" + index, ConstructionAuthorityMode.Individualist, false, null, "system"), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = world.GetBalances(country) }, ["slot"]));
        return (new(world, new Dictionary<int, EntityId> { [1] = country }, planets.Select((planet, index) => new ConstructionColonyBinding("c" + index, 1, planet)), ledgers), planets);
    }
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
