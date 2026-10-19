using System.Collections;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Economy;

static class InstalledCapacityChecks
{
    public static Task Snapshots()
    {
        var (initial, planet) = Fixture();
        var context = new ConstructionContext();
        var first = Synthetic(200, 200, context);
        var second = Synthetic(200, -100, context);
        var hash = initial.EconomyHash();
        Check(initial.ForkEconomy().GetInstalledJobCapacity(planet) is null && initial.ForkEconomy().GetInstalledHousing(planet) is null, "Uninitialized capacity became an empty authoritative snapshot.");
        ConstructionInstalledCapacityObject[] objects = [new("a", first), new("b", second)];
        var installed = initial.ReplaceInstalledCapacity("install", 0, "colony", objects);
        Check(installed.Accepted && installed.State.Revision == 1 && installed.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 400 && installed.State.ForkEconomy().GetInstalledHousing(planet) == 100, "Raw independent installed contributions were not applied.");
        var snapshot = installed.State.InstalledCapacity["colony"];
        Check(snapshot.Objects is IList list && list.IsReadOnly && snapshot.ActiveContributions.Count == 2 && ReferenceEquals(snapshot.Objects[0].Assessment, first) && ReferenceEquals(snapshot.ActiveContributions[0], first.Contributions[0]), "Installed identities/source contributions were lost or mutable.");
        objects[0] = new("changed", second);
        Check(snapshot.Objects[0].InstanceId == "a", "Caller collection mutated installed state.");
        var replay = installed.State.ReplaceInstalledCapacity("install", 0, "colony", [new("a", first), new("b", second)]);
        Check(replay.Accepted && replay.Code == "duplicate" && ReferenceEquals(replay.State, installed.State), "Installed retry reapplied contributions.");
        Check(installed.State.ReplaceInstalledCapacity("install", 0, "colony", [new("a", Synthetic(200, 200, context)), new("b", second)]).Code == "command-id-conflict", "Different assessed snapshot reused a receipt.");
        var replaced = installed.State.ReplaceInstalledCapacity("replace", 1, "colony", [new("a", second)]);
        Check(!replaced.Accepted && replaced.Code == "installed-capacity-unresolved", "Negative final housing was silently clamped.");
        replaced = installed.State.ReplaceInstalledCapacity("replace", 1, "colony", [new("a", first)]);
        Check(replaced.Accepted && replaced.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 200 && replaced.State.ForkEconomy().GetInstalledHousing(planet) == 200, "Object replacement incrementally accumulated old capacity.");
        var removed = replaced.State.ReplaceInstalledCapacity("remove", 2, "colony", []);
        Check(removed.Accepted && removed.State.ForkEconomy().GetInstalledJobCapacity(planet)!.Count == 0 && removed.State.ForkEconomy().GetInstalledHousing(planet) == 0 && removed.State.InstalledCapacity["colony"].Objects.Count == 0, "Complete empty snapshot did not remove installed contributions.");
        Check(removed.State.ReplaceInstalledCapacity("stale", 0, "colony", []).Code == "stale-revision" && removed.State.ReplaceInstalledCapacity("unknown", 3, "foreign", []).Code == "unknown-colony", "Snapshot revision/binding guards bypassed.");
        var detached = installed.State.ForkEconomy();
        var detachedHash = detached.StateHash();
        detached.SetInstalledCapacity(planet, new Dictionary<string, decimal> { ["farmer"] = 1 }, 1, 0);
        Check(installed.State.EconomyHash() == detachedHash && detached.StateHash() != detachedHash && initial.EconomyHash() == hash, "Installed capacity fork/hash isolation failed.");
        Check(installed.State.Month == 0 && installed.State.GetBalance(1, "energy") == 1000 && installed.State.ForkEconomy().GetBuildingUpkeep(planet)["energy"] == 5, "Capacity installation changed month, resources or infrastructure upkeep.");
        var world = installed.State.ForkEconomy();
        world.AdvanceMonth();
        Check(world.GetPlanetReport(planet)!.Allocated["farmer"].Count == 1 && world.GetPlanetReport(planet)!.Allocated["farmer"].Workforce == 100, "Raw installed capacity created staffing or was passed as job count.");
        foreach (var later in new[] { Synthetic(1, 1, new()), Synthetic(1, 1, context, null), Synthetic(1, 1, context, unsupported: true), Synthetic(1, 1, context, hash: 'b'), Synthetic(decimal.MaxValue, 1, context), Synthetic(.0000000000000000000000000001m, 1, context) })
        {
            var failure = installed.State.ReplaceInstalledCapacity("failure", 1, "colony", [new("a", first), new("late", later)]);
            Check(!failure.Accepted && ReferenceEquals(failure.State, installed.State) && failure.State.EconomyHash() == detachedHash, "Later context/source/effect/precision failure partially published installed contributions.");
        }
        var inactive = initial.ReplaceInstalledCapacity("inactive", 0, "colony", [new("off", Synthetic(20, 20, context, false, true))]);
        Check(inactive.Accepted && inactive.State.ForkEconomy().GetInstalledJobCapacity(planet)!.Count == 0, "Inactive unknown modifier blocked unrelated capacity.");
        var exactCancellation = initial.ReplaceInstalledCapacity("exact-cancellation", 0, "colony", [new("positive", Synthetic(decimal.MaxValue, 1, context)), new("negative", Synthetic(-decimal.MaxValue, 1, context))]);
        Check(exactCancellation.Accepted && exactCancellation.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 0 && exactCancellation.State.ForkEconomy().GetInstalledHousing(planet) == 2, "Exact signed source cancellation rounded or failed.");
        Check(initial.ReplaceInstalledCapacity("duplicate", 0, "colony", [new("a", first), new("a", first)]).Code == "invalid-installed-snapshot", "Duplicate installed identity counted twice.");
        foreach (var fanout in new[] { Synthetic(1, 1, context, blocks: 300), Synthetic(1, 1, context, jobKeys: 128) })
        {
            var failure = installed.State.ReplaceInstalledCapacity("fanout", 1, "colony", Enumerable.Range(0, 4096).Select(index => new ConstructionInstalledCapacityObject("instance-" + index, fanout)));
            Check(!failure.Accepted && failure.Code == "installed-capacity-unresolved" && ReferenceEquals(failure.State, installed.State) && installed.State.EconomyHash() == detachedHash, "Repeated assessment fanout escaped preflight or changed state.");
        }
        Check(initial.ReplaceInstalledCapacity("oversize", 0, "colony", Enumerable.Range(0, 4097).Select(index => new ConstructionInstalledCapacityObject("instance-" + index, first))).Code == "capacity-limit", "Installed object enumeration was not bounded.");
        try { _ = new ConstructionAggregate(installed.State.ForkEconomy(), new Dictionary<int, EntityId> { [1] = installed.State.ForkEconomy().GetPlanetOwner(planet) }, [new("colony", 1, planet)], [installed.State.Ledgers["colony"]]); throw new Exception("Source-less capacity hydration accepted."); }
        catch (ArgumentException) { }
        return Task.CompletedTask;
    }

    public static void Real(ConstructionCatalogAssessment generator, ConstructionCatalogAssessment hydroponics)
    {
        var (initial, planet) = Fixture();
        var result = initial.ReplaceInstalledCapacity("real", 0, "colony", [new("generator-1", generator), new("generator-2", generator), new("farm-1", hydroponics)]);
        Check(result.Accepted && result.State.ForkEconomy().GetInstalledJobCapacity(planet)!["technician"] == 400 && result.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 200 && result.State.ForkEconomy().GetInstalledHousing(planet) == 400, "Real installed generator/hydroponics capacity or housing incorrect.");
        Check(result.State.ForkEconomy().GetBuildingUpkeep(planet)["energy"] == 5 && result.State.GetBalance(1, "energy") == 1000 && result.State.Month == 0, "Real raw upkeep was charged or native startup/settlement inferred.");
    }

    public static Task Zones()
    {
        var (initial, planet) = Fixture();
        var context = new ConstructionContext();
        var zone = Synthetic(100, 20, context, zone: true);
        var first = new ConstructionInstalledCapacityObject("zone-a", zone, "district-group", 2);
        var second = new ConstructionInstalledCapacityObject("zone-b", zone, "district-group", 2);
        Check(initial.ForkEconomy().GetInstalledZoneBuildingSlotsAdd(planet) is null, "Uninitialized slot addition became zero.");
        var installed = initial.ReplaceInstalledCapacity("zones", 0, "colony", [first, second]);
        Check(installed.Accepted && installed.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 400 && installed.State.ForkEconomy().GetInstalledHousing(planet) == 80 && installed.State.ForkEconomy().GetInstalledZoneBuildingSlotsAdd(planet) == 6, "Zone amounts were scaled twice or flat slots multiplied by district level.");
        var snapshot = installed.State.InstalledCapacity["colony"];
        Check(snapshot.ZoneStages.Count == 2 && snapshot.ZoneStages["zone-a"].DistrictInstanceId == "district-group" && snapshot.ZoneStages["zone-a"].WorldRevision == 0 && snapshot.ZoneStages["zone-a"].DistrictLevel == 2 && ReferenceEquals(snapshot.ZoneStages["zone-a"].Source, zone), "Installed zone stage identity/source/revision lost.");
        var replay = installed.State.ReplaceInstalledCapacity("zones", 0, "colony", [first, second]);
        Check(replay.Accepted && replay.Code == "duplicate" && ReferenceEquals(replay.State, installed.State), "Zone replay reapplied amounts.");
        Check(installed.State.ReplaceInstalledCapacity("zones", 0, "colony", [first with { DistrictLevel = 3 }, second]).Code == "command-id-conflict" && installed.State.ReplaceInstalledCapacity("zones", 0, "colony", [first with { DistrictId = "other" }, second]).Code == "command-id-conflict", "Changed zone binding reused an old receipt.");
        var changed = installed.State.ReplaceInstalledCapacity("level", 1, "colony", [first with { DistrictLevel = 3 }]);
        Check(changed.Accepted && changed.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 300 && changed.State.ForkEconomy().GetInstalledZoneBuildingSlotsAdd(planet) == 3, "Level replacement accumulated old zone amounts.");
        var zero = initial.ReplaceInstalledCapacity("zero", 0, "colony", [first with { DistrictLevel = 0 }]);
        Check(zero.Accepted && zero.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 0 && zero.State.ForkEconomy().GetInstalledZoneBuildingSlotsAdd(planet) == 3, "Zero district level erased flat zone additions.");
        var multiplied = initial.ReplaceInstalledCapacity("mult", 0, "colony", [new("zone", Synthetic(100, 20, context, zone: true, multiplier: .25m), "district", 2)]);
        Check(multiplied.Accepted && multiplied.State.ForkEconomy().GetInstalledJobCapacity(planet)!["farmer"] == 50 && multiplied.State.ForkEconomy().GetInstalledZoneBuildingSlotsAdd(planet) == 3, "Zone numeric source multiplier failed or leaked onto flat slots.");
        var stable = installed.State.EconomyHash();
        foreach (var invalid in new[] { first with { DistrictId = null }, first with { DistrictId = "bad\n" }, first with { DistrictLevel = null }, first with { DistrictLevel = -1 }, first with { DistrictLevel = 4097 }, first with { Assessment = Synthetic(1, 1, context, zone: true, omitPotential: true) }, first with { Assessment = Synthetic(1, 1, context, unsupported: true, zone: true) }, first with { Assessment = Synthetic(decimal.MaxValue, 1, context, zone: true) } })
        {
            var failed = installed.State.ReplaceInstalledCapacity("bad-zone", 1, "colony", [new("farm", Synthetic(200, 0, context)), invalid]);
            Check(!failed.Accepted && ReferenceEquals(failed.State, installed.State) && failed.State.EconomyHash() == stable, "Later invalid zone partially published earlier farm/slot state.");
        }
        Check(!initial.ReplaceInstalledCapacity("inconsistent", 0, "colony", [first, second with { DistrictLevel = 3 }]).Accepted, "One district identity accepted inconsistent levels.");
        Check(!initial.ReplaceInstalledCapacity("unexpected", 0, "colony", [new("farm", Synthetic(1, 1, context), "district", 2)]).Accepted, "Nonzone accepted a district-level binding.");
        foreach (var invalidId in new[] { new string('x', 10000), "bad\n", "" })
        {
            var failure = installed.State.ReplaceInstalledCapacity("early-id", 1, "colony", [first with { DistrictId = invalidId }]);
            Check(!failure.Accepted && failure.Code == "invalid-installed-snapshot" && ReferenceEquals(failure.State, installed.State) && failure.State.EconomyHash() == stable, "Malformed district identifier reached fingerprint serialization or changed state.");
        }
        foreach (var fanout in new[] { Synthetic(1, 1, context, zone: true, blocks: 300), Synthetic(1, 1, context, zone: true, jobKeys: 128) })
        {
            var failed = initial.ReplaceInstalledCapacity("zone-fanout", 0, "colony", Enumerable.Range(0, 4096).Select(index => new ConstructionInstalledCapacityObject("zone-" + index, fanout, "district", 1)));
            Check(!failed.Accepted && ReferenceEquals(failed.State, initial), "Zone staging allocated past cumulative fanout guards.");
        }
        var detached = installed.State.ForkEconomy();
        Check(detached.GetInstalledZoneBuildingSlotsAdd(planet) == 6 && detached.StateHash() == stable, "Fork lost zone slot contributions.");
        detached.SetInstalledCapacity(planet, new Dictionary<string, decimal> { ["farmer"] = 400 }, 80, 7);
        Check(detached.StateHash() != stable && installed.State.EconomyHash() == stable, "Slot-only changes were not hashed or mutated the aggregate.");
        var empty = installed.State.ReplaceInstalledCapacity("empty", 1, "colony", []);
        Check(empty.Accepted && empty.State.ForkEconomy().GetInstalledZoneBuildingSlotsAdd(planet) == 0 && empty.State.InstalledCapacity["colony"].ZoneStages.Count == 0, "Zone removal retained flat slot addition or stage identity.");
        return Task.CompletedTask;
    }

    public static void RealFarming(ConstructionCatalogProjection catalog, ConstructionCatalogProjection conditions)
    {
        ConstructionContext Context(bool gestalt = false, bool spiritualist = false, IEnumerable<string>? civics = null, IEnumerable<string>? technology = null, bool? wilderness = false)
        {
            var facts = Enum.GetValues<ConstructionOwnerPredicate>().ToDictionary(key => key, key => (bool?)(key is ConstructionOwnerPredicate.Regular or ConstructionOwnerPredicate.UsesFood));
            facts[ConstructionOwnerPredicate.Gestalt] = gestalt;
            facts[ConstructionOwnerPredicate.SpiritualistFallenEmpire] = spiritualist;
            facts[ConstructionOwnerPredicate.Wilderness] = wilderness;
            return new(ownerExists: true, ownerFacts: facts, planetClass: "pc_continental", modifiers: [], carrierFlags: [], technologies: technology, sourceFacts: new ConstructionSourceFacts(districtSet: "standard", validCivics: civics));
        }
        var (initial, planet) = Fixture();
        foreach (var (context, job, housing) in new[]
        {
            (Context(civics: [], technology: []), "farmer", 200m),
            (Context(gestalt: true, civics: [], technology: []), "agri_drone", 300m),
            (Context(spiritualist: true, civics: [], technology: []), "fe_acolyte_farm", 200m),
            (Context(civics: ["civic_agrarian_idyll"], technology: []), "farmer", 600m),
            (Context(civics: ["civic_agrarian_idyll"], technology: ["tech_housing_agrarian_idyll"]), "farmer", 800m)
        })
        {
            var farming = ConstructionCatalogAssessor.Assess(catalog, "district_farming", context, conditions);
            var result = initial.ReplaceInstalledCapacity("farming", 0, "colony", [new("farm-district", farming)]);
            Check(result.Accepted, "Complete real farming modifier set refused.");
            var world = result.State.ForkEconomy();
            Check(world.GetInstalledJobCapacity(planet)!.Count == 1 && world.GetInstalledJobCapacity(planet)![job] == 200 && world.GetInstalledHousing(planet) == housing, "Farming branch IDs/housing were aliased, suppressed or combined incorrectly.");
            Check(result.State.InstalledCapacity["colony"].ActiveContributions.SequenceEqual(farming.Contributions.Where(item => item.Kind is "planet_modifier" or "triggered_planet_modifier").Where(item => item.Condition.Truth == ConstructionTruth.True)), "An active farming modifier or exact source identity was omitted.");
            Check(world.GetBuildingUpkeep(planet)["energy"] == 5 && result.State.GetBalance(1, "energy") == 1000 && result.State.Month == 0 && result.State.Ledgers["colony"].Installed.Count == 0, "Raw farming effect application charged upkeep or fabricated completion/startup.");
            world.AdvanceMonth();
            Check(world.GetPlanetReport(planet)!.Allocated["farmer"].Count == 1 && world.GetPlanetReport(planet)!.Allocated["farmer"].Workforce == 100, "Farming capacity fabricated employment.");
            Check(result.State.ReplaceInstalledCapacity("farming", 0, "colony", [new("farm-district", farming)]).Code == "duplicate", "Farming retry applied effects twice.");
            var generator = ConstructionCatalogAssessor.Assess(catalog, "district_generator", context, conditions);
            var mixed = result.State.ReplaceInstalledCapacity("mixed", 1, "colony", [new("farm-a", farming), new("farm-b", farming), new("generator", generator)]);
            Check(mixed.Accepted && mixed.State.ForkEconomy().GetInstalledJobCapacity(planet)![job] == 400 && mixed.State.ForkEconomy().GetInstalledHousing(planet) == housing * 2 + (housing >= 600 ? 500 : context.OwnerFacts[ConstructionOwnerPredicate.Gestalt] == true ? 300 : 200), "Mixed replacement accumulated prior farming effects or lost independent district housing.");
            var hash = mixed.State.EconomyHash();
            var mining = ConstructionCatalogAssessor.Assess(catalog, "district_mining", context, conditions);
            Check(mining.Contributions.Any(item => item.BlockingGates.Any(gate => gate.DependencyId == "catalog-potential-not-declared")), "Mining missing-potential evidence disappeared.");
            foreach (var later in new[] { mining, ConstructionCatalogAssessor.Assess(catalog, "district_farming", Context(civics: [], technology: [], wilderness: null), conditions) })
            {
                var failed = mixed.State.ReplaceInstalledCapacity("late-failure", 2, "colony", [new("farm", farming), new("later", later)]);
                Check(!failed.Accepted && ReferenceEquals(failed.State, mixed.State) && mixed.State.EconomyHash() == hash, "Unknown/unsupported later object partially published farming effects.");
            }
            var replacement = mixed.State.ReplaceInstalledCapacity("replacement", 2, "colony", [new("farm", farming)]);
            Check(replacement.Accepted && replacement.State.ForkEconomy().GetInstalledJobCapacity(planet)![job] == 200 && replacement.State.ForkEconomy().GetInstalledHousing(planet) == housing, "Complete farming replacement did not remove prior objects.");
            var removal = replacement.State.ReplaceInstalledCapacity("removal", 3, "colony", []);
            Check(removal.Accepted && removal.State.ForkEconomy().GetInstalledJobCapacity(planet)!.Count == 0 && removal.State.ForkEconomy().GetInstalledHousing(planet) == 0, "Farming removal left accumulated capacity.");
            Console.WriteLine($"Installed farming: {job} 200 / housing {housing} / all active modifiers retained.");
        }
        foreach (var unknown in new[] { Context(civics: null, technology: []), Context(civics: ["civic_agrarian_idyll"], technology: null), Context(civics: [], technology: [], wilderness: null) })
        {
            var result = initial.ReplaceInstalledCapacity("unknown-farming", 0, "colony", [new("farm", ConstructionCatalogAssessor.Assess(catalog, "district_farming", unknown, conditions))]);
            Check(!result.Accepted && ReferenceEquals(result.State, initial), "Unknown active farming facts were defaulted or partially applied.");
        }
        var sharedUnknown = Context(civics: [], technology: [], wilderness: null);
        var knownGenerator = ConstructionCatalogAssessor.Assess(catalog, "district_generator", sharedUnknown, conditions);
        var unknownFarming = ConstructionCatalogAssessor.Assess(catalog, "district_farming", sharedUnknown, conditions);
        Check(initial.ReplaceInstalledCapacity("known-first", 0, "colony", [new("generator", knownGenerator)]).Accepted, "Rollback fixture did not have a supported first object.");
        var unknownLater = initial.ReplaceInstalledCapacity("unknown-later", 0, "colony", [new("generator", knownGenerator), new("farm", unknownFarming)]);
        Check(!unknownLater.Accepted && ReferenceEquals(unknownLater.State, initial) && initial.ForkEconomy().GetInstalledJobCapacity(planet) is null, "Same-context later unknown farming modifier partially installed the first object.");
    }

    public static void RealCapitalRefusal(ConstructionCatalogProjection catalog, ConstructionCatalogProjection rural, ConstructionContext context, ConstructionCatalogProjection conditions)
    {
        var (initial, _) = Fixture();
        var assessment = ConstructionCatalogAssessor.Assess(catalog, catalog.Definitions.Keys.Single(), context, conditions);
        var farming = ConstructionCatalogAssessor.Assess(rural, "district_farming", context, conditions);
        Check(initial.ReplaceInstalledCapacity("supported-farming", 0, "colony", [new("farm", farming)]).Accepted, "Capital refusal fixture lacked a complete supported first object.");
        var result = initial.ReplaceInstalledCapacity("capital-incomplete", 0, "colony", [new("farm", farming), new("capital", assessment)]);
        Check(!result.Accepted && ReferenceEquals(result.State, initial), "Incomplete selected capital effects were partially installed.");
        var unresolved = assessment.Contributions.Where(item => item.Kind.Contains("modifier", StringComparison.Ordinal) && item.BlockingGates.Count != 0).ToArray();
        Check(unresolved.Length != 0, "Capital mandatory unsupported effect evidence disappeared.");
        foreach (var group in unresolved.GroupBy(item => item.Origin.Source))
        {
            Console.WriteLine($"Capital pending {assessment.DefinitionId}: {group.Key} / lines {string.Join(", ", group.Select(item => item.Origin.Line))} / {string.Join(", ", group.SelectMany(item => item.BlockingGates).Select(gate => gate.DependencyId).Distinct())}");
        }
    }

    public static void RealZone(ConstructionCatalogAssessment generator, ConstructionCatalogAssessment zone)
    {
        var (initial, planet) = Fixture();
        var result = initial.ReplaceInstalledCapacity("real-zone", 0, "colony", [new("generator", generator), new("energy-zone", zone, "explicit-generator-group", 3)]);
        Check(result.Accepted && result.State.ForkEconomy().GetInstalledJobCapacity(planet)!["technician"] == 500 && result.State.ForkEconomy().GetInstalledHousing(planet) == 200 && result.State.ForkEconomy().GetInstalledZoneBuildingSlotsAdd(planet) == 3, "Real generator/energy-zone raw additions or district scaling incorrect.");
        Check(result.State.ForkEconomy().GetBuildingUpkeep(planet)["energy"] == 5 && result.State.GetBalance(1, "energy") == 1000 && result.State.Month == 0 && result.State.Ledgers["colony"].Installed.Count == 0, "Raw zone application changed upkeep/resources/month or fabricated ledger installation.");
    }

    public static void RealOwnerFlags(ConstructionCatalogProjection catalog, ConstructionCatalogProjection conditions)
    {
        foreach (var flags in new IEnumerable<string>?[] { [], ["fallen_empire_1"], null })
        {
            var ownerFacts = Enum.GetValues<ConstructionOwnerPredicate>().ToDictionary(key => key, key => (bool?)(key is ConstructionOwnerPredicate.Regular or ConstructionOwnerPredicate.UsesFood));
            var planetFacts = Enum.GetValues<ConstructionPlanetPredicate>().ToDictionary(key => key, _ => (bool?)false);
            var context = new ConstructionContext(ownerExists: true, ownerFacts: ownerFacts, fromNomadic: false, modifiers: [], carrierFlags: [], technologies: [], sourceFacts: new ConstructionSourceFacts("standard", "default", [], [], [], [], planetFacts, validCivics: [], ownerFlags: flags));
            var assessment = ConstructionCatalogAssessor.Assess(catalog, "zone_research_unity", context, conditions);
            var regular = assessment.Contributions.Single(item => item.Origin.Source == "common/inline_scripts/jobs/zone_researchers_add.txt" && item.Origin.Line == 4);
            var fallen = assessment.Contributions.Single(item => item.Origin.Source == "common/inline_scripts/jobs/zone_researchers_add.txt" && item.Origin.Line == 468);
            var expected = flags is null ? ConstructionTruth.Unknown : flags.Any() ? ConstructionTruth.True : ConstructionTruth.False;
            Check(fallen.Condition.Truth == expected && regular.Condition.Truth == (expected == ConstructionTruth.Unknown ? ConstructionTruth.Unknown : expected == ConstructionTruth.True ? ConstructionTruth.False : ConstructionTruth.True), "Real flag-controlled source branches mis-evaluated.");
            Check(fallen.Amounts.Count(amount => amount.Resource == "job_fe_archivist_add") == 3 && fallen.Gates.Any(gate => gate.DependencyId == "catalog-duplicate-amount"), "Inactive duplicate raw source diagnostics were discarded or combined.");
            Check(fallen.BlockingGates.Any(gate => gate.DependencyId == "catalog-duplicate-amount") == (expected != ConstructionTruth.False), "Active/unresolved duplicate gating was guessed or inactive duplicates blocked.");
            var stage = ConstructionZoneContributionStager.Stage(assessment, "colony", "explicit-district", 0, 3);
            var (initial, _) = Fixture();
            var result = initial.ReplaceInstalledCapacity("research-flags", 0, "colony", [new("research", assessment, "explicit-district", 3)]);
            Check(stage.Gates.Any(gate => gate.DependencyId == "catalog-potential-not-declared") && !result.Accepted && ReferenceEquals(result.State, initial), "Flag closure defaulted unrelated missing housing potential or partially published state.");
            if (expected == ConstructionTruth.False)
            {
                Check(regular.Amounts.Count(amount => amount.Resource is "job_physicist_add" or "job_biologist_add" or "job_engineer_add") == 3 && regular.BlockingGates.Count == 0 && !stage.Gates.Any(gate => gate.DependencyId is "catalog-duplicate-amount" or "boolean-condition-value" or "owner-flags-context"), "Explicit empty flags did not close the research false-branch duplicate gate.");
            }
            Console.WriteLine($"Research owner flags: {(flags is null ? "unavailable" : flags.Any() ? "fallen_empire_1 present" : "explicit empty")} / FE condition {fallen.Condition.Truth} / {string.Join(", ", stage.Gates.Select(gate => gate.DependencyId).Distinct())}");
        }
    }

    public static void RealZoneBoundary(ConstructionCatalogProjection catalog, ConstructionContext context, ConstructionCatalogProjection conditions)
    {
        foreach (var id in new[] { "zone_default", "zone_energy", "zone_urban", "zone_research_unity", "zone_industrial" })
        {
            var (initial, _) = Fixture();
            var assessment = ConstructionCatalogAssessor.Assess(catalog, id, context, conditions);
            var stage = ConstructionZoneContributionStager.Stage(assessment, "colony", "explicit-district", 0, 3);
            var result = initial.ReplaceInstalledCapacity("zone-boundary", 0, "colony", [new("explicit-zone", assessment, "explicit-district", 3)]);
            if (id is "zone_default" or "zone_energy") { Check(result.Accepted, "Source-supported real zone raw application refused."); }
            else
            {
                Check(!result.Accepted && result.Code == "installed-capacity-unresolved" && ReferenceEquals(result.State, initial) && stage.Gates.Any(gate => gate.DependencyId == "catalog-potential-not-declared"), "Missing real triggered potential was defaulted or partially applied.");
                foreach (var contribution in assessment.Contributions.Where(item => item.BlockingGates.Count != 0 && item.Kind.Contains("modifier", StringComparison.Ordinal)))
                {
                    Console.WriteLine($"Zone pending {id}: {contribution.Origin.Source}:{contribution.Origin.Line} / {string.Join(", ", contribution.BlockingGates.Select(gate => gate.DependencyId))} / duplicate keys: {string.Join(", ", contribution.Amounts.GroupBy(amount => amount.Resource).Where(group => group.Count() > 1).Select(group => group.Key))}");
                }
            }
            Console.WriteLine($"Installed zone {id}: {(result.Accepted ? "raw application supported" : "unavailable")} / {string.Join(", ", stage.Gates.Select(gate => gate.DependencyId).Distinct())}");
        }
    }

    private static ConstructionCatalogAssessment Synthetic(decimal job, decimal housing, ConstructionContext context, bool? condition = true, bool unsupported = false, char hash = 'a', int jobKeys = 1, int blocks = 1, bool zone = false, decimal? multiplier = null, bool omitPotential = false)
    {
        var id = zone ? "zone_energy" : "building_hydroponics_farm";
        var origin = new ConstructionOrigin(zone ? "common/zones/00_zones.txt" : "common/buildings/03_resource_buildings.txt", 1, 1);
        ConstructionField Field(string key, string? scalar, params ConstructionField[] children) => new(key, "=", scalar, children, origin, []);
        var fields = new List<ConstructionField> { Field("potential", null, Field(condition is null ? "unresolved" : "always", condition == false ? "no" : "yes")), Field("job_farmer_add", job.ToString(System.Globalization.CultureInfo.InvariantCulture)), Field("planet_housing_add", housing.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        for (var index = 1; index < jobKeys; index++) { fields.Add(Field("job_farmer" + index + "_add", "1")); }
        if (unsupported) { fields.Add(Field("planet_amenities_add", "1")); }
        if (multiplier is not null) { fields.Add(Field("mult", multiplier.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))); }
        if (omitPotential) { fields.RemoveAt(0); }
        var key = zone ? "triggered_district_planet_modifier" : "triggered_planet_modifier";
        var roots = Enumerable.Range(0, blocks).Select(_ => Field(key, null, fields.ToArray())).ToList();
        if (zone) { roots.Add(Field("planet_modifier", null, Field("zone_building_slots_add", "3"))); }
        var definition = new ConstructionPlacementDefinition(id, zone ? "zone" : "building", origin, roots);
        var parameters = roots.SelectMany((root, block) => root.Children.Select((field, index) => (field, index)).Where(pair => pair.field.Scalar is not null).Select(pair => new ConstructionCatalogParameter(id, "/" + block + ":" + root.Key + "/" + pair.index + ":" + pair.field.Key, "=", decimal.Parse(pair.field.Scalar!, System.Globalization.CultureInfo.InvariantCulture), origin)));
        var source = new ConstructionCatalogProjection(zone ? "construction-zone-catalog-452-v1" : "construction-building-building_hydroponics_farm-catalog-452-v1", [definition], parameters, [new("launcher-settings.json", 10, new string(hash, 64))], []);
        return ConstructionCatalogAssessor.Assess(source, id, context);
    }

    private static (ConstructionAggregate Aggregate, EntityId Planet) Fixture()
    {
        var world = new EconomyWorld([new("energy", 10000), new("food", 10000)], [new("farmer", "worker", 100, new Dictionary<string, decimal> { ["food"] = 6 }, new Dictionary<string, decimal> { ["energy"] = 1 })], [new("worker", null)]);
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        world.SetBalance(country, "energy", 1000);
        world.SetBuildingUpkeep(planet, new Dictionary<string, decimal> { ["energy"] = 5 });
        world.AddGroup(planet, 300, 1);
        world.SetJobs(planet, "farmer", 1);
        var ledger = new ConstructionLedger(new("colony", ConstructionAuthorityMode.Individualist, false, null, "system"), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = world.GetBalances(country) }, ["slot"]);
        return (new(world, new Dictionary<int, EntityId> { [1] = country }, [new("colony", 1, planet)], [ledger]), planet);
    }

    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
