using Stellaris.Core.Economy;

static class EconomyCandidateChecks
{
    public static Task Isolation()
    {
        var empty = new Dictionary<string, decimal>();
        var world = new EconomyWorld([new("energy", 100000), new("food", 100000)], [new("farmer", "worker", 100, new Dictionary<string, decimal> { ["food"] = 6 }, new Dictionary<string, decimal> { ["energy"] = 1 })], [new("worker", null)]);
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        world.AddGroup(planet, 300, 1);
        world.SetBalance(country, "energy", 1000);
        world.SetJobs(planet, "farmer", 2);
        world.SetJobBonusWorkforceMultiplier(planet, "farmer", .1m);
        world.SetModifiers(planet, [new("worker", "food", "produces", 1, .2m)]);
        world.SetBuildingUpkeep(planet, new Dictionary<string, decimal> { ["energy"] = 2 });
        world.AdvanceMonth();
        var original = world.StateHash();
        var report = world.GetPlanetReport(planet);
        var candidate = world.Fork();
        Check(candidate.StateHash() == original && candidate.Month == world.Month && candidate.GetPlanetReport(planet) == report, "Fork lost IDs/state/month/report.");
        candidate.SetBalance(country, "energy", 500);
        candidate.SetJobs(planet, "farmer", 3);
        candidate.SetJobBonusWorkforceMultiplier(planet, "farmer", .5m);
        candidate.SetModifiers(planet, [new("worker", "food", "produces", 0, 1)]);
        candidate.SetBuildingUpkeep(planet, new Dictionary<string, decimal> { ["energy"] = 7 });
        candidate.AddGroup(planet, 100, 1);
        candidate.AdvanceMonth();
        Check(world.StateHash() == original && world.GetPlanetReport(planet) == report && world.Month == 1 && candidate.Month == 2, "Candidate mutated original state.");
        var stableCandidate = candidate.StateHash();
        world.SetJobs(planet, "farmer", 1);
        world.SetBalance(country, "energy", 2000);
        world.SetModifiers(planet, []);
        world.SetBuildingUpkeep(planet, empty);
        world.SetJobBonusWorkforceMultiplier(planet, "farmer", 0);
        world.AddGroup(planet, 50, 1);
        var anotherCountry = world.CreateCountry();
        world.CreatePlanet(anotherCountry);
        Check(candidate.StateHash() == stableCandidate, "Original mutation changed candidate collections.");
        var beforeFailure = world.StateHash();
        var failed = world.Fork();
        failed.SetBuildingUpkeep(planet, new Dictionary<string, decimal> { ["energy"] = 100000 });
        try { failed.AdvanceMonth(); }
        catch (InvalidOperationException)
        {
            Check(world.StateHash() == beforeFailure, "Discarded failed candidate changed original.");
            var emptyWorld = new EconomyWorld([new("energy", 100)], [], []);
            Check(emptyWorld.Fork().StateHash() == emptyWorld.StateHash(), "Empty world fork changed state.");
            return Task.CompletedTask;
        }
        throw new InvalidOperationException("Candidate deficit was not rejected.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
