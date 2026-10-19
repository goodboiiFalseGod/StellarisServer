using Stellaris.Core.Economy;

static class ShelterEconomyChecks
{
    public static Task Consumers()
    {
        var world = new EconomyWorld([new("energy", 100000), new("food", 100000)],
            [new("farmer", "worker", 100, Amounts("food", 6), Amounts("energy", 2)), new("colonist", "ruler", 100, Amounts("food", 4), Amounts("energy", 1))], [new("planet_jobs", null), new("worker", "planet_jobs"), new("ruler", "planet_jobs")]);
        var owner = world.CreateCountry();
        world.SetBalance(owner, "energy", 1000);
        var planet = world.CreatePlanet(owner);
        world.AddGroup(planet, 200, 1);
        world.SetJobs(planet, "farmer", 1);
        world.SetJobs(planet, "colonist", 1);
        world.SetJobBonusWorkforceMultiplier(planet, "farmer", .1m);
        world.SetJobBonusWorkforceMultiplier(planet, "colonist", .2m);
        world.SetModifiers(planet, [new("worker", "food", "produces", 1, .25m), new("worker", "energy", "upkeep", 1, -.25m)]);
        world.SetBuildingUpkeep(planet, Amounts("energy", 5));
        var original = world.StateHash();
        Check(world.GetInstalledJobEconomyEffects(planet) is null, "Absent installed channel became authoritative empty facts.");
        var candidate = world.Fork();
        candidate.SetInstalledJobEconomyEffects(planet, new(.1m, .05m));
        var configured = candidate.StateHash();
        Check(configured != original && world.StateHash() == original, "Installed economic channel was not fork/hash isolated.");
        var detached = candidate.Fork();
        detached.SetInstalledJobEconomyEffects(planet, null);
        Check(detached.StateHash() == original && candidate.StateHash() == configured, "Removing installed effects changed the original or retained source channel.");
        candidate.AdvanceMonth();
        var report = candidate.GetPlanetReport(planet)!;
        Check(report.Allocated["farmer"].BonusWorkforce == 15 && report.Allocated["colonist"].BonusWorkforce == 25 && report.UnemployedWorkforce == 0, "Installed Workforce multiplied trait bonus, consumed vacancy Workforce or missed a job category.");
        Check(report.Produced["food"] == 16.3675m && report.JobUpkeep["energy"] == 3.8375m && report.BuildingUpkeep["energy"] == 5, "D38/D43 output/base/Workforce/upkeep ordering or target isolation failed.");
        Check(report.Allocated.Values.All(allocation => allocation.Count == 1 && allocation.Workforce == 100), "Economic effects fabricated staffing or changed job capacity.");
        candidate.SetInstalledJobEconomyEffects(planet, new(.2m, .1m));
        candidate.AdvanceMonth();
        Check(candidate.GetPlanetReport(planet)!.Produced["food"] == 18.42m && candidate.GetPlanetReport(planet)!.JobUpkeep["energy"] == 4, "Installed replacement incrementally double counted or overwrote configured channels.");
        candidate.SetInstalledJobEconomyEffects(planet, null);
        candidate.AdvanceMonth();
        Check(candidate.GetPlanetReport(planet)!.Produced["food"] == 14.425m && candidate.GetPlanetReport(planet)!.JobUpkeep["energy"] == 3.675m, "Installed removal deleted configured traits/modifiers or left installed effects.");
        candidate.SetInstalledJobEconomyEffects(planet, new(.1m, .05m));
        candidate.SetModifiers(planet, []);
        candidate.SetJobBonusWorkforceMultiplier(planet, "farmer", .3m);
        candidate.AdvanceMonth();
        Check(candidate.GetPlanetReport(planet)!.Allocated["farmer"].BonusWorkforce == 35 && candidate.GetInstalledJobEconomyEffects(planet) == new InstalledJobEconomyEffects(.1m, .05m), "Configured channel replacement overwrote installed source effects.");
        var beforeInvalid = candidate.StateHash();
        try { candidate.SetInstalledJobEconomyEffects(planet, new(.1m, -.1m)); throw new InvalidOperationException("Negative unsupported installed bonus accepted."); }
        catch (ArgumentOutOfRangeException) { }
        Check(candidate.StateHash() == beforeInvalid, "Invalid installed effects mutated state.");
        SharedMonthRollback();
        OutputCategoryBoundary();
        CategoryModifiers();
        return Task.CompletedTask;
    }

    private static void CategoryModifiers()
    {
        var world = new EconomyWorld([new("food", 100000), new("energy", 100000)],
            [new("farmer", "planet_farmers", 100, Amounts("food", 6), Amounts("energy", 2)), new("child", "child_farmers", 100, Amounts("food", 6), Amounts("energy", 2))],
            [new("planet_jobs", null), new("workers", "planet_jobs"), new("planet_farmers", "workers"), new("child_farmers", "planet_farmers")]);
        var owner = world.CreateCountry();
        world.SetBalance(owner, "energy", 1000);
        var planet = world.CreatePlanet(owner);
        world.AddGroup(planet, 200, 1);
        world.SetJobs(planet, "farmer", 1);
        world.SetJobs(planet, "child", 1);
        world.SetModifiers(planet, [new("planet_jobs", "food", "produces", 0, .1m), new("planet_farmers", "food", "produces", 1, 0), new("workers", "energy", "upkeep", 0, -.25m)]);
        world.SetBuildingUpkeep(planet, Amounts("energy", 5));
        var input = new List<Modifier> { new("planet_farmers", "food", "produces", 1, 0), new("workers", "food", "produces", 100, .1m), new("workers", "food", "produces", 0, .1m), new("workers", "energy", "upkeep", 0, .5m) };
        var snapshot = new InstalledJobEconomyEffects(.1m, .05m, input);
        world.SetInstalledJobEconomyEffects(planet, snapshot);
        var hash = world.StateHash();
        input.Clear();
        Check(world.StateHash() == hash && snapshot.Modifiers.Count == 4, "Caller mutation changed installed category modifiers.");
        try { ((IList<Modifier>)snapshot.Modifiers).Clear(); throw new InvalidOperationException("Installed modifiers expose mutable storage."); }
        catch (NotSupportedException) { }
        var fork = world.Fork();
        world.AdvanceMonth();
        var report = world.GetPlanetReport(planet);
        Check(report.Produced["food"] == 20.58m && report.JobUpkeep["energy"] == 5.25m && report.BuildingUpkeep["energy"] == 5, "Installed exact-only base additions, descendant/duplicate multipliers or upkeep isolation failed.");
        fork.SetInstalledJobEconomyEffects(planet, null);
        fork.AdvanceMonth();
        Check(fork.GetPlanetReport(planet).Produced["food"] == 14.3m && world.GetInstalledJobEconomyEffects(planet) == snapshot, "Installed category removal mutated a fork or configured modifiers.");
        foreach (var invalid in new[] { new Modifier("missing", "food", "produces", 1, 0), new Modifier("workers", "missing", "upkeep", 0, 1), new Modifier("workers", "food", "invalid", 0, 1) })
        {
            hash = world.StateHash();
            var rejected = false;
            try { world.SetInstalledJobEconomyEffects(planet, new(0, 0, [invalid])); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && world.StateHash() == hash, "Invalid installed category snapshot partially replaced state.");
        }
    }

    private static void OutputCategoryBoundary()
    {
        var world = new EconomyWorld([new("food", 100000)],
            [new("outside", "unrelated", 100, Amounts("food", 6), new Dictionary<string, decimal>())], [new("unrelated", null)]);
        var owner = world.CreateCountry();
        var planet = world.CreatePlanet(owner);
        world.AddGroup(planet, 100, 1);
        world.SetJobs(planet, "outside", 1);
        world.SetInstalledJobEconomyEffects(planet, new(.1m, .05m));
        world.AdvanceMonth();
        Check(world.GetPlanetReport(planet).Produced["food"] == 6.3m, "Planet-jobs output escaped its category tree or global Workforce missed an unrelated category.");
    }

    private static void SharedMonthRollback()
    {
        var world = new EconomyWorld([new("food", 100000)], [new("farmer", "worker", 100, Amounts("food", 6), new Dictionary<string, decimal>())], [new("planet_jobs", null), new("worker", "planet_jobs")]);
        var shared = world.CreateCountry();
        var other = world.CreateCountry();
        var planets = new[] { world.CreatePlanet(shared), world.CreatePlanet(shared), world.CreatePlanet(other) };
        foreach (var planet in planets)
        {
            world.AddGroup(planet, 100, 1);
            world.SetJobs(planet, "farmer", 1);
            world.SetInstalledJobEconomyEffects(planet, new(.1m, .05m));
        }
        world.SetInstalledJobEconomyEffects(planets[2], new(decimal.MaxValue, .05m));
        var before = world.StateHash();
        try { world.AdvanceMonth(); throw new InvalidOperationException("Late colony arithmetic overflow accepted."); }
        catch (OverflowException) { }
        Check(world.StateHash() == before && world.Month == 0 && world.GetBalance(shared, "food") == 0, "Later colony failure partially published shared balances/reports/month.");
        foreach (var planet in planets)
        {
            var reportUnavailable = false;
            try { _ = world.GetPlanetReport(planet); }
            catch (InvalidOperationException) { reportUnavailable = true; }
            Check(reportUnavailable, "Failed first month published a planet report.");
        }
        world.SetInstalledJobEconomyEffects(planets[2], new(.1m, .05m));
        world.AdvanceMonth();
        Check(world.Month == 1 && world.GetBalance(shared, "food") == 13.86m && world.GetBalance(other, "food") == 6.93m, "Shared-account monthly effects settled twice or lost a colony.");
        world.SetInstalledJobEconomyEffects(planets[2], new(-2, .05m));
        before = world.StateHash();
        try { world.AdvanceMonth(); throw new InvalidOperationException("Negative combined output factor accepted."); }
        catch (InvalidOperationException exception) when (exception.Message != "Negative combined output factor accepted.") { }
        Check(world.StateHash() == before && world.Month == 1, "Invalid later output factor partially published month.");
    }

    private static IReadOnlyDictionary<string, decimal> Amounts(string resource, decimal value) => new Dictionary<string, decimal> { [resource] = value };
    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
