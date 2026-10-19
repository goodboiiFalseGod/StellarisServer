using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;

static class StartupBuildingChecks
{
    public static async Task Real(string gameRoot)
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), "Stellaris-StartupMissing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(missingRoot);
        try
        {
            try { _ = await M3StartupCatalog.ImportAsync(missingRoot); throw new Exception("Missing required startup source unexpectedly produced a legacy-capable catalog."); }
            catch (StartupBuildingImportException) { }
            try { _ = await new StartupBuildingImporter().GenerateAsync(missingRoot, new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20)); throw new Exception("Missing startup building source unexpectedly imported."); }
            catch (StartupBuildingImportException) { }
        }
        finally { Directory.Delete(missingRoot, true); }

        var startProfile = new StartupPopulationResourceImporter().Import(gameRoot);
        Assert(startProfile.Population == 3200, "Regular source pop threshold + origin_default group did not produce the ordinary start population.");
        Assert(startProfile.StartingStocks.Count == 8 && startProfile.StartingStocks["energy"] == 200 && startProfile.StartingStocks["minerals"] == 200
            && startProfile.StartingStocks["alloys"] == 200 && startProfile.StartingStocks["trade"] == 200 && startProfile.StartingStocks["influence"] == 100
            && startProfile.StartingStocks["unity"] == 50 && startProfile.StartingStocks["food"] == 200 && startProfile.StartingStocks["consumer_goods"] == 100,
            "Ordinary source start stock branches changed or were not combined.");
        Assert(startProfile.Sources.Files.Any(file => file.Path == "common/scripted_effects/01_start_of_game_effects.txt")
            && startProfile.Sources.Files.Any(file => file.Path == "common/inline_scripts/game_start/origin_default.txt")
            && startProfile.Sources.Files.Any(file => file.Path == "common/defines/00_defines.txt"), "Start population/resource provenance is incomplete.");

        var initializerCatalog = await new StartupHomeworldInitializerImporter().ImportAsync(gameRoot);
        Assert(initializerCatalog.MinimumSize == 18 && initializerCatalog.MaximumSize == 21, "Homeworld bounds did not come from the complete source pool.");
        var selections = Enumerable.Range(1, 64).Select(seed => initializerCatalog.Select("pc_continental", seed)).ToArray();
        Assert(selections.All(item => item.PlanetClass == "pc_continental" && item.PlanetSize is >= 18 and <= 21), "Homeworld selection exceeded the imported class/bounds.");
        Assert(selections.Select(item => item.InitializerId).Distinct(StringComparer.Ordinal).Count() > 1, "Seeded selection did not vary across source candidates.");

        var runtimeCatalog = await M3StartupCatalog.ImportAsync(gameRoot);
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var selection = runtimeCatalog.Select(templateId, 22);
            Assert(selection.Plan.Context.PlanetSize is >= 18 and <= 21, "Runtime catalog didn't connect source homeworld size to an actual startup plan.");
            Assert(!selection.Plan.Gates.Any(gate => gate.SourcePath is "common/inline_scripts/game_start/rural_districts_placement.txt" or "common/inline_scripts/game_start/origin_default.txt"), "Supported ordinary preset retained a rural/origin source gate.");
            Assert(selection.Plan.Districts.Single(item => item.DefinitionId == "district_city").Level == 3, "A supported ordinary preset missed source-selected default city plus two authority city adds.");
            Assert(selection.Plan.Districts.Any(item => item.DefinitionId == "district_farming"), "Ordinary continental rural placement did not produce farming districts.");
            Assert(selection.Plan.Features.Count > 0 && selection.Plan.DistrictCaps.Count > 0, "Source deposits/blockers or effective district caps were not materialized.");
            Assert(selection.Plan.Features.Select(item => item.Order).SequenceEqual(Enumerable.Range(0, selection.Plan.Features.Count)), "Feature instances did not preserve source effect order and duplicates.");
            Assert(selection.Plan.Districts.Sum(item => item.Level) <= selection.Plan.FinalPlanetSize + selection.Plan.Context.DistrictCapacityModifier + selection.Plan.DistrictCaps.Sum(item => item.ModifierId == "planet_max_districts_add" ? item.Value : 0), "Final districts exceed the source-derived effective total capacity.");
            var colony = M3StartupColony.Create("source-world", 41, selection.Homeworld, selection.Plan);
            Assert(colony.Features.Count == selection.Plan.Features.Count && colony.Zones.Count == selection.Plan.Zones.Count && colony.Districts.Count == selection.Plan.Districts.Count, "M3 startup colony omitted source feature/district/zone instances.");
            Assert(colony.Features.Where(item => item.HiddenByBlockerInstanceId is not null).All(item => colony.Features.Any(blocker => blocker.InstanceId == item.HiddenByBlockerInstanceId && blocker.HiddenFeatureInstanceId == item.InstanceId)), "M3 hidden feature links are not reciprocal instance references.");
            Assert(colony.Buildings.SelectMany(item => item.CompatibleZoneInstanceIds).All(id => colony.Zones.Any(zone => zone.InstanceId == id)), "M3 building zone compatibility references a missing zone instance.");
        }

        var ordinary = await new StartupBuildingImporter().GenerateAsync(gameRoot,
            new StartupOwnerContext(StartupAuthority.Regular,
                ["ethic_fanatic_authoritarian", "ethic_materialist"],
                ["civic_mining_guilds", "civic_functional_architecture"],
                "origin_default", 20, planetClass: "pc_continental", countryUsesFoodGameStart: true, blockerChoiceSeed: 73, sourcePredicateFacts: OrdinaryFacts()));
        Assert(!ordinary.Gates.Any(gate => gate.SourcePath is "common/inline_scripts/game_start/rural_districts_placement.txt" or "common/inline_scripts/game_start/origin_default.txt"), "Supported ordinary context retained a rural/origin source gate.");
        Assert(ordinary.Districts.Single(item => item.DefinitionId == "district_city").Level == 3, "Source default starting city level and ordered authority adds were not materialized.");
        Assert(ordinary.Districts.Any(item => item.DefinitionId == "district_farming"), "Default origin rural district count was not executed from source.");
        Assert(ordinary.Features.Any(item => item.IsBlocker && item.HiddenFeatureInstanceId is not null) && ordinary.Features.Any(item => item.IsHidden && item.HiddenByBlockerInstanceId is not null), "Blocker/deposit swap links were not materialized.");
        Assert(ordinary.Features.Count(item => item.DefinitionId == "d_failing_infrastructure") == 2, "Repeated source blocker instances were collapsed.");
        Assert(ordinary.Features.Count(item => item.DefinitionId == "d_rugged_woods") == 2, "Repeated continental and ordinary source deposit occurrences were collapsed.");
        var seededSelections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seed in Enumerable.Range(0, 12))
        {
            var seeded = await new StartupBuildingImporter().GenerateAsync(gameRoot,
                new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20, countryUsesFoodGameStart: true, blockerChoiceSeed: seed, sourcePredicateFacts: OrdinaryFacts()));
            foreach (var blocker in seeded.Features.Where(item => item.IsBlocker && item.HiddenFeatureInstanceId is not null))
            {
                var hidden = seeded.Features.Single(item => item.InstanceId == blocker.HiddenFeatureInstanceId);
                var allowed = blocker.DefinitionId == "d_failing_infrastructure"
                    ? new[] { "d_rushing_waterfalls", "d_searing_desert", "d_frozen_gas_lake", "d_prosperous_mesa", "d_ore_rich_caverns", "d_fungal_caves", "d_lush_jungle", "d_fertile_lands", "d_great_river" }
                    : Array.Empty<string>();
                Assert(allowed.Contains(hidden.DefinitionId, StringComparer.Ordinal), "Blocker selected a target outside its source swap list.");
                seededSelections.Add(hidden.DefinitionId);
            }
        }
        Assert(seededSelections.Count > 1, "Blocker selection did not respond to the explicit task-local seed.");
        var capacityContext = new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20, countryUsesFoodGameStart: true, blockerChoiceSeed: 777, sourcePredicateFacts: OrdinaryFacts());
        var sourceCapPlan = await new StartupBuildingImporter().GenerateAsync(gameRoot, capacityContext);
        var changedCapImporter = new StartupBuildingImporter(_ => Task.CompletedTask, relative =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (relative != "common/deposits/01_planetary_deposits.txt") { return bytes; }
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var changed = new System.Text.RegularExpressions.Regex("(?s)(d_hot_springs\\s*=\\s*\\{.*?district_generator_max_add\\s*=\\s*)1")
                .Replace(text, "${1}4", 1);
            return System.Text.Encoding.UTF8.GetBytes(changed);
        });
        var changedCapPlan = await changedCapImporter.GenerateAsync(gameRoot, capacityContext);
        Assert(changedCapPlan.DistrictCaps.Single(item => item.ModifierId == "district_generator_max_add").Value == sourceCapPlan.DistrictCaps.Single(item => item.ModifierId == "district_generator_max_add").Value + 3,
            "Live district cap did not follow a changed imported active deposit modifier.");
        var orderedDepositOverlayApplied = false;
        var orderedDepositMutationImporter = new StartupBuildingImporter(_ => Task.CompletedTask, relative =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (relative != "common/scripted_effects/01_start_of_game_effects.txt") { return bytes; }
            orderedDepositOverlayApplied = true;
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var changed = new System.Text.RegularExpressions.Regex(
                "(?s)(generate_start_deposits_and_blockers\\s*=\\s*\\{.*?)(\\r?\\n\\}\\r?\\n\\r?\\n# Special for Earth)")
                .Replace(text, "$1\n    clear_deposits = yes\n    add_deposit = d_hot_springs$2", 1);
            var generator = ParadoxParser.Parse(changed, relative).Named("generate_start_deposits_and_blockers").Single().AsBlock();
            if (!generator.Entries.TakeLast(2).Select(item => item.Key).SequenceEqual(["clear_deposits", "add_deposit"]))
            {
                throw new Exception("Ordered deposit source overlay did not append at the generator root.");
            }
            return System.Text.Encoding.UTF8.GetBytes(changed);
        });
        var orderedDepositMutation = await orderedDepositMutationImporter.GenerateAsync(gameRoot, capacityContext);
        Assert(orderedDepositOverlayApplied, "Ordered deposit test overlay was not read.");
        Assert(orderedDepositMutation.Features.Select(item => item.DefinitionId).SequenceEqual(["d_hot_springs"]),
            "Source-ordered clear_deposits/add_deposit mutation did not replace prior feature instances: " + string.Join(",", orderedDepositMutation.Features.Select(item => item.DefinitionId)) + ".");
        Assert(orderedDepositMutation.DistrictCaps.Single(item => item.ModifierId == "planet_max_districts_add").Value == 0 &&
            orderedDepositMutation.DistrictCaps.Single(item => item.ModifierId == "district_generator_max_add").Value == 1,
            "Effective caps retained cleared feature modifiers after the ordered deposit mutation.");
        var recurringZoneOverlayImporter = new StartupBuildingImporter(_ => Task.CompletedTask, relative =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (relative != "common/inline_scripts/buildings/nomadic_cost_switcher.txt") { return bytes; }
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var changed = text.Replace("cost = {", "cost = {\n\tupkeep = { energy = 1 }", StringComparison.Ordinal);
            return System.Text.Encoding.UTF8.GetBytes(changed);
        });
        try
        {
            _ = await recurringZoneOverlayImporter.GenerateAsync(gameRoot, capacityContext);
            throw new Exception("zone_default acquired a recurring flow but retained a zero-flow proof.");
        }
        catch (StartupBuildingImportException) { }
        Assert(ordinary.Features.Where(item => item.IsBlocker).All(item => item.HiddenFeatureInstanceId is not null || !ordinary.Sources.Any(source => source.RelativePath == "common/deposits/00_blockers.txt")), "A swapping blocker is missing its hidden deposit link.");
        Assert(ordinary.Zones.Any(zone => zone.ZoneId == "zone_default" && zone.SlotType == "slot_city_government") && ordinary.Zones.Any(zone => zone.ZoneId == "zone_research_unity") && ordinary.Zones.Any(zone => zone.ZoneId == "zone_industrial"), "Ordinary zone slot operations were not materialized.");
        var defaultZoneProof = ordinary.ZoneRecurringEconomyProofs.Single(proof => proof.ZoneId == "zone_default");
        Assert(ordinary.Sources.Any(file => file.RelativePath == defaultZoneProof.DefinitionPath && file.Sha256 == defaultZoneProof.DefinitionSha256)
            && ordinary.Sources.Any(file => file.RelativePath == defaultZoneProof.InlinePath && file.Sha256 == defaultZoneProof.InlineSha256),
            "Source-confirmed zone_default zero-flow proof is not bound to both source hashes.");
        Assert(ordinary.Buildings.Select(item => item.DefinitionId).ToHashSet(StringComparer.Ordinal).SetEquals(["building_capital", "building_factory_1", "building_precinct_house", "building_research_lab_1"]), "Ordinary authority/ethic branches differ from the imported source effects.");
        Assert(ordinary.Sources.Any(file => file.RelativePath.EndsWith("authority_regular.txt", StringComparison.Ordinal)) && ordinary.Sources.Any(file => file.RelativePath == "common/inline_scripts/game_start/origin_default.txt") && ordinary.Sources.Any(file => file.RelativePath == "common/scripted_effects/01_start_of_game_effects.txt"), "Source provenance is incomplete or skipped the selected default origin position.");

        var hive = await new StartupBuildingImporter().GenerateAsync(gameRoot,
            new StartupOwnerContext(StartupAuthority.Hive, [], [], "origin_default", 20));
        Assert(hive.Districts.Single(item => item.DefinitionId == "district_hive").Level == 2, "Hive district count is not source-derived.");
        Assert(hive.Buildings.Any(item => item.DefinitionId == "building_hive_capital") && hive.Buildings.Any(item => item.DefinitionId == "building_spawning_pool") && hive.Buildings.Any(item => item.DefinitionId == "building_hive_warren"), "Hive authority source branch was not executed.");

        var machine = await new StartupBuildingImporter().GenerateAsync(gameRoot,
            new StartupOwnerContext(StartupAuthority.Machine, [], ["civic_machine_servitor"], "origin_default", 20));
        Assert(machine.Districts.Single(item => item.DefinitionId == "district_nexus").Level == 2, "Machine district count is not source-derived.");
        Assert(machine.Zones.Any(zone => zone.ZoneId == "zone_industrial") && machine.Buildings.Any(item => item.DefinitionId == "building_factory_1"), "Machine civic zone swap did not follow the source predicate.");

        var ordinaryWithoutCivic = await new StartupBuildingImporter().GenerateAsync(gameRoot,
            new StartupOwnerContext(StartupAuthority.Regular, ["ethic_fanatic_authoritarian"], ["civic_civil_education"], "origin_default", 20, countryUsesFoodGameStart: true, sourcePredicateFacts: OrdinaryFacts(["civic_civil_education"])));
        Assert(ordinaryWithoutCivic.Buildings.All(item => item.DefinitionId != "building_precinct_house"), "Civil education did not suppress the authoritarian source branch.");

        try
        {
            _ = await new StartupBuildingImporter().GenerateAsync(gameRoot,
                new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20, sourcePredicateFacts: OrdinaryFacts()));
            throw new Exception("Unknown country_uses_food_game_start fact unexpectedly published a plan.");
        }
        catch (StartupBuildingImportException exception) when (exception.Message.Contains("country_uses_food_game_start", StringComparison.Ordinal)) { }

        try
        {
            _ = await new StartupBuildingImporter().GenerateAsync(gameRoot,
                new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20, countryUsesFoodGameStart: true));
            throw new Exception("Unknown source owner predicate was treated as false.");
        }
        catch (StartupBuildingImportException exception) when (exception.Message.Contains("is_apocalyptic_empire", StringComparison.Ordinal)) { }

        var changedDuringImport = false;
        var mutatingImporter = new StartupBuildingImporter(_ => { changedDuringImport = true; return Task.CompletedTask; }, relative =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!changedDuringImport || relative != "common/scripted_effects/01_start_of_game_effects.txt") { return bytes; }
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var changed = text.Replace("add_deposit = d_failing_infrastructure", "add_deposit = d_failing_infrastructure_changed", StringComparison.Ordinal);
            return System.Text.Encoding.UTF8.GetBytes(changed);
        });
        try
        {
            _ = await mutatingImporter.GenerateAsync(gameRoot, new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20, countryUsesFoodGameStart: true, sourcePredicateFacts: OrdinaryFacts()));
            throw new Exception("Changed reached startup source passed stable-source verification.");
        }
        catch (StartupBuildingImportException exception) when (exception.Message.Contains("changed during import", StringComparison.Ordinal)) { }

        var malformedImporter = new StartupBuildingImporter(_ => Task.CompletedTask, relative => relative == "common/scripted_effects/01_start_of_game_effects.txt"
            ? System.Text.Encoding.UTF8.GetBytes("generate_start_buildings_and_districts = {\n")
            : File.ReadAllBytes(Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar))));
        try
        {
            _ = await malformedImporter.GenerateAsync(gameRoot, new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20));
            throw new Exception("Malformed reached startup source unexpectedly parsed.");
        }
        catch (StartupBuildingImportException exception) when (exception.Message.Contains("Malformed startup source", StringComparison.Ordinal)) { }

        var noSwapTargetImporter = new StartupBuildingImporter(_ => Task.CompletedTask, relative =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (relative != "common/deposits/01_blocker_deposits.txt") { return bytes; }
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var changed = new System.Text.RegularExpressions.Regex(
                "(?s)(d_failing_infrastructure\\s*=\\s*\\{.*?blocker_swap_types\\s*=\\s*\\{).*?(\\})")
                .Replace(text, "$1\nd_sourceworld_missing_target\n$2", 1);
            return System.Text.Encoding.UTF8.GetBytes(changed);
        });
        try
        {
            _ = await noSwapTargetImporter.GenerateAsync(gameRoot, new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20, countryUsesFoodGameStart: true, sourcePredicateFacts: OrdinaryFacts()));
            throw new Exception("Missing source blocker target unexpectedly imported.");
        }
        catch (StartupBuildingImportException exception) when (exception.Message.Contains("no active suitable blocker_swap_types target", StringComparison.Ordinal)) { }

        var missingDefaultDistrictImporter = new StartupBuildingImporter(_ => Task.CompletedTask, relative =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (relative != "common/districts/00_urban_districts.txt") { return bytes; }
            var changed = System.Text.Encoding.UTF8.GetString(bytes).Replace("default_starting_district = yes", "default_starting_district = no", StringComparison.Ordinal);
            return System.Text.Encoding.UTF8.GetBytes(changed);
        });
        try
        {
            _ = await missingDefaultDistrictImporter.GenerateAsync(gameRoot, new StartupOwnerContext(StartupAuthority.Regular, [], [], "origin_default", 20, countryUsesFoodGameStart: true, sourcePredicateFacts: OrdinaryFacts()));
            throw new Exception("Missing source default-starting district unexpectedly imported.");
        }
        catch (StartupBuildingImportException exception) when (exception.Message.Contains("Expected one source default_starting_district", StringComparison.Ordinal)) { }
    }

    private static IReadOnlyDictionary<string, bool?> OrdinaryFacts(IEnumerable<string>? civics = null) => new Dictionary<string, bool?>(StringComparer.Ordinal)
    {
        ["is_apocalyptic_empire"] = false,
        ["is_environmental_architects_empire"] = (civics ?? []).Contains("civic_environmental_architects", StringComparer.Ordinal),
        ["is_lithoid_empire"] = false,
        ["is_lithoid_or_infernal_empire"] = false,
        ["is_lithoid_devouring_swarm"] = false,
        ["has_carrier_flag:planet_unity"] = false,
        ["has_carrier_flag:uncapped_energy"] = false,
        ["has_carrier_flag:wilderness_planet"] = false,
        ["has_carrier_flag:uncapped_mining"] = false,
        ["has_carrier_flag:uncapped_farming"] = false
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition) { throw new Exception(message); }
    }
}
