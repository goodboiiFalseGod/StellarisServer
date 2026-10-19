using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Читаем настоящий каталог и startup; не создаём фиктивные ресурсы, технологии или installed buildings.
internal static class ResourceBuildingSourceChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check, bool science = false, bool scienceUpgrades = false, bool scienceTier3 = false)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("source-resource-buildings", 1, home.Homeworld, home.Plan);
        var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
        var template = ConstructionOrdinaryTemplate.Get("economic");
        var context = content.Context(startup, template, runtime, M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
        if (scienceTier3)
        {
            CheckScienceTier3(root, economy, content, context, check);
            return;
        }
        if (scienceUpgrades)
        {
            CheckScienceUpgrades(economy, content, context, check);
            return;
        }
        var definitions = science ? new[]
        {
            ("building_physics_lab_1", "planet_physicists_physics_research_produces_add", "tech_physics_1"),
            ("building_biolab_1", "planet_biologists_society_research_produces_add", "tech_society_1"),
            ("building_engineering_facility_1", "planet_engineers_engineering_research_produces_add", "tech_engineering_1")
        } : new[]
        {
            ("building_energy_grid", "planet_technician_energy_produces_add", "tech_power_hub_1"),
            ("building_mineral_purification_plant", "planet_miners_minerals_produces_add", "tech_mineral_purification_1"),
            ("building_food_processing_facility", "planet_farmers_food_produces_add", "tech_food_processing_1")
        };
        foreach (var (id, leaf, tech) in definitions)
        {
            var definition = economy.Buildings[id];
            check(!content.RuntimeGates.ContainsKey(id), "source resource economy supported: " + id + " " + content.RuntimeGateEvidence.GetValueOrDefault(id));
            var term = definition.Terms.Single(term => term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == leaf);
            check(term.Value == (science ? .25m : .5m) && term.ApplicationScope == PlanetEconomyApplicationScope.PerDefinitionInstance,
                "source production Add per building: " + id);
            var catalog = content.Content.Catalogs[id];
            var assessment = ConstructionCatalogAssessor.Assess(catalog, id, context, content.Content.Conditions);
            check(assessment.Potential.Truth == ConstructionTruth.True && assessment.Destroy.Truth == ConstructionTruth.False,
                "source resource ordinary conditions: " + id);
            check(catalog.Definitions[id].Fields.Single(field => field.Key == "prerequisites").Children.Any(field => field.Scalar == tech) && assessment.Prerequisites.Truth == ConstructionTruth.False,
                "source resource technology remains required: " + id);
            var quote = content.Content.Quote(id, context, -.15m, .15m, M3ConstructionContent.EconomyFacts(startup));
            check(quote.Quote is null && quote.Code == "not-eligible", "source resource cannot bypass technology: " + id);
        }
    }

    private static void CheckScienceUpgrades(PlanetEconomyCatalog economy, M3ConstructionContent content, ConstructionContext context, Action<bool, string> check)
    {
        var definitions = new[]
        {
            (Tier1: "building_physics_lab_1", Tier2: "building_physics_lab_2", Tier3: "building_physics_lab_3", Production: "planet_physicists_physics_research_produces_add", Upkeep: "planet_physicists_volatile_motes_upkeep_add", Technology: "tech_physics_2", Tier3WorkforceLeaf: "physicist_jobs_bonus_workforce_mult"),
            (Tier1: "building_biolab_1", Tier2: "building_biolab_2", Tier3: "building_biolab_3", Production: "planet_biologists_society_research_produces_add", Upkeep: "planet_biologists_exotic_gases_upkeep_add", Technology: "tech_society_2", Tier3WorkforceLeaf: "biologist_jobs_bonus_workforce_mult"),
            (Tier1: "building_engineering_facility_1", Tier2: "building_engineering_facility_2", Tier3: "building_engineering_facility_3", Production: "planet_engineers_engineering_research_produces_add", Upkeep: "planet_engineers_rare_crystals_upkeep_add", Technology: "tech_engineering_2", Tier3WorkforceLeaf: "engineer_jobs_bonus_workforce_mult")
        };

        foreach (var item in definitions)
        {
            var definition = economy.Buildings[item.Tier2];
            check(!content.RuntimeGates.ContainsKey(item.Tier2), "source tier2 science building has no M3 runtime gate: " + item.Tier2);
            var production = definition.Terms.Single(term => term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == item.Production);
            var upkeep = definition.Terms.Single(term => term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == item.Upkeep);
            check(production.Value == .5m && production.ApplicationScope == PlanetEconomyApplicationScope.PerDefinitionInstance,
                "source tier2 production Add .5 per definition instance: " + item.Tier2);
            check(upkeep.Value == .25m && upkeep.ApplicationScope == PlanetEconomyApplicationScope.PerDefinitionInstance,
                "source tier2 job upkeep Add .25 per definition instance: " + item.Tier2);

            var catalog = content.Content.Catalogs[item.Tier2];
            var assessment = ConstructionCatalogAssessor.Assess(catalog, item.Tier2, context, content.Content.Conditions);
            check(!assessment.Contributions.Where(contribution => contribution.Condition.Truth != ConstructionTruth.False)
                    .Any(contribution => contribution.BlockingGates.Count != 0),
                "source tier2 active contributions have no blocking gates: " + item.Tier2);
            var fields = catalog.Definitions[item.Tier2].Fields;
            check(fields.Any(field => field.Key == "can_build" && field.Scalar == "no"), "source tier2 can_build=no preserved: " + item.Tier2);
            check(fields.Any(field => field.Key == "prerequisites" && field.Children.Any(child => child.Scalar == item.Technology))
                && assessment.Prerequisites.Truth == ConstructionTruth.False,
                "source tier2 prerequisite is absent from preset technologies: " + item.Tier2);
            check(content.Content.Catalogs[item.Tier1].Definitions[item.Tier1].Fields.Any(field => field.Key == "upgrades" && field.Children.Any(child => child.Scalar == item.Tier2)),
                "source tier1 upgrade edge to tier2 preserved: " + item.Tier1);

            var tier3Catalog = content.Content.Catalogs[item.Tier3];
            var tier3 = ConstructionCatalogAssessor.Assess(tier3Catalog, item.Tier3, context, content.Content.Conditions);
            check(!content.RuntimeGates.ContainsKey(item.Tier3), "source tier3 science building has no M3 runtime gate: " + item.Tier3);
            check(!tier3.Contributions.Where(contribution => contribution.Condition.Truth != ConstructionTruth.False)
                    .Any(contribution => contribution.BlockingGates.Count != 0),
                "source tier3 active contributions have no blocking gates: " + item.Tier3);
        }
    }

    private static void CheckScienceTier3(string root, PlanetEconomyCatalog economy, M3ConstructionContent content, ConstructionContext context, Action<bool, string> check)
    {
        var source = new ContentSession(root);
        var parsedModifiers = source.LoadFile(PlanetEconomyImporter.RareCompositeSource).Root;
        var definitions = new[]
        {
            (Tier2: "building_physics_lab_2", Tier3: "building_physics_lab_3", WorkforceLeaf: "physicist_jobs_bonus_workforce_mult", Technology: "tech_physics_3"),
            (Tier2: "building_biolab_2", Tier3: "building_biolab_3", WorkforceLeaf: "biologist_jobs_bonus_workforce_mult", Technology: "tech_society_3"),
            (Tier2: "building_engineering_facility_2", Tier3: "building_engineering_facility_3", WorkforceLeaf: "engineer_jobs_bonus_workforce_mult", Technology: "tech_engineering_3")
        };

        foreach (var item in definitions)
        {
            var definition = economy.Buildings[item.Tier3];
            check(!content.RuntimeGates.ContainsKey(item.Tier3), "source tier3 science building has no M3 runtime gate: " + item.Tier3);
            var term = definition.Terms.Single(term => term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == item.WorkforceLeaf);
            check(term.Value == .1m && term.ApplicationScope == PlanetEconomyApplicationScope.PerDefinitionInstance,
                "source tier3 job bonus +.1 per definition instance: " + item.Tier3);
            var modifier = parsedModifiers.Named(item.WorkforceLeaf).Single().AsBlock();
            var mappedJobs = ConstructionJobWorkforceMapping.Find(item.WorkforceLeaf);
            var nativeEntries = modifier.Entries.Where(entry => entry.Key != "custom_tooltip").ToArray();
            var expectedLeaves = mappedJobs?.Select(job => "pop_" + job + "_bonus_workforce_mult").Order(StringComparer.Ordinal).ToArray();
            check(expectedLeaves is not null && nativeEntries.Select(entry => entry.Key).Order(StringComparer.Ordinal)
                    .SequenceEqual(expectedLeaves, StringComparer.Ordinal)
                && nativeEntries.All(entry => entry.Operator == "=" && decimal.Parse(entry.AsScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1m),
                "native static modifier expands to exactly the mapped three source job leaves at 1: " + item.WorkforceLeaf);

            var catalog = content.Content.Catalogs[item.Tier3];
            var assessment = ConstructionCatalogAssessor.Assess(catalog, item.Tier3, context, content.Content.Conditions);
            check(!assessment.Contributions.Where(contribution => contribution.Condition.Truth != ConstructionTruth.False)
                    .Any(contribution => contribution.BlockingGates.Count != 0),
                "source tier3 active contributions have no blocking gates: " + item.Tier3);
            check(catalog.Definitions[item.Tier3].Fields.Any(field => field.Key == "can_build" && field.Scalar == "no"),
                "source tier3 can_build=no preserved: " + item.Tier3);
            check(catalog.Definitions[item.Tier3].Fields.Any(field => field.Key == "prerequisites" && field.Children.Any(child => child.Scalar == item.Technology))
                && assessment.Prerequisites.Truth == ConstructionTruth.False,
                "source tier3 prerequisite remains required and absent from preset technologies: " + item.Tier3);
            check(content.Content.Catalogs[item.Tier2].Definitions[item.Tier2].Fields.Any(field => field.Key == "upgrades" && field.Children.Any(child => child.Scalar == item.Tier3)),
                "source tier2 upgrade edge to tier3 preserved: " + item.Tier2);
        }
        source.VerifyStable();
    }
}
