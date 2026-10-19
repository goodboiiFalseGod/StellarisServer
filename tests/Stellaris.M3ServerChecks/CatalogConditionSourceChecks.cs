using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Только чтение реального source import 4.5.2 и выбранного startup-контекста.
internal static class CatalogConditionSourceChecks
{
    private static readonly (string Zone, string Helper, string[] ExpectedDeposits, string Technology)[] RareZones =
    [
        ("zone_volatile_motes", "has_volatile_motes_deposit", ["d_dust_caverns", "d_dust_desert", "d_industrial_sector", "d_explosive_atmosphere", "d_particle_storm_3_motes"], "tech_mine_volatile_motes"),
        ("zone_exotic_gases", "has_exotic_gases_deposit", ["d_bubbling_swamp", "d_fuming_bog", "d_industrial_sector", "d_exotic_mountain", "d_bogplants", "d_toxic_god_envenomed_seas_upgraded"], "tech_mine_exotic_gases"),
        ("zone_rare_crystals", "has_rare_crystals_deposit", ["d_crystalline_caverns", "d_crystal_forest", "d_crystal_reef", "d_industrial_sector", "d_crystaline_growths", "d_crystal_kraken_body", "d_crystal_kraken_body_bombed", "d_crystalline_glacier", "d_celestial_storm_1_mines", "d_celestial_storm_3_crystal"], "tech_mine_rare_crystals")
    ];

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("source-catalog-conditions", 1, home.Homeworld, home.Plan);
        var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
        var template = ConstructionOrdinaryTemplate.Get("economic");
        var context = content.Context(startup, template, runtime, M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
        var facts = context.SourceFacts ?? throw new InvalidOperationException("Actual startup source facts are unavailable.");
        check(facts.OwnerHeldTraditions is { Count: 0 },
            "actual M3 startup supplies known-empty received-traditions snapshot");

        var visibleFeatureDefinitions = startup.Features.Where(feature => !feature.IsHidden)
            .Select(feature => feature.DefinitionId).ToHashSet(StringComparer.Ordinal);
        check(facts.Deposits is not null && facts.Deposits.SetEquals(visibleFeatureDefinitions),
            "catalog source facts contain exactly visible startup feature definitions");
        check(facts.OwnerOrigin == startup.Plan.Context.Origin,
            "catalog source owner origin equals actual startup origin");

        var conditionCatalog = content.Content.Conditions;
        var helperDeposits = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var item in RareZones)
        {
            check(conditionCatalog.Definitions.TryGetValue(item.Helper, out var sourceHelper) && sourceHelper.Kind == "planetCondition"
                && sourceHelper.Origin.Source == "common/scripted_triggers/01_scripted_triggers_buildings.txt",
                "source helper imported as planetCondition: " + item.Helper);
            var helper = conditionCatalog.Definitions[item.Helper];
            var elseField = helper.Fields.SingleOrDefault(field => field.Key == "else" && field.Operator == "=" && field.Children.Count != 0);
            var orField = elseField?.Children.SingleOrDefault(field => field.Key == "OR" && field.Operator == "=" && field.Children.Count != 0);
            check(orField is not null, "source helper has ordinary else OR branch: " + item.Helper);
            var nativeDeposits = orField!.Children.Where(field => field.Key == "has_deposit" && field.Operator == "=" && field.Children.Count == 0)
                .Select(field => field.Scalar).ToArray();
            check(nativeDeposits.All(id => id is not null) && nativeDeposits.Distinct(StringComparer.Ordinal).Count() == nativeDeposits.Length
                && nativeDeposits.Order(StringComparer.Ordinal).SequenceEqual(item.ExpectedDeposits.Order(StringComparer.Ordinal), StringComparer.Ordinal),
                "source helper ordinary OR retains exact literal deposit IDs: " + item.Helper);
            helperDeposits.Add(item.Helper, nativeDeposits.Select(id => id!).ToHashSet(StringComparer.Ordinal));
        }

        foreach (var item in RareZones)
        {
            var candidateOverlap = helperDeposits[item.Helper].Overlaps(facts.Deposits!);
            var zoneCatalog = content.Content.Catalogs[item.Zone];
            var definition = zoneCatalog.Definitions[item.Zone];
            var potentialFields = definition.Fields.Where(field => field.Key == "potential").SelectMany(field => Flatten(field.Children)).ToArray();
            check(potentialFields.Any(field => field.Key == item.Helper && field.Operator == "=" && field.Scalar == "yes"),
                "source rare-resource zone potential calls its native helper: " + item.Zone);
            var assessment = ConstructionCatalogAssessor.Assess(zoneCatalog, item.Zone, context, conditionCatalog);
            check(assessment.Potential.Truth == (candidateOverlap ? ConstructionTruth.True : ConstructionTruth.False),
                "actual visible deposit overlap resolves zone potential: " + item.Zone);
            var unlockFields = definition.Fields.Where(field => field.Key == "unlock").SelectMany(field => Flatten(field.Children)).ToArray();
            check(unlockFields.Any(field => field.Key == "has_technology" && field.Scalar == item.Technology)
                && !context.Technologies!.Contains(item.Technology) && assessment.Unlock.Truth == ConstructionTruth.False,
                "source technology unlock remains absent from actual starting technologies: " + item.Zone);
        }

        var luxuryCatalog = content.Content.Catalogs["building_luxury_residence"];
        var luxury = luxuryCatalog.Definitions["building_luxury_residence"];
        var luxuryAssessment = ConstructionCatalogAssessor.Assess(luxuryCatalog, "building_luxury_residence", context, conditionCatalog);
        var luxuryOriginFields = Flatten(luxury.Fields).Where(field => field.Key == "has_origin" && field.Scalar == "origin_forever_cruise").ToArray();
        check(luxuryOriginFields.Length != 0 && luxuryOriginFields.All(field => field.Origin.Source == "common/buildings/07_amenity_buildings.txt"),
            "luxury residence origin guards are retained from source");
        check(!luxuryAssessment.Potential.BlockingGates.Any(gate => gate.DependencyId == "owner-origin-context")
            && !luxuryAssessment.Destroy.BlockingGates.Any(gate => gate.DependencyId == "owner-origin-context"),
            "luxury residence potential and destroy resolve actual startup origin");
        check(luxuryAssessment.Potential.Truth == ConstructionTruth.True
            && luxuryAssessment.Contributions.Any(contribution => contribution.Condition.Truth == ConstructionTruth.True
                && contribution.BlockingGates.Any(gate => gate.DependencyId == "catalog-modifier-planet_amenities_add")),
            "luxury residence source guards resolve while amenity contribution remains unsupported");
        var luxuryQuote = content.Content.Quote("building_luxury_residence", context, 0m, 0m,
            M3ConstructionContent.EconomyFacts(startup));
        check(luxuryQuote.Quote is null && luxuryQuote.Code == "source-effect-unresolved:catalog-modifier-planet_amenities_add",
            "actual luxury quote exposes unresolved source amenity effect after source guards pass");
        check(luxuryAssessment.Destroy.Truth == ConstructionTruth.False,
            "luxury residence destroy trigger resolves false for actual startup origin and owner facts");

        const string clonePath = "common/buildings/01_pop_assembly_buildings.txt";
        var nativeClone = new ContentSession(root).LoadFile(clonePath).Root.Named("building_clone_vats").Single();
        var nativeLimitModifiers = nativeClone.AsBlock().Named("planet_limit").Single().AsBlock().Named("modifier");
        check(nativeLimitModifiers.Any(modifier => modifier.AsBlock().Named("owner").Any(owner =>
                owner.AsBlock().Named("has_tradition").Any(tradition => tradition.Operator == "=" && tradition.Scalar == "tr_cloning_finish" && tradition.Block is null))),
            "native clone vats planet limit retains owner has_tradition tr_cloning_finish");
        var cloneCatalog = content.Content.Catalogs["building_clone_vats"];
        var cloneDefinition = cloneCatalog.Definitions["building_clone_vats"];
        check(cloneDefinition.Origin.Source == clonePath && Flatten(cloneDefinition.Fields).Any(field =>
                field.Key == "has_tradition" && field.Scalar == "tr_cloning_finish" && field.Origin.Source == clonePath),
            "imported clone vats preserve tradition condition source provenance");

        var techModifier = nativeClone.AsBlock().Named("triggered_planet_modifier").Single(modifier =>
            Flatten(modifier.AsBlock()).Any(field => field.Key == "has_technology" && field.Scalar == "tech_gene_tailoring"));
        var cloneAssessment = ConstructionCatalogAssessor.Assess(cloneCatalog, "building_clone_vats", context, conditionCatalog);
        check(!context.Technologies!.Contains("tech_gene_tailoring") && cloneAssessment.Contributions.Any(contribution =>
                contribution.Kind == "triggered_planet_modifier" && contribution.Origin.Source == clonePath
                && contribution.Origin.Line == techModifier.Line && contribution.Condition.Truth == ConstructionTruth.False),
            "clone vats source technology effect remains false in the actual starting technology snapshot");
        var cloneQuote = content.Content.Quote("building_clone_vats", context, 0m, 0m, M3ConstructionContent.EconomyFacts(startup));
        check(cloneQuote.Quote is null, "clone vats remain unavailable from the actual catalogue assessment; no lifecycle readiness implied");

        const string researchSourcePath = "common/buildings/05_research_buildings.txt";
        const string researchBuildingId = "building_research_efficiency_1";
        check(facts.OwnerGalacticCommunityMember == false,
            "actual M3 owner snapshot preserves current known-false galactic community membership policy");
        var nativeResearch = new ContentSession(root).LoadFile(researchSourcePath).Root.Named(researchBuildingId).Single();
        var communityModifier = nativeResearch.AsBlock().Named("triggered_planet_modifier").Single(modifier =>
            modifier.AsBlock().Named("potential").Any(potential => potential.Block is { } potentialBlock
                && potentialBlock.Named("owner?").Any(owner => owner.Block is { } ownerBlock
                    && ownerBlock.Named("is_galactic_community_member").Any(member => member.Operator == "=" && member.Scalar == "yes" && member.Block is null))));
        var communityPotential = communityModifier.AsBlock().Named("potential").Single().AsBlock();
        check(communityPotential.Named("owner?").Count() == 1 && !communityPotential.Named("owner").Any()
            && communityPotential.Named("is_active_resolution").Any() && communityPotential.Named("has_modifier").Any(),
            "native research modifier keeps GC predicate in owner? scope alongside its separate resolution/modifier gates");
        var researchCatalog = content.Content.Catalogs[researchBuildingId];
        check(Flatten(researchCatalog.Definitions[researchBuildingId].Fields).Any(field =>
                field.Key == "is_galactic_community_member" && field.Scalar == "yes" && field.Origin.Source == researchSourcePath),
            "imported research GC predicate retains native source provenance");
        var researchAssessment = ConstructionCatalogAssessor.Assess(researchCatalog, researchBuildingId, context, conditionCatalog);
        check(researchAssessment.Contributions.Any(contribution => contribution.Kind == "triggered_planet_modifier"
                && contribution.Origin.Source == researchSourcePath && contribution.Origin.Line == communityModifier.Line
                && contribution.Condition.Truth == ConstructionTruth.False),
            "actual catalog assessment resolves the owner? GC modifier false while its other native gates remain");
    }

    private static IEnumerable<ConstructionField> Flatten(IEnumerable<ConstructionField> fields)
        => fields.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));

    private static IEnumerable<ScriptEntry> Flatten(ScriptBlock block)
    {
        foreach (var entry in block.Entries)
        {
            yield return entry;
            if (entry.Block is { } nested)
            {
                foreach (var child in Flatten(nested)) { yield return child; }
            }
        }
    }
}
