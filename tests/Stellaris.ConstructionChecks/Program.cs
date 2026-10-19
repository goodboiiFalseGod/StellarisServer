using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Construction;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--game-root"))
{
    Console.Error.WriteLine("Usage: Stellaris.ConstructionChecks [--game-root PATH]");
    return 2;
}
var checks = new List<(string Name, Func<Task> Run)>
{
    ("Accepted ordinary template configuration and explicit initial context", TemplateChecks.Ordinary),
    ("Explicit lobby template selection, privacy and per-colony context", AssessmentChecks.TemplateSelection),
    ("Isolated economy candidate preserves state and rollback", EconomyCandidateChecks.Isolation),
    ("Shelter economic consumers preserve output and additive Workforce channel ownership", ShelterEconomyChecks.Consumers),
    ("Source-owned installed replacement feeds monthly effects atomically", SourceOwnedEconomyChecks.Application),
    ("Source-owned category job effects retain exact source scaling and target channels", SourceCategoryEconomyChecks.Application),
    ("Shelter country and species roots retain source scope and mandatory gates", ShelterScopeChecks.Roots),
    ("Whole shelter closure evaluates original helpers and explicit runtime scope facts", ShelterClosureChecks.Facts),
    ("Reached shelter system scopes retain explicit target and star flag facts", ShelterClosureChecks.Systems),
    ("Reached shelter capital paths compare actual identities and preserve leader gates", ShelterClosureChecks.CapitalIdentities),
    ("Reached shelter growing target preserves the actual species link and trait facts", ShelterClosureChecks.GrowingSpecies),
    ("Reached shelter storm value consumes original arithmetic and active resolutions", ShelterClosureChecks.Values),
    ("Whole closure import preserves legacy and rejects reached conditional templates", Checks.ShelterClosureImport),
    ("Received exits preserve authoritative geometry and endpoint privacy", NavigationExitChecks.Projection),
    ("Trusted completion candidates isolate infrastructure and settlement phase", CompletionCandidateChecks.Settlement),
    ("Aggregate shared accounts, multi-colony settlement and exact retries", AggregateChecks.Transactions),
    ("Aggregate validates actual colony and country ownership", AggregateChecks.Bindings),
    ("Installed source capacity replacement preserves identity and atomic economy state", InstalledCapacityChecks.Snapshots),
    ("Installed zone levels and flat slot additions remain separately authoritative", InstalledCapacityChecks.Zones),
    ("Atomic construction debit, reservation, retry and completion", TransactionChecks.Orders),
    ("Refund and ownership policies fail closed atomically", TransactionChecks.Refunds),
    ("Actual-paid cancellation preserves caps, receipts and atomic ledger state", PaidCancellationChecks.Ledgers),
    ("Actual-paid aggregate cancellation derives world caps and reconciles payer", PaidCancellationChecks.Aggregates),
    ("Accepted daily work discards excess and completes before one month settlement", DailyConstructionChecks.Advance),
    ("Explicit ledger queues constrain daily work, reorder and pause without progress loss", QueueConstructionChecks.Lifecycle),
    ("Construction bounds, identity and batch rollback", TransactionChecks.Safety),
    ("Additive development protocol preserves unavailable values", TransactionChecks.Protocol),
    ("Scoped construction source assessment and independent job blocks", AssessmentChecks.Conditions),
    ("Source/context-bound nominal duration preserves exact ratio and legacy basis", AssessmentChecks.NominalDuration),
    ("Explicit owner country flags preserve string membership and unknown scopes", AssessmentChecks.OwnerFlags),
    ("Development projection privacy, readiness, refusal and retry", AssessmentChecks.Session),
    ("Bounded placement import, compatibility and explicit capacity", Checks.Placement),
    ("Conditional feature inventory, scalar inline and fail-closed sources", Checks.Features),
    ("Fixed district and zone catalogs preserve raw numerical branches", Checks.Catalogs),
    ("Source-bound catalog assessment preserves conditional contributions", Checks.CatalogAssessment),
    ("Bounded scripted predicates resolve exact imported source", Checks.ScriptedPredicates),
    ("Source-bound functional civic unlocks require explicit valid-civic facts", Checks.OwnerConditions),
    ("Fixed capital country-type helper preserves explicit source and legacy catalogs", Checks.CapitalCountryCondition),
    ("Exact capital habitat helper consumes only explicit district-set facts", Checks.CapitalHabitatCondition),
    ("Accepted Functional Architecture source operands preserve eligibility gates", Checks.FunctionalCivic),
    ("Accepted fixed building catalogs retain startup effects and flags", Checks.BuildingCatalogs),
    ("Source-bound zone scaling preserves exact signed district contributions", Checks.ZoneScaling),
    ("Source-bound economic categories preserve mult inheritance and self-only add", Checks.EconomicCategories),
    ("Raw branches, provenance, immutable data and hashes", Checks.Projection),
    ("Local variables and nested inline caller context", Checks.Variables),
    ("Unsupported conditions remain unresolved", Checks.Unsupported),
    ("Ambiguous definitions, variables and dependencies fail closed", Checks.Invalid),
    ("Version, UTF-8, safe paths and depth limits", Checks.Safety),
    ("Cancellation, changed sources and reusable importer", Checks.Atomic),
    ("Inline argument fanout is bounded before generated strings", Checks.Fanout),
    ("File count, input, expansion and variable/inline chain budgets", Checks.Budgets),
    ("Reparse dependency rejection", Checks.Reparse)
};
if (args.Length == 2)
{
    checks.Add(("Real 4.5.2 read-only hydroponics projection", () => Checks.Real(args[1])));
    checks.Add(("Real 4.5.2 startup effects preserve authority, ethic and civic branches", () => StartupBuildingChecks.Real(args[1])));
}
var failures = 0;
var skipped = 0;
foreach (var check in checks)
{
    try
    {
        await check.Run();
        Console.WriteLine("PASS: " + check.Name);
    }
    catch (SkipException exception)
    {
        skipped++;
        Console.WriteLine("SKIP: " + check.Name + ": " + exception.Message);
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine("FAIL: " + check.Name + ": " + exception);
    }
}
Console.WriteLine($"ConstructionChecks: {checks.Count - failures - skipped} PASS, {failures} FAIL, {skipped} SKIP");
return failures == 0 ? 0 : 1;

static class Checks
{
    public static Task CapitalHabitatCondition() => Fixture(async root =>
    {
        const string SOURCE = "common/scripted_triggers/00_scripted_triggers.txt";
        const string BASE = "is_special_colony_type = { always = no } has_functional_civic = { OR = { has_valid_civic = civic_functional_architecture has_valid_civic = civic_machine_builder } } has_tech_or_functional_civic = { OR = { has_technology = $TECH$ has_functional_civic = yes } } is_fallen_empire = { OR = { is_country_type = fallen_empire is_country_type = awakened_fallen_empire } } ";
        await Write(root, SOURCE, BASE + "uses_habitat_capitals = { uses_district_set = habitat }");
        await Write(root, "common/scripted_triggers/01_scripted_triggers_buildings.txt", "planet_is_habitat_equivalent = { always = no } planet_is_ecu_equivalent = { always = no } planet_is_ring_world_equivalent = { always = no }");
        var importer = new ConstructionDefinitionImporter();
        var catalog = await importer.ImportCapitalConditionCatalogAsync(root);
        Assert(catalog.ProfileId == "construction-conditions-capital-catalog-452-v1" && catalog.Definitions.Count == 8 && catalog.Definitions["uses_habitat_capitals"].Kind == "planetCondition", "Capital fixed inventory changed.");
        CapitalHabitatChecks.Facts(catalog);
        foreach (var legacy in new[] { await importer.ImportConditionCatalogAsync(root), await importer.ImportOwnerConditionCatalogAsync(root), await importer.ImportStartupConditionCatalogAsync(root) })
        {
            Assert(CapitalHabitatChecks.Evaluate(legacy, "habitat").Truth == ConstructionTruth.Unknown, "Legacy profile implicitly gained an unimported helper.");
        }
        foreach (var broken in new[]
        {
            new ConstructionCatalogProjection(catalog.ProfileId, catalog.Definitions.Values.Where(item => item.Id != "uses_habitat_capitals"), catalog.Parameters, catalog.Files, []),
            new ConstructionCatalogProjection(catalog.ProfileId, catalog.Definitions.Values.Select(item => item.Id == "uses_habitat_capitals" ? new ConstructionPlacementDefinition(item.Id, "ownerCondition", item.Origin, item.Fields) : item), catalog.Parameters, catalog.Files, []),
            new ConstructionCatalogProjection("construction-conditions-startup-catalog-452-v1", catalog.Definitions.Values, catalog.Parameters, catalog.Files, [])
        })
        {
            try { CapitalHabitatChecks.Evaluate(broken, "habitat"); throw new Exception("Malformed capital condition catalog accepted."); }
            catch (ArgumentException) { }
        }
        foreach (var (body, expected) in new[] { ("uses_district_set = changed_set", ConstructionTruth.True), ("unsupported_native = yes", ConstructionTruth.Unknown), ("uses_habitat_capitals = yes", ConstructionTruth.Unknown) })
        {
            await Write(root, SOURCE, BASE + "uses_habitat_capitals = { " + body + " }");
            Assert(CapitalHabitatChecks.Evaluate(await importer.ImportCapitalConditionCatalogAsync(root), "changed_set").Truth == expected, "Changed/unsupported/cyclic habitat helper was hardcoded or unbounded.");
        }
    });

    public static Task CapitalCountryCondition() => Fixture(async root =>
    {
        const string SOURCE = "common/scripted_triggers/00_scripted_triggers.txt";
        const string HELPER = "is_fallen_empire = { OR = { is_country_type = fallen_empire is_country_type = awakened_fallen_empire } }";
        const string BASE = "is_special_colony_type = { always = no } has_functional_civic = { OR = { has_valid_civic = civic_functional_architecture has_valid_civic = civic_machine_builder } } has_tech_or_functional_civic = { OR = { has_technology = $TECH$ has_functional_civic = yes } } ";
        await Write(root, SOURCE, BASE + HELPER);
        await Write(root, "common/scripted_triggers/01_scripted_triggers_buildings.txt", "planet_is_habitat_equivalent = { always = no } planet_is_ecu_equivalent = { always = no } planet_is_ring_world_equivalent = { always = no }");
        var importer = new ConstructionDefinitionImporter();
        var catalog = await importer.ImportStartupConditionCatalogAsync(root);
        Assert(catalog.ProfileId == "construction-conditions-startup-catalog-452-v1" && catalog.Definitions.Count == 7 && catalog.Definitions["is_fallen_empire"].Kind == "ownerCondition", "Fixed startup predicate inventory changed.");
        CapitalConditionChecks.Membership(catalog);
        foreach (var legacy in new[] { await importer.ImportConditionCatalogAsync(root), await importer.ImportOwnerConditionCatalogAsync(root) })
        {
            Assert(CapitalConditionChecks.Evaluate(legacy, "default").Truth == ConstructionTruth.Unknown, "Legacy catalog implicitly gained an unimported helper.");
        }
        foreach (var broken in new[]
        {
            new ConstructionCatalogProjection(catalog.ProfileId, catalog.Definitions.Values.Where(item => item.Id != "is_fallen_empire"), catalog.Parameters, catalog.Files, []),
            new ConstructionCatalogProjection(catalog.ProfileId, catalog.Definitions.Values.Select(item => item.Id == "is_fallen_empire" ? new ConstructionPlacementDefinition(item.Id, "planetCondition", item.Origin, item.Fields) : item), catalog.Parameters, catalog.Files, []),
            new ConstructionCatalogProjection("unknown-profile", catalog.Definitions.Values, catalog.Parameters, catalog.Files, [])
        })
        {
            try { CapitalConditionChecks.Evaluate(broken, "default"); throw new Exception("Malformed startup condition catalog accepted."); }
            catch (ArgumentException) { }
        }
        foreach (var (replacement, truth) in new[] { ("is_fallen_empire = { is_country_type = changed_type }", ConstructionTruth.True), ("is_fallen_empire = { unsupported_native = yes }", ConstructionTruth.Unknown), ("is_fallen_empire = { is_fallen_empire = yes }", ConstructionTruth.Unknown) })
        {
            await Write(root, SOURCE, BASE + replacement);
            Assert(CapitalConditionChecks.Evaluate(await importer.ImportStartupConditionCatalogAsync(root), "changed_type").Truth == truth, "Changed/cyclic helper was hardcoded or expanded without bounds.");
        }
    });

    public static Task FunctionalCivic() => Fixture(async root =>
    {
        const string PATH = "common/governments/civics/00_civics.txt";
        const string BASE = "civic_functional_architecture = { potential = { ethics = { NOT = { value = ethic_gestalt_consciousness } } authority = { NOT = { value = auth_corporate } } } modifier = { planet_structures_cost_mult = -.15 planet_building_build_speed_mult = .15 add_attunement_the_composer_of_strands = @attunement } }";
        await Write(root, "common/scripted_variables/09_scripted_variables_shroud.txt", "@attunement = 60");
        await Write(root, "common/governments/99_README_GOVERNMENT.txt", "# Government requirement-list grammar evidence");
        await Write(root, PATH, BASE);
        var importer = new ConstructionDefinitionImporter();
        var source = await importer.ImportFunctionalCivicCatalogAsync(root);
        var validContext = new ConstructionContext(sourceFacts: new ConstructionSourceFacts(validCivics: ["civic_functional_architecture"]));
        foreach (var template in new[] { "economic", "scientific", "military" })
        {
            var result = ConstructionFunctionalCivicCollection.Collect(source, template, validContext);
            Assert(ReferenceEquals(result.Source, source) && ReferenceEquals(result.Template, ConstructionOrdinaryTemplate.Get(template)) && result.Potential.Truth == ConstructionTruth.True && result.Possible.Truth == ConstructionTruth.Unknown && result.ValidatedApplicability.Truth == ConstructionTruth.True, "Accepted template/source potential/possible/applicability conflated.");
            Assert(result.ConstructionOperands.Select(item => item.Value).SequenceEqual(new[] { -.15m, .15m }) && result.ConstructionOperands.All(item => item.Origin.Source == PATH) && result.ExcludedModifiers.Single().Key == "add_attunement_the_composer_of_strands", "Construction civic operands/provenance or excluded effect lost.");
            Assert(result.ConstructionOperands is IList operands && operands.IsReadOnly && result.ExcludedModifiers is IList excluded && excluded.IsReadOnly, "Civic collection is mutable.");
        }
        Assert(ConstructionFunctionalCivicCollection.Collect(source, "economic", new()).ValidatedApplicability.Truth == ConstructionTruth.Unknown && ConstructionFunctionalCivicCollection.Collect(source, "economic", new(sourceFacts: new ConstructionSourceFacts(validCivics: []))).ValidatedApplicability.Truth == ConstructionTruth.False, "Template membership/potential inferred native validity.");
        async Task<ConstructionFunctionalCivicCollection> Collect(string text)
        {
            await Write(root, PATH, text);
            return ConstructionFunctionalCivicCollection.Collect(await importer.ImportFunctionalCivicCatalogAsync(root), "economic", validContext);
        }
        var denied = await Collect(BASE.Replace("NOT = { value = auth_corporate }", "value = auth_corporate"));
        Assert(denied.Potential.Truth == ConstructionTruth.False && denied.ValidatedApplicability.Truth == ConstructionTruth.True, "Declared potential overwritten by explicit validity fact.");
        var unknown = await Collect(BASE.Replace("ethics =", "unsupported_selector ="));
        Assert(unknown.Potential.Truth == ConstructionTruth.Unknown, "Unsupported government selector inferred.");
        var possible = await Collect(BASE.Insert(BASE.LastIndexOf('}'), "possible = { OR = { ethics = { value = ethic_materialist } authority = { value = auth_corporate } } } "));
        Assert(possible.Possible.Truth == ConstructionTruth.True, "Government list OR did not use bound template configuration.");
        var extra = await Collect(BASE.Insert(BASE.LastIndexOf('}'), "playable = { always = no } "));
        Assert(extra.Gates.Any(gate => gate.DependencyId == "civic-additional-declaration"), "Additional source eligibility condition silently ignored.");
        foreach (var text in new[]
        {
            BASE.Replace("planet_structures_cost_mult = -.15", "planet_structures_cost_mult = -.15 planet_structures_cost_mult = -.15"),
            BASE.Replace("planet_structures_cost_mult = -.15", "planet_structures_cost_mult = modifier:unknown"),
            BASE.Replace("planet_structures_cost_mult = -.15", "planet_structures_cost_mult = {}"),
            BASE.Replace("planet_structures_cost_mult = -.15", "planet_structures_cost_mult += -.15"),
            BASE.Replace("planet_structures_cost_mult = -.15", "unknown_effect = 1"),
            BASE.Insert(BASE.LastIndexOf('}'), "modifier = {} ")
        })
        {
            var rejected = await Collect(text);
            Assert(rejected.ConstructionOperands.Count == 0 && rejected.Gates.First().DependencyId == "civic-modifier-shape", "Malformed civic modifier partially collected.");
        }
        try { ConstructionFunctionalCivicCollection.Collect(source, "private/path", validContext); throw new Exception("Unknown template accepted."); }
        catch (ArgumentException exception) { Assert(!exception.Message.Contains("private/path", StringComparison.Ordinal), "Untrusted template echoed."); }
        try { ConstructionFunctionalCivicCollection.Collect(new("wrong-profile", source.Definitions.Values, source.Parameters, source.Files, []), "economic", validContext); throw new Exception("Wrong civic profile accepted."); }
        catch (ArgumentException) { }
    });

    public static Task OwnerConditions() => Fixture(async root =>
    {
        const string SOURCE = "common/scripted_triggers/00_scripted_triggers.txt";
        const string TEXT = "is_special_colony_type = { always = no } has_functional_civic = { OR = { has_valid_civic = civic_functional_architecture has_valid_civic = civic_machine_builder } } has_tech_or_functional_civic = { OR = { has_technology = $TECH$ has_functional_civic = yes } }";
        await Write(root, SOURCE, TEXT);
        await Write(root, "common/scripted_triggers/01_scripted_triggers_buildings.txt", "planet_is_habitat_equivalent = { always = no } planet_is_ecu_equivalent = { always = no } planet_is_ring_world_equivalent = { always = no }");
        var importer = new ConstructionDefinitionImporter();
        var conditions = await importer.ImportOwnerConditionCatalogAsync(root);
        Assert(conditions.ProfileId == "construction-conditions-owner-catalog-452-v1" && conditions.Definitions.Count == 6 && conditions.Definitions["has_functional_civic"].Kind == "ownerCondition" && conditions.Definitions["has_tech_or_functional_civic"].Fields.Single().Children.Any(field => field.Scalar == "$TECH$"), "Extended predicate inventory or exact template token lost.");
        const string CITY = "common/districts/00_urban_districts.txt";
        async Task<ConstructionCatalogAssessment> Assess(string predicate, ConstructionContext context, ConstructionCatalogProjection? source = null)
        {
            await Write(root, CITY, "district_city = { potential = { " + predicate + " } }");
            return ConstructionCatalogAssessor.Assess(await importer.ImportCityCatalogAsync(root), "district_city", context, source ?? conditions);
        }
        ConstructionContext Context(IEnumerable<string>? tech, IEnumerable<string>? valid, IEnumerable<string>? membership = null) => new(ownerExists: true, technologies: tech, sourceFacts: new ConstructionSourceFacts(civics: membership, validCivics: valid));
        const string CALL = "owner = { has_tech_or_functional_civic = { TECH = tech_power_hub_1 } }";
        foreach (var civic in new[] { "civic_functional_architecture", "civic_machine_builder" })
        {
            Assert((await Assess(CALL, Context([], [civic]))).Potential.Truth == ConstructionTruth.True, "Explicit valid functional civic failed unlock.");
        }
        Assert((await Assess(CALL, Context(["tech_power_hub_1"], []))).Potential.Truth == ConstructionTruth.True, "Explicit researched technology failed unlock.");
        Assert((await Assess(CALL, Context(["tech_power_hub_1"], null))).Potential.Truth == ConstructionTruth.True && (await Assess(CALL, Context(null, ["civic_functional_architecture"]))).Potential.Truth == ConstructionTruth.True, "Decisive OR lost due to unrelated unknown input.");
        Assert((await Assess(CALL, Context([], []))).Potential.Truth == ConstructionTruth.False && (await Assess(CALL, Context([], [], ["civic_functional_architecture"]))).Potential.Truth == ConstructionTruth.False, "Absent or invalid civic membership treated as valid.");
        Assert((await Assess(CALL, Context([], null, ["civic_functional_architecture"]))).Potential.Truth == ConstructionTruth.Unknown && (await Assess(CALL, new())).Potential.Truth == ConstructionTruth.Unknown, "Missing civic validation or owner facts inferred.");
        Assert((await Assess("owner = { has_functional_civic = no }", Context([], []))).Potential.Truth == ConstructionTruth.True && (await Assess("owner = { has_functional_civic = no }", Context([], ["civic_functional_architecture"]))).Potential.Truth == ConstructionTruth.False, "Owner helper inversion failed.");
        foreach (var invalid in new[] { "", "TECH = tech_power_hub_1 TECH = tech_power_hub_1", "TECH != tech_power_hub_1", "TECH = {}", "OTHER = tech_power_hub_1", "TECH = \"private/path\"", "TECH = " + new string('x', 129) })
        {
            Assert((await Assess("owner = { has_tech_or_functional_civic = { " + invalid + " } }", Context([], ["civic_functional_architecture"]))).Potential.Truth == ConstructionTruth.Unknown, "Malformed owner argument inferred.");
        }
        Assert((await Assess("has_tech_or_functional_civic = { TECH = tech_power_hub_1 }", Context([], ["civic_functional_architecture"]))).Potential.Truth == ConstructionTruth.Unknown, "Owner predicate resolved in planet scope.");
        var legacy = await importer.ImportConditionCatalogAsync(root);
        Assert((await Assess(CALL, Context([], ["civic_functional_architecture"]), legacy)).Potential.Truth == ConstructionTruth.Unknown, "Legacy catalog inferred unimported owner predicate.");
        var invalidProfile = new ConstructionCatalogProjection(conditions.ProfileId, conditions.Definitions.Values.Where(item => item.Id != "has_functional_civic"), conditions.Parameters, conditions.Files, []);
        try { await Assess(CALL, Context([], []), invalidProfile); throw new Exception("Incomplete extended condition catalog accepted."); }
        catch (ArgumentException) { }
        var functional = conditions.Definitions["has_functional_civic"];
        var templateTechnology = conditions.Definitions["has_tech_or_functional_civic"].Fields.Single().Children.Single(field => field.Key == "has_technology");
        var unbound = new ConstructionCatalogProjection(conditions.ProfileId, conditions.Definitions.Values.Select(item => item.Id == functional.Id ? new ConstructionPlacementDefinition(item.Id, item.Kind, item.Origin, [templateTechnology]) : item), conditions.Parameters, conditions.Files, []);
        var unboundAnswer = await Assess("owner = { has_functional_civic = yes }", Context([], []), unbound);
        Assert(unboundAnswer.Potential.Truth == ConstructionTruth.Unknown && unboundAnswer.Potential.Gates.Any(gate => gate.DependencyId == "owner-scripted-argument-unbound"), "Free template argument treated as a technology ID or default.");
        var wrongKind = new ConstructionCatalogProjection(conditions.ProfileId, conditions.Definitions.Values.Select(item => item.Id == functional.Id ? new ConstructionPlacementDefinition(item.Id, "planetCondition", item.Origin, item.Fields) : item), conditions.Parameters, conditions.Files, []);
        try { await Assess(CALL, Context([], []), wrongKind); throw new Exception("Wrong owner predicate kind accepted."); }
        catch (ArgumentException) { }
        await Write(root, SOURCE, TEXT.Replace("has_functional_civic = { OR = { has_valid_civic = civic_functional_architecture has_valid_civic = civic_machine_builder } }", "has_functional_civic = { has_functional_civic = yes }"));
        var cyclic = await Assess(CALL, Context([], []), await importer.ImportOwnerConditionCatalogAsync(root));
        Assert(cyclic.Potential.Truth == ConstructionTruth.Unknown && cyclic.Potential.Gates.Any(gate => gate.DependencyId == "condition-budget"), "Owner scripted recursion unbounded or inferred.");
        await Write(root, SOURCE, TEXT.Replace("$TECH$", "$OTHER$"));
        await Fail(root, async () => { await importer.ImportOwnerConditionCatalogAsync(root); });
        await Write(root, SOURCE, TEXT);
        var validList = new List<string> { "civic_functional_architecture" };
        var frozen = new ConstructionSourceFacts(validCivics: validList);
        validList.Clear();
        Assert(frozen.ValidCivics!.Contains("civic_functional_architecture"), "Validated civic facts are mutable through caller list.");
    });

    public static Task EconomicCategories() => Fixture(async root =>
    {
        const string PATH = "common/economic_categories/00_common_categories.txt";
        const string BASE = "country = { generate_mult_modifiers = { produces cost } } planets = { parent = country } planet_structures = { parent = planets generate_mult_modifiers = { cost upkeep } modifier_category = colony } planet_buildings = { parent = planet_structures generate_mult_modifiers = { produces cost upkeep } } planet_buildings_strongholds = { parent = planet_buildings } planet_districts = { parent = planet_structures generate_mult_modifiers = { cost upkeep } } planet_districts_cities = { parent = planet_districts generate_mult_modifiers = { cost } generate_add_modifiers = { produces } } planet_districts_generator = { parent = planet_districts } planet_districts_mining = { parent = planet_districts } planet_districts_farming = { parent = planet_districts } planet_zones = { parent = planet_structures generate_mult_modifiers = {} }";
        await Write(root, "common/economic_categories/99_README_ECONOMIC_CATEGORIES.txt", "# Source semantics evidence");
        await Write(root, PATH, BASE + " unrelated = { parent = missing } ");
        var importer = new ConstructionDefinitionImporter();
        var catalog = await importer.ImportEconomicCategoriesAsync(root);
        AssertEconomicCategories(catalog);
        await ResourceCategoryBinding(root, catalog);
        Assert(catalog.Categories is IDictionary dictionary && dictionary.IsReadOnly && catalog.Categories["country"].Mult is IList operations && operations.IsReadOnly, "Economic category projection is mutable.");
        var selection = catalog.Select("planet_districts_cities", ConstructionEconomicOperation.Cost, ConstructionEconomicModifierFamily.Mult);
        Assert(ReferenceEquals(selection.Catalog, catalog) && selection.TargetCategory == "planet_districts_cities" && selection.Declarations is IList declarations && declarations.IsReadOnly && selection.Declarations.All(item => item.Source.Origin.Source == PATH), "Economic category source/selection binding lost.");
        await Write(root, PATH, BASE.Replace("planet_districts_generator = { parent = planet_districts }", "planet_districts_generator = { parent = planet_districts_cities }"));
        var childOfCity = await importer.ImportEconomicCategoriesAsync(root);
        Assert(childOfCity.Select("planet_districts_generator", ConstructionEconomicOperation.Produces, ConstructionEconomicModifierFamily.Add).Declarations.Count == 0 && childOfCity.Select("planet_districts_generator", ConstructionEconomicOperation.Cost, ConstructionEconomicModifierFamily.Mult).Declarations.First().Source.Id == "planet_districts_cities", "Add inherited or mult parent declaration disappeared.");
        string Chain(int count) => BASE.Replace("parent = country", "parent = chain_0") + string.Concat(Enumerable.Range(0, count).Select(index => " chain_" + index + " = { parent = " + (index == count - 1 ? "country" : "chain_" + (index + 1)) + " }"));
        await Write(root, PATH, Chain(21));
        Assert((await importer.ImportEconomicCategoriesAsync(root)).Categories.Count == 32, "Valid maximum category closure rejected.");
        foreach (var bad in new[]
        {
            BASE.Replace("parent = country", "parent = missing"),
            BASE.Replace("parent = country", "parent = planet_structures"),
            BASE.Replace("parent = country", ""),
            BASE.Replace("parent = country", "parent = country parent = country"),
            BASE.Replace("parent = country", "parent = {}"),
            BASE.Replace("produces cost", "produces cost cost"),
            BASE.Replace("produces cost", "unknown"),
            BASE.Replace("produces cost", "cost = yes"),
            BASE.Replace("produces cost", "cost {}"),
            BASE.Replace("generate_add_modifiers = { produces }", "generate_add_modifiers = { produces } generate_add_modifiers = {}"),
            BASE.Replace("modifier_category = colony", "modifier_category = colony modifier_category = colony"),
            BASE.Replace("modifier_category = colony", "modifier_category = {}"),
            BASE + " country = {}",
            BASE.Replace("generate_mult_modifiers = {}", "triggered_cost_modifier = {}"),
            BASE.Replace("generate_mult_modifiers = {}", "generate_mult_modifiers = yes"),
            Chain(22)
        })
        {
            await Write(root, PATH, bad);
            try { await importer.ImportEconomicCategoriesAsync(root); throw new Exception("Invalid category source published."); }
            catch (Exception exception) when (exception is InvalidDataException or ConstructionImportException) { }
        }
        foreach (var action in new Action[]
        {
            () => catalog.Select("private/path\nsecret", ConstructionEconomicOperation.Cost, ConstructionEconomicModifierFamily.Mult),
            () => catalog.Select("country", (ConstructionEconomicOperation)999, ConstructionEconomicModifierFamily.Mult),
            () => catalog.Select("country", ConstructionEconomicOperation.Cost, (ConstructionEconomicModifierFamily)999)
        })
        {
            try { action(); throw new Exception("Unknown category/enum accepted."); }
            catch (ArgumentException exception) { Assert(!exception.Message.Contains("secret", StringComparison.Ordinal), "Untrusted selection leaked."); }
        }
        await Write(root, PATH, BASE);
        var changed = new ConstructionDefinitionImporter(async cancellation => await Write(root, PATH, BASE + "# changed"));
        try { await changed.ImportEconomicCategoriesAsync(root); throw new Exception("Changed category source published."); }
        catch (Exception exception) when (exception is InvalidDataException or ConstructionImportException) { }
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        try { await importer.ImportEconomicCategoriesAsync(root, canceled.Token); throw new Exception("Canceled category source published."); }
        catch (OperationCanceledException) { }
    });

    private static void AssertEconomicCategories(ConstructionEconomicCategoryProjection catalog)
    {
        Assert(catalog.Categories.Count == 11 && catalog.Source.ProfileId == "construction-economic-categories-452-v1", "Reached economic category coverage changed.");
        string[] Selected(string id, ConstructionEconomicOperation operation, ConstructionEconomicModifierFamily family) => catalog.Select(id, operation, family).Declarations.Select(item => item.Source.Id).ToArray();
        Assert(Selected("planet_districts_cities", ConstructionEconomicOperation.Produces, ConstructionEconomicModifierFamily.Add).SequenceEqual(new[] { "planet_districts_cities" }), "City additive output lost.");
        Assert(Selected("planet_zones", ConstructionEconomicOperation.Produces, ConstructionEconomicModifierFamily.Add).Length == 0, "Additive category inheritance invented.");
        Assert(Selected("planet_districts_generator", ConstructionEconomicOperation.Cost, ConstructionEconomicModifierFamily.Mult).SequenceEqual(new[] { "planet_districts", "planet_structures", "country" }), "Generator cost parent closure changed.");
        Assert(Selected("planet_zones", ConstructionEconomicOperation.Upkeep, ConstructionEconomicModifierFamily.Mult).SequenceEqual(new[] { "planet_structures" }), "Absent/empty generation inferred.");
        Assert(Selected("planet_buildings_strongholds", ConstructionEconomicOperation.Produces, ConstructionEconomicModifierFamily.Mult).SequenceEqual(new[] { "planet_buildings", "country" }), "Stronghold building inheritance changed.");
        Assert(catalog.Categories["planet_structures"].ModifierCategory == "colony" && catalog.Categories["country"].ModifierCategory is null && catalog.Categories["planet_districts_cities"].ModifierCategory is null, "Modifier target inferred from parent.");
    }

    private static async Task ResourceCategoryBinding(string root, ConstructionEconomicCategoryProjection categories)
    {
        var importer = new ConstructionDefinitionImporter();
        var source = await importer.ImportBuildingCatalogAsync(root, "building_hydroponics_farm");
        var context = new ConstructionContext(sourceFacts: new ConstructionSourceFacts(validCivics: ["civic_functional_architecture"]));
        var assessment = ConstructionCatalogAssessor.Assess(source, "building_hydroponics_farm", context);
        var binding = ConstructionResourceCategoryBinding.Bind(assessment, categories);
        Assert(binding.BindingGates.Count == 0 && binding.Branches.Count == 3 && ReferenceEquals(binding.Assessment, assessment) && ReferenceEquals(binding.CategoryCatalog, categories), "Resource category assessment/source binding lost.");
        Assert(binding.Branches is IList list && list.IsReadOnly && binding.Branches.Select(item => item.Source).SequenceEqual(assessment.Contributions.Where(item => item.Kind is "cost" or "upkeep")), "Resource branch order/condition/raw amount identity changed.");
        Assert(binding.Branches.All(item => item.ResourceTablePath.EndsWith(":resources", StringComparison.Ordinal) && item.CategorySource.Scalar == "planet_buildings" && ReferenceEquals(item.Mult.Catalog, categories) && item.Add.Declarations.Count == 0), "Resource table/category source or self-only add binding changed.");
        Assert(binding.Branches.Where(item => item.Source.Kind == "cost").All(item => item.Mult.Declarations.Select(category => category.Source.Id).SequenceEqual(new[] { "planet_buildings", "planet_structures", "country" })) && binding.Branches.Where(item => item.Source.Kind == "upkeep").All(item => item.Mult.Declarations.Select(category => category.Source.Id).SequenceEqual(new[] { "planet_buildings", "planet_structures" })), "Infrastructure cost/upkeep categories conflated.");
        const string CIVIC_PATH = "common/governments/civics/00_civics.txt";
        const string CIVIC = "civic_functional_architecture = { potential = {} modifier = { planet_structures_cost_mult = -.15 planet_building_build_speed_mult = .15 } }";
        await Write(root, "common/scripted_variables/09_scripted_variables_shroud.txt", "@attunement = 60");
        await Write(root, "common/governments/99_README_GOVERNMENT.txt", "# Government requirement-list grammar evidence");
        await Write(root, CIVIC_PATH, CIVIC);
        var civicSource = await importer.ImportFunctionalCivicCatalogAsync(root);
        var civic = ConstructionFunctionalCivicCollection.Collect(civicSource, "economic", context);
        var basis = ConstructionNumericalBasisAssociation.Bind(binding, civic, "economic");
        AssertBasisAssociation(basis);
        Assert(basis.CostCandidates.Count == 2 && basis.CostCandidates.Select(item => item.Branch.Source).SequenceEqual(assessment.Contributions.Where(item => item.Kind == "cost")) && ReferenceEquals(basis.Resources, binding) && ReferenceEquals(basis.Civic, civic) && ReferenceEquals(basis.Template, civic.Template), "Association reordered branches or lost snapshot identities.");
        Assert(ReferenceEquals(assessment.Context, context) && ReferenceEquals(civic.Context, context) && basis.CostCandidates is IList candidates && candidates.IsReadOnly && basis.UnassociatedOperands is IList unassociated && unassociated.IsReadOnly && basis.AssociationGates is IList gates && gates.IsReadOnly, "Association context identity or immutability lost.");
        void Rejected(ConstructionResourceCategoryBinding resources, ConstructionFunctionalCivicCollection collection, string template, string dependency)
        {
            var rejected = ConstructionNumericalBasisAssociation.Bind(resources, collection, template);
            Assert(rejected.CostCandidates.Count == 0 && rejected.UnassociatedOperands.Count == 0 && rejected.AssociationGates.Single().DependencyId == dependency, "Invalid basis partially associated or wrong gate.");
        }
        Rejected(binding, civic, "scientific", "template-snapshot-mismatch");
        var missingDeclarationSource = new ConstructionCatalogProjection(categories.Source.ProfileId, categories.Source.Definitions.Values.Select(definition => definition.Id != "planet_structures" ? definition : new ConstructionPlacementDefinition(definition.Id, definition.Kind, definition.Origin, definition.Fields.Where(field => field.Key != "generate_mult_modifiers"))), categories.Source.Parameters, categories.Source.Files, categories.Source.Coverage);
        var missingDeclaration = ConstructionResourceCategoryBinding.Bind(assessment, new ConstructionEconomicCategoryProjection(missingDeclarationSource));
        Rejected(missingDeclaration, civic, "economic", "civic-cost-declaration-unresolved");
        Rejected(binding, ConstructionFunctionalCivicCollection.Collect(civicSource, "economic", new()), "economic", "context-snapshot-mismatch");
        try { ConstructionNumericalBasisAssociation.Bind(binding, civic, "private/path"); throw new Exception("Unknown template associated."); }
        catch (ArgumentException exception) { Assert(!exception.Message.Contains("private/path", StringComparison.Ordinal), "Untrusted association template echoed."); }
        foreach (var path in new[] { "launcher-settings.json", _globals[0] })
        {
            foreach (var lengthMismatch in new[] { false, true })
            {
                var files = civicSource.Files.Select(file => file.RelativePath != path ? file : lengthMismatch ? file with { ByteLength = file.ByteLength + 1 } : file with { Sha256 = new string('0', 64) });
                var altered = new ConstructionCatalogProjection(civicSource.ProfileId, civicSource.Definitions.Values, civicSource.Parameters, files, civicSource.Coverage);
                Rejected(binding, ConstructionFunctionalCivicCollection.Collect(altered, "economic", context), "economic", "source-profile-mismatch");
            }
        }
        foreach (var validity in new[] { ConstructionTruth.False, ConstructionTruth.Unknown })
        {
            var otherContext = validity == ConstructionTruth.False ? new ConstructionContext(sourceFacts: new ConstructionSourceFacts(validCivics: [])) : new ConstructionContext();
            var otherBinding = ConstructionResourceCategoryBinding.Bind(ConstructionCatalogAssessor.Assess(source, "building_hydroponics_farm", otherContext), categories);
            var otherCivic = ConstructionFunctionalCivicCollection.Collect(civicSource, "economic", otherContext);
            var annotated = ConstructionNumericalBasisAssociation.Bind(otherBinding, otherCivic, "economic");
            AssertBasisAssociation(annotated);
            Assert(annotated.Civic.ValidatedApplicability.Truth == validity && annotated.Civic.Possible.Truth == ConstructionTruth.Unknown, "Association inferred civic validity or possible condition.");
        }
        await Write(root, CIVIC_PATH, CIVIC.Replace("planet_structures_cost_mult = -.15", "planet_structures_cost_mult = modifier:unknown"));
        Rejected(binding, ConstructionFunctionalCivicCollection.Collect(await importer.ImportFunctionalCivicCatalogAsync(root), "economic", context), "economic", "civic-operands-unavailable");
        await Write(root, CIVIC_PATH, CIVIC);
        var original = source.Definitions["building_hydroponics_farm"];
        var nonConstruction = new ConstructionCatalogProjection(source.ProfileId, new[] { new ConstructionPlacementDefinition(original.Id, "zoneSlot", original.Origin, original.Fields) }, source.Parameters, source.Files, source.Coverage);
        var invalidKind = ConstructionResourceCategoryBinding.Bind(ConstructionCatalogAssessor.Assess(nonConstruction, original.Id, new()), categories);
        Assert(invalidKind.Branches.Count == 0 && invalidKind.BindingGates.Single().DependencyId == "resource-definition-kind", "Nonconstruction resource definition bound.");
        Rejected(invalidKind, ConstructionFunctionalCivicCollection.Collect(civicSource, "economic", invalidKind.Assessment.Context), "economic", "resource-binding-unavailable");
        foreach (var path in new[] { "launcher-settings.json", _globals[0] })
        {
            foreach (var mismatch in new[] { false, true })
            {
                var files = categories.Source.Files.Select(file => file.RelativePath != path ? file : mismatch ? file with { ByteLength = file.ByteLength + 1 } : file with { Sha256 = new string('0', 64) });
                var altered = new ConstructionEconomicCategoryProjection(new(categories.Source.ProfileId, categories.Source.Definitions.Values, [], files, []));
                var rejected = ConstructionResourceCategoryBinding.Bind(assessment, altered);
                Assert(rejected.Branches.Count == 0 && rejected.BindingGates.Single().DependencyId == "source-profile-mismatch" && !JsonSerializer.Serialize(rejected.BindingGates).Contains(path, StringComparison.Ordinal), "Source mismatch partially bound or leaked source path.");
            }
        }
        await Write(root, "common/scripted_triggers/01_scripted_triggers_buildings.txt", "planet_is_habitat_equivalent = {} planet_is_ecu_equivalent = {} planet_is_ring_world_equivalent = {}");
        await Write(root, "common/scripted_triggers/00_scripted_triggers.txt", "is_special_colony_type = {}");
        var conditions = await importer.ImportConditionCatalogAsync(root);
        var mismatchedConditions = new ConstructionCatalogProjection(conditions.ProfileId, conditions.Definitions.Values, conditions.Parameters, conditions.Files.Select(file => file.RelativePath == _globals[0] ? file with { Sha256 = new string('0', 64) } : file), conditions.Coverage);
        var withConditions = ConstructionCatalogAssessor.Assess(source, "building_hydroponics_farm", context, conditions);
        Assert(ReferenceEquals(ConstructionResourceCategoryBinding.Bind(withConditions, categories).Assessment.ConditionSource, conditions), "Condition source binding lost.");
        AssertBasisAssociation(ConstructionNumericalBasisAssociation.Bind(ConstructionResourceCategoryBinding.Bind(withConditions, categories), civic, "economic"));
        var mismatchedAssociation = ConstructionResourceCategoryBinding.Bind(ConstructionCatalogAssessor.Assess(source, "building_hydroponics_farm", context, mismatchedConditions), categories);
        Rejected(mismatchedAssociation, civic, "economic", "source-profile-mismatch");
        var mismatched = ConstructionResourceCategoryBinding.Bind(ConstructionCatalogAssessor.Assess(source, "building_hydroponics_farm", new(), mismatchedConditions), categories);
        Assert(mismatched.Branches.Count == 0 && mismatched.BindingGates.Single().DependencyId == "source-profile-mismatch", "Condition/numerical source mismatch accepted.");
        foreach (var body in new[]
        {
            Definition().Replace("category = planet_buildings", "category = unknown"),
            Definition().Replace("category = planet_buildings", "category = planet_buildings category = planet_buildings"),
            Definition().Replace("category = planet_buildings", "category = {}"),
            Definition().Replace("category = planet_buildings", ""),
            Definition().Replace("resources =", "not_resources ="),
            Definition().Insert(Definition().LastIndexOf('}'), "resources = { category = planet_buildings } ")
        })
        {
            await Write(root, BUILDINGS, body);
            var invalidSource = await importer.ImportBuildingCatalogAsync(root, "building_hydroponics_farm");
            var rejected = ConstructionResourceCategoryBinding.Bind(ConstructionCatalogAssessor.Assess(invalidSource, "building_hydroponics_farm", new()), categories);
            Assert(rejected.Branches.Count == 0 && rejected.BindingGates.Count == 1, "Malformed/unknown resource category partially bound.");
        }
        await Write(root, BUILDINGS, Definition());
    }

    private static void AssertBasisAssociation(ConstructionNumericalBasisAssociation basis)
    {
        Assert(basis.CostCandidates.Count != 0 && basis.CostCandidates.All(item => item.Branch.Source.Kind == "cost" && item.Declaration.Source.Id == "planet_structures" && item.Declaration.Mult.Contains(ConstructionEconomicOperation.Cost) && item.CivicOperand.Value == -.15m && ReferenceEquals(item.CivicOperand, basis.Civic.ConstructionOperands.Single(operand => operand.Resource == "planet_structures_cost_mult")) && item.Branch.Mult.Declarations.Any(declaration => ReferenceEquals(declaration, item.Declaration))), "Cost association lost exact raw operand/declaration identity or included upkeep.");
        Assert(basis.UnassociatedOperands.Single().Resource == "planet_building_build_speed_mult" && basis.UnassociatedOperands.Single().Value == .15m && basis.AssociationGates.Single().DependencyId == "building-speed-target-and-time-rule", "Building speed was applied or its evidence gate lost.");
    }

    public static Task ZoneScaling() => Fixture(async root =>
    {
        const string zones = "common/zones/00_zones.txt";
        const string slots = "common/zone_slots/00_zone_slots.txt";
        var body = "planet_modifier = { planet_housing_add = 10 } district_planet_modifier = { job_technician_add = 100 } triggered_district_planet_modifier = { potential = { always = yes } planet_housing_add = -1.25 } triggered_district_planet_modifier = { potential = { always = no unknown = yes } planet_housing_add = 1 mult = modifier:unknown }";
        await WriteZones(body);
        await Write(root, slots, string.Concat(new[] { "slot_city_government", "slot_city_01", "slot_city_02", "slot_energy", "slot_minerals", "slot_food" }.Select(id => id + " = {} ")));
        var importer = new ConstructionDefinitionImporter();
        async Task<ConstructionCatalogAssessment> Assess() => ConstructionCatalogAssessor.Assess(await importer.ImportZoneCatalogAsync(root), "zone_urban", new());
        var assessment = await Assess();
        var result = ConstructionZoneContributionStager.Stage(assessment, "colony-a", "city-a", 7, 2);
        Assert(ReferenceEquals(result.Source, assessment) && result.ColonyId == "colony-a" && result.DistrictInstanceId == "city-a" && result.WorldRevision == 7 && result.DistrictLevel == 2, "Zone staging source/instance/revision/level binding lost.");
        Assert(result.Gates.Count == 0 && result.Contributions.Count == 3 && result.Contributions[0].DistrictScaledAmounts.Single().Value == 10 && result.Contributions[1].DistrictScaledAmounts.Single().Value == 200 && result.Contributions[2].DistrictScaledAmounts.Single().Value == -2.5m, "Flat/district/signed contribution scaling changed.");
        Assert(result.Contributions[1].Source.Amounts.Single().Value == 100 && result.Contributions[1].Source.Amounts.Single().Origin == result.Contributions[1].DistrictScaledAmounts.Single().Origin && result.Contributions is IList list && list.IsReadOnly && result.Contributions[1].DistrictScaledAmounts is IList values && values.IsReadOnly, "Raw source/provenance changed or staging mutable.");
        var missing = ConstructionZoneContributionStager.Stage(assessment, "colony-a", "city-a", 7, null);
        Assert(missing.Gates.Any(gate => gate.DependencyId == "zone-district-level") && missing.Contributions.Count == 1 && missing.Contributions[0].DistrictScaledAmounts.Single().Value == 10, "Missing level inferred or flat contribution scaled.");
        Assert(ConstructionZoneContributionStager.Stage(assessment, "a", "b", 0, 0).Contributions.Skip(1).All(item => item.DistrictScaledAmounts.Single().Value == 0), "Zero level not preserved.");
        Assert(ConstructionZoneContributionStager.Stage(assessment, "a", "b", 0, 4096).Contributions[1].DistrictScaledAmounts.Single().Value == 409600, "Bounded maximum level changed.");
        foreach (var level in new[] { -1, 4097 })
        {
            try { ConstructionZoneContributionStager.Stage(assessment, "a", "b", 0, level); throw new Exception("Invalid level accepted."); }
            catch (ArgumentException) { }
        }
        try { ConstructionZoneContributionStager.Stage(assessment, "a", "b", -1, 2); throw new Exception("Negative revision accepted."); }
        catch (ArgumentException) { }
        await WriteZones(body + " triggered_district_planet_modifier = { potential = { always = yes } job_technician_add = 1 mult = 3 }");
        var numericMult = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 2);
        Assert(numericMult.Gates.Count == 0 && numericMult.Contributions.Count == 4 && numericMult.Contributions.Last().DistrictScaledAmounts.Single().Value == 6 && numericMult.Contributions.Last().Source.RawMultiplier == 3, "Documented triggered multiplier not applied exactly.");
        foreach (var (operand, expected) in new[] { ("-.5", 4m), (".25", -2m), ("0", 0m), ("@ratio", -.8m) })
        {
            await WriteZones("triggered_district_planet_modifier = { potential = { always = yes } modifier = { job_technician_add = -2 } multiplier = " + operand + " }");
            var scaled = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 4);
            Assert(scaled.Gates.Count == 0 && scaled.Contributions.Single().DistrictScaledAmounts.Single().Value == expected && scaled.Contributions.Single().Source.Amounts.Single().Value == -2, "Signed/fractional/zero/variable nested alias scaling changed raw source or result.");
        }
        foreach (var operand in new[] { "mult = 2 multiplier = 3", "multiplier = 2 multiplier = 3", "mult = {}", "multiplier = modifier:unknown" })
        {
            await WriteZones("triggered_planet_modifier = { potential = { always = yes } job_technician_add = 2 " + operand + " }");
            var rejected = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 4);
            Assert(rejected.Gates.Count != 0 && rejected.Contributions.Count == 0, "Invalid/dynamic triggered multiplier applied.");
        }
        await WriteZones("planet_modifier = { job_technician_add = 2 multiplier = 3 }");
        Assert(ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 4).Gates.Count != 0, "Triggered alias extrapolated into plain modifier semantics.");
        await WriteZones("triggered_district_planet_modifier = { potential = { always = yes } job_technician_add = 79228162514264337593543950335 mult = .5 }");
        var fused = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 2);
        Assert(fused.Gates.Count == 0 && fused.Contributions.Single().DistrictScaledAmounts.Single().Value == decimal.MaxValue, "Representable fused product failed due to intermediate overflow.");
        await WriteZones("triggered_planet_modifier = { potential = { always = yes } job_technician_add = 4 multiplier = -.5 }");
        var flatMultiplier = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 4096);
        Assert(flatMultiplier.Gates.Count == 0 && flatMultiplier.Contributions.Single().DistrictScaledAmounts.Single().Value == -2, "Flat triggered multiplier incorrectly scaled by district level.");
        await WriteZones("triggered_district_planet_modifier = { potential = { always = yes } job_technician_add = .0000000000000000000000000001 multiplier = 0 }");
        var zeroMultiplier = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 4096);
        Assert(zeroMultiplier.Gates.Count == 0 && zeroMultiplier.Contributions.Single().DistrictScaledAmounts.Single().Value == 0, "Explicit zero multiplier failed precision normalization.");
        await WriteZones("planet_modifier = { planet_housing_add = 1 } triggered_planet_modifier = { potential = { always = yes } job_technician_add = .0000000000000000000000000001 multiplier = .1 }");
        var underflow = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 1);
        Assert(underflow.Contributions.Count == 0 && underflow.Gates.First().Code == "quantity-overflow", "Unrepresentable final precision rounded or leaked partial contributions.");
        await WriteZones("planet_modifier = { planet_housing_add = 1 } district_planet_modifier = { planet_housing_add = 79228162514264337593543950335 }");
        var overflow = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 2);
        Assert(overflow.Contributions.Count == 0 && overflow.Gates.Any(gate => gate.Code == "quantity-overflow"), "Overflow leaked earlier staged contributions.");
        Assert(ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 1).Contributions[1].DistrictScaledAmounts.Single().Value == decimal.MaxValue, "Full unsigned decimal coefficient reconstruction failed.");
        await WriteZones("planet_modifier = { " + string.Concat(Enumerable.Range(0, 300).Select(index => "unsupported_" + index + " = 1 ")) + " } district_planet_modifier = { planet_housing_add = 79228162514264337593543950335 }");
        var saturated = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 2);
        Assert(saturated.Gates.Count <= 256 && saturated.Gates[0].Code == "quantity-overflow" && saturated.Contributions.Count == 0, "Diagnostic cap hid fatal overflow.");
        await WriteZones("planet_modifier = { planet_housing_add = 1 } district_planet_modifier = { planet_housing_add = 7.9228162514264337593543950335 }");
        var precision = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 3);
        Assert(precision.Contributions.Count == 0 && precision.Gates.Any(gate => gate.Code == "quantity-overflow"), "Decimal silently rounded district scaling.");
        await WriteZones("district_planet_modifier = { planet_housing_add = .1234567890123456789012345678 }");
        Assert(ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 2).Contributions.Single().DistrictScaledAmounts.Single().Value == .2469135780246913578024691356m, "Exact fractional scaling failed.");
        await WriteZones("triggered_district_planet_modifier = { potential = { unknown = yes } job_technician_add = 100 }");
        var unknown = ConstructionZoneContributionStager.Stage(await Assess(), "a", "b", 0, 2);
        Assert(unknown.Contributions.Count == 0 && unknown.Gates.Count != 0, "Unknown active condition became effective capacity.");
        try { ConstructionZoneContributionStager.Stage(ConstructionCatalogAssessor.Assess(await importer.ImportZoneCatalogAsync(root), "slot_city_01", new()), "a", "b", 0, 2); throw new Exception("Non-zone staging accepted."); }
        catch (ArgumentException) { }

        Task WriteZones(string modifiers) => Write(root, zones, string.Concat(new[] { "default", "urban", "research_unity", "industrial", "energy" }.Select(id => "zone_" + id + " = { base_buildtime = 1 resources = { category = planet_zones cost = { minerals = 50 } } " + modifiers + " } ")));
    });

    private static readonly IReadOnlyDictionary<string, string[]> _acceptedBuildingFiles = new Dictionary<string, string[]>
    {
        ["00_capital_buildings.txt"] = ["building_colony_shelter", "building_capital"],
        ["02_government_buildings.txt"] = ["building_precinct_house"],
        ["03_resource_buildings.txt"] = ["building_hydroponics_farm"],
        ["04_manufacturing_buildings.txt"] = ["building_foundry_1", "building_factory_1"],
        ["05_research_buildings.txt"] = ["building_research_lab_1"],
        ["06_trade_buildings.txt"] = ["building_commercial_zone"],
        ["07_amenity_buildings.txt"] = ["building_holo_theatres", "building_medical_1"],
        ["08_unity_buildings.txt"] = ["building_bureaucratic_1"],
        ["09_army_buildings.txt"] = ["building_stronghold"]
    };

    public static Task BuildingCatalogs() => Fixture(async root =>
    {
        await Write(root, "common/scripted_variables/07_scripted_variables_cosmic_storms.txt", "@storm_example = .05");
        await Write(root, "common/scripted_variables/08_scripted_variables_biogenesis.txt", "@medical_example = 500");
        foreach (var (file, ids) in _acceptedBuildingFiles)
        {
            await Write(root, "common/buildings/" + file, string.Concat(ids.Select(id => id + " = { base_buildtime = 240 can_build = no planet_limit = 1 potential = { NOR = { always = no unknown = yes } } destroy_trigger = { always = no } resources = { category = planet_buildings upkeep = { energy = 2 } } triggered_planet_modifier = { potential = { always = yes } job_colonist_add = -100 planet_housing_add = 300 } } ")));
        }
        var importer = new ConstructionDefinitionImporter();
        foreach (var id in _acceptedBuildingFiles.Values.SelectMany(ids => ids))
        {
            var source = await importer.ImportBuildingCatalogAsync(root, id);
            var result = ConstructionCatalogAssessor.Assess(source, id, new());
            Assert(source.Definitions.Count == 1 && source.Definitions[id].Kind == "building" && source.ProfileId == "construction-building-" + id + "-catalog-452-v1", "Building mapped to wrong source/profile/inventory.");
            Assert(source.Definitions[id].Fields.Any(field => field.Key == "can_build" && field.Scalar == "no") && source.Parameters.Any(number => number.FieldPath.EndsWith(":planet_limit", StringComparison.Ordinal) && number.Value == 1), "Original construction/uniqueness flags lost.");
            var modifier = result.Contributions.Single(item => item.Kind == "triggered_planet_modifier");
            Assert(modifier.Amounts.Any(amount => amount.Resource == "job_colonist_add" && amount.Value == -100) && modifier.Gates.Count == 0 && result.Destroy.Truth == ConstructionTruth.False && result.Potential.Truth == ConstructionTruth.Unknown, "Signed raw capacity, separate destroy or NOR unknown lost.");
        }
        foreach (var id in new[] { "building_machine_capital", "../building_capital", "building_major_capital", "", null })
        {
            try { await importer.ImportBuildingCatalogAsync("missing-root", id!); throw new Exception("Unknown building accepted."); }
            catch (ArgumentException) { }
        }
        const string primary = "common/buildings/00_capital_buildings.txt";
        await Write(root, primary, "building_capital = {} building_capital = {}");
        await Fail(root, async () => { await importer.ImportBuildingCatalogAsync(root, "building_capital"); });
        await Write(root, primary, "building_capital = { potential = { NOR = { always = yes unknown = yes } } }");
        Assert(ConstructionCatalogAssessor.Assess(await importer.ImportBuildingCatalogAsync(root, "building_capital"), "building_capital", new()).Potential.Truth == ConstructionTruth.False, "NOR decisive true child did not negate to false.");
        foreach (var malformedReference in new[] { "@" + new string('x', 2000), "@C:/private/source", "@private\nsource" })
        {
            await Write(root, primary, "building_capital = { base_buildtime = \"" + malformedReference + "\" }");
            try { await importer.ImportBuildingCatalogAsync(root, "building_capital"); throw new Exception("Malformed reference accepted."); }
            catch (ConstructionImportException exception) { Assert(exception.Message.Length < 256 && !exception.Message.Contains(malformedReference, StringComparison.Ordinal), "Missing-variable error echoes malformed source scalar."); }
        }
    });

    public static Task ScriptedPredicates() => Fixture(async root =>
    {
        const string buildings = "common/scripted_triggers/01_scripted_triggers_buildings.txt";
        const string colony = "common/scripted_triggers/00_scripted_triggers.txt";
        const string city = "common/districts/00_urban_districts.txt";
        const string sourceText = "planet_is_habitat_equivalent = { uses_district_set = habitat } planet_is_ecu_equivalent = { NOT = { has_deposit = prototype } OR = { uses_district_set = city_world uses_district_set = hive_world uses_district_set = machine_world } } planet_is_ring_world_equivalent = { uses_district_set = ring_world }";
        await Write(root, buildings, sourceText);
        await Write(root, colony, "is_special_colony_type = { OR = { has_modifier = penal_colony has_modifier = resort_colony has_modifier = slave_colony } }");
        await Write(root, city, "district_city = { potential = { planet_is_ecu_equivalent = yes } allow = { planet_is_habitat_equivalent = yes } unlock = { planet_is_ring_world_equivalent = no } planet_modifier = { planet_housing_add = 1 } triggered_planet_modifier = { potential = { is_special_colony_type = yes } planet_housing_add = 2 } }");
        var importer = new ConstructionDefinitionImporter();
        var conditions = await importer.ImportConditionCatalogAsync(root);
        var numerical = await importer.ImportCityCatalogAsync(root);
        Assert(conditions.Definitions.Count == 4 && conditions.ProfileId == "construction-conditions-catalog-452-v1" && conditions.Files.Any(file => file.RelativePath == buildings && file.Sha256.Length == 64), "Condition source inventory/hash lost.");
        foreach (var set in new[] { "standard", "habitat", "ring_world", "city_world", "hive_world", "machine_world" })
        {
            var context = new ConstructionContext(modifiers: [], sourceFacts: new ConstructionSourceFacts(districtSet: set, deposits: [], planetFacts: new Dictionary<ConstructionPlanetPredicate, bool?> { [ConstructionPlanetPredicate.EcuEquivalent] = !(set is "city_world" or "hive_world" or "machine_world") }));
            var result = ConstructionCatalogAssessor.Assess(numerical, "district_city", context, conditions);
            Assert(ReferenceEquals(result.ConditionSource, conditions) && result.Potential.Truth == (set is "city_world" or "hive_world" or "machine_world" ? ConstructionTruth.True : ConstructionTruth.False), "Source predicate replaced by contradictory injected conclusion.");
            Assert(result.Allow.Truth == (set == "habitat" ? ConstructionTruth.True : ConstructionTruth.False) && result.Unlock.Truth == (set == "ring_world" ? ConstructionTruth.False : ConstructionTruth.True), "Habitat/ring source condition or no inversion changed.");
        }
        var excluded = ConstructionCatalogAssessor.Assess(numerical, "district_city", new ConstructionContext(modifiers: ["resort_colony"], sourceFacts: new ConstructionSourceFacts(districtSet: "machine_world", deposits: ["prototype"])), conditions);
        Assert(excluded.Potential.Truth == ConstructionTruth.False && excluded.Contributions.Single(item => item.Kind == "triggered_planet_modifier").Condition.Truth == ConstructionTruth.True, "Prototype exclusion or special colony source lost.");
        var unknown = ConstructionCatalogAssessor.Assess(numerical, "district_city", new ConstructionContext(sourceFacts: new ConstructionSourceFacts(districtSet: "machine_world", planetFacts: new Dictionary<ConstructionPlanetPredicate, bool?> { [ConstructionPlanetPredicate.EcuEquivalent] = true })), conditions);
        Assert(unknown.Potential.Truth == ConstructionTruth.Unknown, "Unknown source dependency fell back to injected fact.");
        Assert(ConstructionCatalogAssessor.Assess(numerical, "district_city", new ConstructionContext(sourceFacts: new ConstructionSourceFacts(planetFacts: new Dictionary<ConstructionPlanetPredicate, bool?> { [ConstructionPlanetPredicate.EcuEquivalent] = true }))).Potential.Truth == ConstructionTruth.True, "No-source explicit-fact compatibility changed.");
        try { ConstructionCatalogAssessor.Assess(numerical, "district_city", new(), numerical); throw new Exception("Wrong condition profile accepted."); }
        catch (ArgumentException) { }
        await Write(root, buildings, sourceText.Replace("uses_district_set = habitat", "planet_is_habitat_equivalent = yes", StringComparison.Ordinal));
        var cyclic = ConstructionCatalogAssessor.Assess(numerical, "district_city", new(), await importer.ImportConditionCatalogAsync(root));
        Assert(cyclic.Allow.Truth == ConstructionTruth.Unknown && cyclic.Allow.Gates.Any(gate => gate.DependencyId == "condition-budget"), "Source recursion not bounded.");
        await Write(root, buildings, sourceText.Replace("uses_district_set = habitat", "uses_district_set != habitat", StringComparison.Ordinal));
        Assert(ConstructionCatalogAssessor.Assess(numerical, "district_city", new(), await importer.ImportConditionCatalogAsync(root)).Allow.Truth == ConstructionTruth.Unknown, "Unsupported source operator inferred.");
        await Write(root, buildings, sourceText);
        await Fail(root, async () => { await new ConstructionDefinitionImporter(token => Write(root, buildings, sourceText + " # mutation")).ImportConditionCatalogAsync(root); });
    });

    public static Task CatalogAssessment() => Fixture(async root =>
    {
        const string city = "common/districts/00_urban_districts.txt";
        const string text = """
        @cost = 500
        district_city = {
          base_buildtime = 480
          potential = { uses_district_set = standard owner = { is_regular_empire = yes } }
          allow = { custom_tooltip = { fail_text = example NOT = { has_carrier_flag = blocked } } hidden_trigger = { always = yes } }
          resources = {
            category = planet_districts_cities
            cost = { trigger = { NOT = { has_modifier = wooden_planet } } minerals = @cost }
            cost = { trigger = { has_modifier = wooden_planet } food = @cost mult = 0.5 }
            upkeep = { energy = 2 }
          }
          planet_modifier = { planet_housing_add = 1000 }
          triggered_planet_modifier = { potential = { owner = { has_valid_civic = civic_agrarian_idyll } } modifier = { planet_housing_add = -200 } }
          triggered_planet_modifier = { potential = { owner = { has_technology = tech_housing_1 } } job_technician_add = 200 }
          triggered_district_planet_modifier = { potential = { AND = { always = no unknown = yes } } planet_housing_add = 1 mult = modifier:dynamic }
          triggered_district_planet_modifier = { potential = { OR = { always = yes unknown = yes } } planet_housing_add = 1 mult = modifier:dynamic }
          triggered_planet_modifier = { potential = { NAND = { always = yes always = no } } planet_housing_add = 3 }
        }
        """;
        await Write(root, city, text);
        var source = await new ConstructionDefinitionImporter().ImportCityCatalogAsync(root);
        var context = new ConstructionContext(ownerExists: true, ownerFacts: new Dictionary<ConstructionOwnerPredicate, bool?> { [ConstructionOwnerPredicate.Regular] = true }, carrierFlags: [], modifiers: [], technologies: ["tech_housing_1"], sourceFacts: new ConstructionSourceFacts(districtSet: "standard", civics: ["civic_agrarian_idyll"], validCivics: ["civic_agrarian_idyll"]));
        var result = ConstructionCatalogAssessor.Assess(source, "district_city", context);
        Assert(ReferenceEquals(result.Source, source) && result.DefinitionId == "district_city" && result.Potential.Truth == ConstructionTruth.True && result.Allow.Truth == ConstructionTruth.True && result.Unlock.Truth == ConstructionTruth.Unknown, "Source binding or condition declaration changed.");
        var costs = result.Contributions.Where(item => item.Kind == "cost").ToArray();
        Assert(costs.Length == 2 && costs[0].Condition.Truth == ConstructionTruth.True && costs[1].Condition.Truth == ConstructionTruth.False && costs[0].Amounts.Single().Value == 500 && costs[1].RawMultiplier == .5m && costs[0].RawMultiplier is null && costs[0].FieldPath != costs[1].FieldPath, "Repeated cost branches flattened or raw multiplier inferred.");
        Assert(result.Contributions.Single(item => item.Kind == "upkeep").Amounts.Single().Value == 2 && result.Contributions.Any(item => item.Amounts.Any(amount => amount.Value == -200)) && result.Contributions.Any(item => item.Amounts.Any(amount => amount.Resource == "job_technician_add" && amount.Value == 200)), "Upkeep/housing/job raw contributions lost.");
        var district = result.Contributions.Where(item => item.Kind == "triggered_district_planet_modifier").ToArray();
        Assert(district[0].Condition.Truth == ConstructionTruth.False && district[0].BlockingGates.Count == 0 && district[0].Gates.Count != 0 && district[1].Condition.Truth == ConstructionTruth.True && district[1].BlockingGates.Count != 0, "Inactive unknown multipliers block, or active unknown multiplier became one.");
        var missing = ConstructionCatalogAssessor.Assess(source, "district_city", new ConstructionContext());
        Assert(missing.Potential.Truth == ConstructionTruth.Unknown && missing.Contributions.Where(item => item.Kind == "cost").All(item => item.Condition.Truth == ConstructionTruth.Unknown), "Absent facts inferred empty/false.");
        Assert(result.Contributions is IList items && items.IsReadOnly && costs[0].Amounts is IList amounts && amounts.IsReadOnly && result.Source.Definitions["district_city"].Fields.Any(field => field.Origin == result.Contributions[0].Origin), "Mutable candidates or missing provenance.");
        await Write(root, city, "district_city = { base_buildtime = 0 base_buildtime = 1 resources = { category = example cost = { minerals = -1 minerals = 4 } } planet_modifier = { planet_housing_add += 1 unsupported_effect = 4 } }");
        var malformed = ConstructionCatalogAssessor.Assess(await new ConstructionDefinitionImporter().ImportCityCatalogAsync(root), "district_city", context);
        Assert(malformed.Contributions.Single(item => item.Kind == "cost").Gates.Any(gate => gate.DependencyId == "catalog-number-unresolved"), "Negative resource amount silently accepted.");
        Assert(malformed.Contributions.Any(item => item.Kind == "base_buildtime" && item.Gates.Count != 0) && malformed.Contributions.First(item => item.Kind == "buildtime").Gates.Count != 0, "Missing/duplicate/zero buildtime accepted.");
        Assert(malformed.Contributions.Single(item => item.Kind == "planet_modifier").Gates.Count != 0, "Unsupported modifier operator/effect accepted.");
        foreach (var (body, kind, dependency) in new[]
        {
            ("resources = { category = a category = b cost = { minerals = 4 } }", "resources", "catalog-container-shape"),
            ("resources = { category = a cost = { minerals = 1 minerals = 2 } }", "cost", "catalog-duplicate-amount"),
            ("triggered_planet_modifier = { planet_housing_add = 3 }", "triggered_planet_modifier", "catalog-potential-not-declared"),
            ("planet_modifier = { planet_housing_add += 3 }", "planet_modifier", "catalog-number-unresolved")
        })
        {
            await Write(root, city, "district_city = { base_buildtime = 1 " + body + " }");
            var invalid = ConstructionCatalogAssessor.Assess(await new ConstructionDefinitionImporter().ImportCityCatalogAsync(root), "district_city", context).Contributions.Single(item => item.Kind == kind);
            Assert(invalid.BlockingGates.Any(gate => gate.DependencyId == dependency), "Isolated shape not gated: " + dependency);
        }
        try { ConstructionCatalogAssessor.Assess(source, "foreign", context); throw new Exception("Unknown definition accepted."); }
        catch (ArgumentException) { }
    });

    public static Task Catalogs() => Fixture(async root =>
    {
        const string city = "common/districts/00_urban_districts.txt";
        const string rural = "common/districts/02_rural_districts.txt";
        const string cityText = "@amount = 500 district_city = { base_buildtime = 480 resources = { cost = { minerals = @amount } cost = { food = @amount } } planet_modifier = { planet_housing_add = 1000 } triggered_planet_modifier = { potential = { unknown_condition = yes } modifier = { planet_housing_add = -200 } } custom_math = @[ 1 + 2 ] }";
        await Write(root, city, cityText);
        await Write(root, rural, "@amount = 300 " + string.Concat(new[] { "generator", "mining", "farming" }.Select(id => "district_" + id + " = { base_buildtime = 240 resources = { cost = { minerals = @amount } } } ")));
        await Write(root, "common/zones/00_zones.txt", string.Concat(new[] { "default", "urban", "research_unity", "industrial", "energy" }.Select(id => "zone_" + id + " = { base_buildtime = 360 max_buildings = 3 planet_modifier = { zone_building_slots_add = 3 } } ")));
        await Write(root, "common/zone_slots/00_zone_slots.txt", string.Concat(new[] { "slot_city_government", "slot_city_01", "slot_city_02", "slot_energy", "slot_minerals", "slot_food" }.Select(id => id + " = { unlock = { always = yes } } ")));
        var importer = new ConstructionDefinitionImporter();
        var districts = await importer.ImportCityCatalogAsync(root);
        var ruralCatalog = await importer.ImportRuralCatalogAsync(root);
        var zones = await importer.ImportZoneCatalogAsync(root);
        Assert(districts.Definitions.Count == 1 && ruralCatalog.Definitions.Count == 3 && zones.Definitions.Count == 11 && districts.ProfileId == "construction-city-catalog-452-v1" && ruralCatalog.ProfileId == "construction-rural-catalog-452-v1" && zones.ProfileId == "construction-zone-catalog-452-v1", "Fixed catalog inventory/profile changed.");
        var cityNumbers = districts.Parameters.Where(number => number.DefinitionId == "district_city").ToArray();
        Assert(cityNumbers.Count(number => number.Value == 500) == 2 && cityNumbers.Any(number => number.Value == -200) && cityNumbers.Any(number => number.Value == 1000) && ruralCatalog.Parameters.Any(number => number.DefinitionId == "district_generator" && number.Value == 300), "Repeated costs, local variables or conditional numerical branches flattened.");
        Assert(districts.Definitions["district_city"].Fields.Single(field => field.Key == "resources").Children[0].Children[0].Scalar == "@amount" && districts.Coverage.Any(item => item.Status == "unsupported" && item.Field.EndsWith(":custom_math", StringComparison.Ordinal)), "Original variable/math source lost.");
        Assert(districts.Definitions is IDictionary definitions && definitions.IsReadOnly && districts.Parameters is IList parameters && parameters.IsReadOnly && districts.Files is IList files && files.IsReadOnly, "Catalog exposes mutable data.");
        await Write(root, city, cityText + " district_city = {}");
        await Fail(root, async () => { await importer.ImportCityCatalogAsync(root); });
        await Write(root, city, cityText.Replace("@amount = 500", "@amount = @missing", StringComparison.Ordinal));
        await Fail(root, async () => { await importer.ImportCityCatalogAsync(root); });
        await Write(root, city, cityText.Replace("base_buildtime = 480", "base_buildtime = 79228162514264337593543950336", StringComparison.Ordinal));
        await Fail(root, async () => { await importer.ImportCityCatalogAsync(root); });
        await Write(root, city, cityText.Replace("@amount = 500", "@amount = 79228162514264337593543950336", StringComparison.Ordinal));
        await Fail(root, async () => { await importer.ImportCityCatalogAsync(root); });
        await Write(root, city, "district_city = { " + new string('k', 90000) + " = { " + string.Concat(Enumerable.Repeat("value = 1 ", 100)) + " } }");
        await Fail(root, async () => { await importer.ImportCityCatalogAsync(root); });
        await Write(root, city, cityText);
        await Fail(root, async () => { await new ConstructionDefinitionImporter(token => Write(root, city, cityText + " # changed")).ImportCityCatalogAsync(root); });
        await Write(root, city, cityText);
        using var cancellation = new CancellationTokenSource();
        await Cancelled(() => new ConstructionDefinitionImporter(token => { cancellation.Cancel(); return Task.CompletedTask; }).ImportZoneCatalogAsync(root, cancellation.Token));
        Assert((await importer.ImportCityCatalogAsync(root)).Definitions.Count == 1 && (await importer.ImportRuralCatalogAsync(root)).Definitions.Count == 3 && (await importer.ImportZoneCatalogAsync(root)).Definitions.Count == 11, "Catalog importer retained failed/cancelled state.");
    });

    private const string BUILDINGS = "common/buildings/03_resource_buildings.txt";
    private static readonly string[] _globals = ["common/scripted_variables/00_scripted_variables.txt", "common/scripted_variables/100_scripted_variables_zones.txt", "common/scripted_variables/10_scripted_variables_nomads.txt"];

    public static Task Features() => Fixture(async root =>
    {
        const string blockers = "common/deposits/01_blocker_deposits.txt";
        const string passive = "common/deposits/01_planetary_deposits.txt";
        const string inline = "common/inline_scripts/checks/deposit.txt";
        const string original = "d_failing_infrastructure = { category = deposit_cat_blockers time = 120 potential = { always = no } blocker_swap_types = { d_rushing_waterfalls } } d_decrepit_dwellings = { category = deposit_cat_blockers time = 120 inline_script = checks/deposit on_cleared = { create_pop_group = { species = prev } } }";
        await Write(root, blockers, original);
        await Write(root, passive, string.Concat(new[] { "d_hot_springs", "d_rushing_waterfalls", "d_tempestous_mountain", "d_veiny_cliffs", "d_prosperous_mesa", "d_rich_mountain", "d_rugged_woods", "d_fertile_lands", "d_black_soil" }.Select(id => id + " = { category = deposit_cat_energy planet_modifier = { district_generator_max_add = 1 } } ")));
        await Write(root, inline, "ai_weight = { weight = 1000 }");
        var importer = new ConstructionDefinitionImporter();
        var projection = await importer.ImportFeaturesAsync(root);
        Assert(projection.Definitions.Count == 11 && projection.Definitions.Values.Count(definition => definition.Kind == "blocker") == 2, "Feature classification changed.");
        var decrepit = projection.Definitions["d_decrepit_dwellings"];
        Assert(decrepit.Fields.Single(field => field.Key == "on_cleared").Children.Single().Key == "create_pop_group", "Clear effect dropped.");
        Assert(decrepit.Fields.Single(field => field.Key == "ai_weight").InlineCallSites.Count == 1, "Scalar inline provenance lost.");
        Assert(projection.Definitions is System.Collections.IDictionary dictionary && dictionary.IsReadOnly && !JsonSerializer.Serialize(projection).Contains(root, StringComparison.OrdinalIgnoreCase), "Mutable or unsafe feature projection.");
        await Write(root, inline, "inline_script = checks/deposit");
        await Fail(root, async () => { await importer.ImportFeaturesAsync(root); });
        await Write(root, inline, "inline_script = ../outside");
        await Fail(root, async () => { await importer.ImportFeaturesAsync(root); });
        await Write(root, inline, "unknown = $MISSING$");
        await Fail(root, async () => { await importer.ImportFeaturesAsync(root); });
        await Write(root, inline, "ai_weight = { weight = 1000 }");
        await Write(root, blockers, original.Replace("deposit_cat_blockers", "deposit_cat_energy", StringComparison.Ordinal));
        await Fail(root, async () => { await importer.ImportFeaturesAsync(root); });
        await Write(root, blockers, original);
        await Fail(root, async () => { await new ConstructionDefinitionImporter(token => Write(root, blockers, original + " # changed")).ImportFeaturesAsync(root); });
        using var cancellation = new CancellationTokenSource();
        await Cancelled(() => new ConstructionDefinitionImporter(token => { cancellation.Cancel(); return Task.CompletedTask; }).ImportFeaturesAsync(root, cancellation.Token));
        Assert((await importer.ImportFeaturesAsync(root)).Definitions.Count == 11, "Feature import retained failed/cancelled state.");
    });

    public static Task Placement() => Fixture(async root =>
    {
        const string zones = "common/zones/00_zones.txt";
        const string original = "zone_default = { max_buildings = 6 include = { all } } zone_urban = { max_buildings = 3 include = { all } excluded_building_sets = { farming } planet_modifier = { zone_building_slots_add = 3 } }";
        await Write(root, zones, original);
        await Write(root, "common/zone_slots/00_zone_slots.txt", string.Concat(new[] { "slot_city_government", "slot_city_01", "slot_city_02", "slot_energy" }.Select(id => id + " = { unlock = { always = yes } } ")));
        await Write(root, "common/districts/02_rural_districts.txt", "district_generator = { base_buildtime = 240 zone_slots = { slot_energy } resources = { cost = { minerals = 300 } upkeep = { energy = 1 } } }");
        var importer = new ConstructionDefinitionImporter();
        var placement = await importer.ImportPlacementAsync(root);
        var building = (await Import(root)).Definition;
        var empty = new Dictionary<string, string>();
        var state = new Stellaris.Core.Construction.ConstructionPlacementState("zone_urban", true, 2, "test-capacity-policy", ["a", "b"], new Dictionary<string, string> { ["a"] = "installed" }, empty);
        var result = Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(placement, building, state, "b");
        Assert(result.Allowed == Stellaris.Core.Construction.ConstructionTruth.True && result.DeclaredMaxBuildings == 3 && state.Slots.Count == 2, "Declared/additive/effective capacity conflated.");
        Assert(Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(placement, building, state, "a").Code == "slot-unavailable", "Installed slot available twice.");
        var reserved = new Stellaris.Core.Construction.ConstructionPlacementState("zone_urban", true, 2, "test-capacity-policy", ["a", "b"], empty, new Dictionary<string, string> { ["b"] = "order" });
        Assert(Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(placement, building, reserved, "b").Code == "slot-unavailable", "Reserved slot available twice.");
        var unresolved = new Stellaris.Core.Construction.ConstructionPlacementState("zone_urban", true, null, null, ["a"], empty, empty);
        Assert(Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(placement, building, unresolved, "a").Code == "zone-effective-capacity-unresolved", "Missing capacity inferred.");
        var invalid = new Stellaris.Core.Construction.ConstructionPlacementState("zone_urban", true, 1, "test", ["a"], new Dictionary<string, string> { ["a"] = "building" }, new Dictionary<string, string> { ["a"] = "order" });
        Assert(Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(placement, building, invalid, "a").Code == "zone-instance-state-invalid", "Double occupancy not detected.");
        Assert(placement.Definitions.Count == 7 && placement.Definitions is System.Collections.IDictionary dictionary && dictionary.IsReadOnly, "Mutable placement projection.");
        await Write(root, zones, original.Replace("farming", "hydroponics", StringComparison.Ordinal));
        var excluded = await importer.ImportPlacementAsync(root);
        Assert(Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(excluded, building, state, "b").Code == "building-set-excluded", "Excluded set allowed.");
        await Write(root, zones, original.Replace("excluded_building_sets = { farming }", "include = { building_hydroponics_farm }", StringComparison.Ordinal));
        var duplicateMembership = await importer.ImportPlacementAsync(root);
        Assert(Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(duplicateMembership, building, state, "b").Code == "zone-membership-shape", "Duplicate scalar-list membership interpreted.");
        await Write(root, zones, original.Replace("excluded_building_sets = { farming }", "included_building_sets = { hydroponics } excluded_building_sets = { hydroponics }", StringComparison.Ordinal));
        var conflict = await importer.ImportPlacementAsync(root);
        Assert(Stellaris.Core.Construction.ConstructionPlacementValidator.Validate(conflict, building, state, "b").Code == "zone-membership-precedence-unresolved", "Conflicting membership precedence guessed.");
        await Write(root, zones, original);
        await Fail(root, async () => { await new ConstructionDefinitionImporter(token => Write(root, zones, original + " # changed")).ImportPlacementAsync(root); });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Cancelled(() => importer.ImportPlacementAsync(root, cancellation.Token));
        await Write(root, zones, "zone_default = {} zone_urban = {} zone_urban = {}");
        await Fail(root, async () => { await importer.ImportPlacementAsync(root); });
    });

    public static Task Projection() => Fixture(async root =>
    {
        var projection = await Import(root);
        AssertValues(projection);
        Assert(projection.Files.Count == 7, "Unexpected dependency closure.");
        Assert(!JsonSerializer.Serialize(projection).Contains(root, StringComparison.OrdinalIgnoreCase), "Absolute source path leaked.");
        foreach (var values in new IEnumerable[] { projection.Files, projection.Coverage, projection.Definition.Costs, projection.Definition.Fields, projection.Definition.BuildingSets, projection.Definition.Prerequisites })
        {
            Assert(values is IList list && list.IsReadOnly, "Mutable projection collection.");
        }
        var capacity = Flatten(projection.Definition.Fields).Single(field => field.Key == "job_farmer_add");
        Assert(capacity.Scalar == "200" && capacity.InlineCallSites.Count == 1 && capacity.Origin.Source.EndsWith("checks/jobs.txt", StringComparison.Ordinal), "Inline provenance lost.");
        Assert(Flatten(projection.Definition.Fields).Any(field => field.Operator == "<"), "Comparison operator lost.");
        await Hashes(root, projection);
    });

    public static Task Variables() => Fixture(async root =>
    {
        await Write(root, BUILDINGS, "@time = 361\n@capacity = 201\n" + Definition());
        await Write(root, "common/inline_scripts/checks/jobs.txt", "@capacity = 999\ninline_script = { script = checks/leaf COUNT = $AMOUNT$ }");
        await Write(root, "common/inline_scripts/checks/leaf.txt", "triggered_planet_modifier = { job_farmer_add = $COUNT$ }");
        var projection = await Import(root);
        var field = Flatten(projection.Definition.Fields).Single(value => value.Key == "job_farmer_add");
        Assert(projection.Definition.BaseBuildtime == 361 && field.Scalar == "201" && field.InlineCallSites.Count == 2, "Local/caller precedence or provenance failed.");
        await Write(root, "common/inline_scripts/checks/leaf.txt", "inline_script = { script = checks/jobs }");
        await Fail(root);
    });

    public static Task Unsupported() => Fixture(async root =>
    {
        await Write(root, BUILDINGS, Definition().Replace("potential = {", "unknown = { arithmetic = @[ 1 + 2 ] } potential = {", StringComparison.Ordinal));
        var projection = await Import(root);
        Assert(projection.Coverage.Any(item => item.Status == "unsupported") && projection.Coverage.Any(item => item.Status == "context-required"), "Unknown condition became supported.");
        Assert(projection.Definition.Costs.All(cost => cost.Trigger is not null), "Conditional costs were evaluated or discarded.");
    });

    public static Task Invalid() => Fixture(async root =>
    {
        foreach (var source in new[] { Definition() + Definition(), "@time = @missing\n" + Definition(), "@time = @other\n@other = @time\n" + Definition(), Definition().Replace("category = resource", "category = resource category = resource", StringComparison.Ordinal), Definition().Replace("@time", "@[ 1 + 2 ]", StringComparison.Ordinal) })
        {
            await Write(root, BUILDINGS, source);
            await Fail(root);
        }
        await Write(root, BUILDINGS, Definition());
        await Write(root, _globals[1], "@time = 99");
        await Fail(root);
        await Write(root, _globals[1], "@unused = 7");
        File.Delete(Path.Combine(root, _globals[2]));
        await Fail(root);
    });

    public static Task Safety() => Fixture(async root =>
    {
        foreach (var version in new[] { "v4.5.3", "4.5.2", "v4.5.2-mod" })
        {
            await Write(root, "launcher-settings.json", JsonSerializer.Serialize(new { rawVersion = version }));
            await Fail(root);
        }
        await Write(root, "launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}");
        foreach (var id in new[] { "../outside", "checks/../jobs", "C:/secret", "/absolute", "checks//jobs", "checks/./jobs", "" })
        {
            await Write(root, BUILDINGS, Definition().Replace("script = checks/jobs", "script = \"" + id + "\"", StringComparison.Ordinal));
            await Fail(root);
        }
        await File.WriteAllBytesAsync(Path.Combine(root, BUILDINGS), [0xc3, 0x28]);
        await Fail(root);
        await Write(root, BUILDINGS, "#" + new string('x', 2 * 1024 * 1024));
        await Fail(root);
        await Write(root, BUILDINGS, string.Concat(Enumerable.Repeat("nest = { ", 65)) + new string('}', 65));
        await Fail(root);
        await Write(root, BUILDINGS, Definition());
        await Write(root, "common/inline_scripts/checks/jobs.txt", string.Concat(Enumerable.Repeat("unknown = yes\n", 50001)));
        await Fail(root);
    });

    public static Task Atomic() => Fixture(async root =>
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Cancelled(() => Import(root, cancellation.Token));
        var reusable = new ConstructionDefinitionImporter();
        var baseline = await reusable.ImportAsync(root);
        ConstructionProjection? published = null;
        var changed = new ConstructionDefinitionImporter(async token => await Write(root, _globals[0], "@time = 361\n@cost = 400\n@capacity = 200"));
        await Fail(root, async () => published = await changed.ImportAsync(root));
        Assert(published is null && baseline.Definition.BaseBuildtime == 360 && (await reusable.ImportAsync(root)).Definition.BaseBuildtime == 361, "Partial publication, mutation or cross-call cache.");
        using var during = new CancellationTokenSource();
        var cancelled = new ConstructionDefinitionImporter(token => { during.Cancel(); return Task.CompletedTask; });
        await Cancelled(async () => published = await cancelled.ImportAsync(root, during.Token));
        Assert(published is null, "Cancelled import published a result.");
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    });

    public static Task Reparse() => Fixture(async root =>
    {
        var target = Path.Combine(root, "target.txt");
        var link = Path.Combine(root, _globals[0]);
        File.Move(link, target);
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw new SkipException("Symlink creation is unavailable.");
        }
        try
        {
            await Fail(root);
        }
        finally
        {
            Assert((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0 && new FileInfo(link).ResolveLinkTarget(true)?.FullName == target, "Unsafe link cleanup.");
            File.Delete(link);
        }
    });

    public static Task Fanout() => Fixture(async root =>
    {
        var payload = new string('x', 1024 * 1024);
        await Write(root, BUILDINGS, Definition().Replace("AMOUNT = @capacity", "AMOUNT = @capacity BIG = \"" + payload + "\"", StringComparison.Ordinal));
        await Write(root, "common/inline_scripts/checks/jobs.txt", "inline_script = { script = checks/empty " + string.Concat(Enumerable.Range(0, 9).Select(index => $"A{index} = \"$BIG$\" ")) + "}");
        await Write(root, "common/inline_scripts/checks/empty.txt", "# No expanded DTO nodes.");
        await Fail(root);
    });

    public static async Task Budgets()
    {
        await Fixture(async root =>
        {
            await Write(root, BUILDINGS, string.Concat(Enumerable.Range(0, 34).Select(index => $"@v{index} = @v{index + 1}\n")) + "@v34 = 360\n@time = @v0\n" + Definition());
            await Fail(root);
        });
        await Fixture(async root =>
        {
            for (var index = 0; index < 18; index++)
            {
                await Write(root, $"common/inline_scripts/checks/chain{index}.txt", index == 17 ? "unknown = yes" : $"inline_script = {{ script = checks/chain{index + 1} }}");
            }
            await Write(root, "common/inline_scripts/checks/jobs.txt", "inline_script = { script = checks/chain0 }");
            await Fail(root);
        });
        await Fixture(async root =>
        {
            for (var index = 0; index < 28; index++)
            {
                await Write(root, $"common/inline_scripts/checks/file{index}.txt", "unknown = yes");
            }
            await Write(root, "common/inline_scripts/checks/jobs.txt", string.Concat(Enumerable.Range(0, 28).Select(index => $"inline_script = {{ script = checks/file{index} }}\n")));
            await Fail(root);
        });
        await Fixture(async root =>
        {
            for (var index = 0; index < 5; index++)
            {
                await Write(root, $"common/inline_scripts/checks/large{index}.txt", "#" + new string('x', 1800000));
            }
            await Write(root, "common/inline_scripts/checks/jobs.txt", string.Concat(Enumerable.Range(0, 5).Select(index => $"inline_script = {{ script = checks/large{index} }}\n")));
            await Fail(root);
        });
        await Fixture(async root =>
        {
            await Write(root, "common/inline_scripts/checks/large.txt", "unknown = \"" + new string('x', 100000) + "\"");
            await Write(root, "common/inline_scripts/checks/jobs.txt", string.Concat(Enumerable.Repeat("inline_script = { script = checks/large }\n", 90)));
            await Fail(root);
        });
    }

    public static async Task Real(string root)
    {
        var projection = await Import(root);
        AssertValues(projection);
        Assert(projection.Files.Count == 7, "Unexpected real source closure.");
        await Hashes(root, projection);
        AssessmentChecks.Real(projection);
        AssessmentChecks.ImageBinding(projection, await new Stellaris.Core.Assets.M3AssetManifestBuilder().BuildAsync(root));
        var cityCatalog = await new ConstructionDefinitionImporter().ImportCityCatalogAsync(root);
        var districtCatalog = await new ConstructionDefinitionImporter().ImportRuralCatalogAsync(root);
        var zoneCatalog = await new ConstructionDefinitionImporter().ImportZoneCatalogAsync(root);
        var conditionCatalog = await new ConstructionDefinitionImporter().ImportConditionCatalogAsync(root);
        var ownerConditions = await new ConstructionDefinitionImporter().ImportOwnerConditionCatalogAsync(root);
        var startupConditions = await new ConstructionDefinitionImporter().ImportStartupConditionCatalogAsync(root);
        CapitalConditionChecks.Membership(startupConditions);
        var capitalConditions = await new ConstructionDefinitionImporter().ImportCapitalConditionCatalogAsync(root);
        CapitalHabitatChecks.Facts(capitalConditions);
        InstalledCapacityChecks.RealFarming(districtCatalog, capitalConditions);
        var functionalCivic = await new ConstructionDefinitionImporter().ImportFunctionalCivicCatalogAsync(root);
        foreach (var template in new[] { "economic", "scientific", "military" })
        {
            var collected = ConstructionFunctionalCivicCollection.Collect(functionalCivic, template, new(sourceFacts: new ConstructionSourceFacts(validCivics: ["civic_functional_architecture"])));
            Assert(collected.Potential.Truth == ConstructionTruth.True && collected.Possible.Truth == ConstructionTruth.Unknown && collected.ConstructionOperands.Select(item => item.Value).SequenceEqual(new[] { -.15m, .15m }) && collected.ExcludedModifiers.Single().Key == "add_attunement_the_composer_of_strands", "Real accepted civic construction declarations changed.");
        }
        Console.WriteLine($"Functional civic: {functionalCivic.Files.Count} files / {functionalCivic.Definitions.Count} definition / {functionalCivic.Parameters.Count} raw parameters.");
        Assert(ownerConditions.Files.Count == 6 && ownerConditions.Definitions.Count == 6, "Real extended condition closure changed.");
        foreach (var file in ownerConditions.Files)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(root, file.RelativePath));
            Assert(bytes.LongLength == file.ByteLength && Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == file.Sha256, "Extended condition source hash changed.");
        }
        var acceptedCivic = new ConstructionContext(ownerExists: true, technologies: [], sourceFacts: new ConstructionSourceFacts(civics: ["civic_functional_architecture"], validCivics: ["civic_functional_architecture"]));
        var neither = new ConstructionContext(ownerExists: true, technologies: [], sourceFacts: new ConstructionSourceFacts(validCivics: []));
        Assert(ConstructionCatalogAssessor.Assess(zoneCatalog, "slot_energy", acceptedCivic, ownerConditions).Unlock.Truth == ConstructionTruth.True && ConstructionCatalogAssessor.Assess(zoneCatalog, "slot_energy", neither, ownerConditions).Unlock.Truth == ConstructionTruth.False && ConstructionCatalogAssessor.Assess(zoneCatalog, "slot_energy", new(ownerExists: true), ownerConditions).Unlock.Truth == ConstructionTruth.Unknown, "Real selected energy zone unlock did not preserve valid civic/false/unknown context.");
        var economicCategories = await new ConstructionDefinitionImporter().ImportEconomicCategoriesAsync(root);
        AssertEconomicCategories(economicCategories);
        foreach (var file in economicCategories.Source.Files)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(root, file.RelativePath));
            Assert(bytes.LongLength == file.ByteLength && Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == file.Sha256, "Economic category source hash changed.");
        }
        Console.WriteLine($"Economic categories: {economicCategories.Source.Files.Count} files / {economicCategories.Categories.Count} definitions.");
        Assert(cityCatalog.Definitions.Count == 1 && districtCatalog.Definitions.Count == 3 && zoneCatalog.Definitions.Count == 11, "Real fixed catalog coverage changed.");
        Assert(districtCatalog.Parameters.Any(number => number.DefinitionId == "district_generator" && number.FieldPath.EndsWith(":base_buildtime", StringComparison.Ordinal) && number.Value == 240) && zoneCatalog.Parameters.Any(number => number.DefinitionId == "zone_urban" && number.FieldPath.EndsWith(":base_buildtime", StringComparison.Ordinal) && number.Value == 360), "Real numeric variable resolution failed.");
        var ordinaryFacts = Enum.GetValues<ConstructionOwnerPredicate>().ToDictionary(key => key, key => (bool?)(key is ConstructionOwnerPredicate.Regular or ConstructionOwnerPredicate.UsesFood));
        var planetFacts = Enum.GetValues<ConstructionPlanetPredicate>().ToDictionary(key => key, key => (bool?)false);
        var catalogContext = new ConstructionContext(ownerExists: true, ownerFacts: ordinaryFacts, fromNomadic: false, modifiers: [], carrierFlags: [], technologies: ["tech_power_plant_1", "tech_mechanized_mining", "tech_hydroponics"], sourceFacts: new ConstructionSourceFacts("standard", "default", [], [], [], [], planetFacts, validCivics: []));
        var civicBasis = ConstructionFunctionalCivicCollection.Collect(functionalCivic, "economic", catalogContext);
        foreach (var catalog in new[] { cityCatalog, districtCatalog, zoneCatalog })
        {
            foreach (var definition in catalog.Definitions.Keys) { _ = ConstructionCatalogAssessor.Assess(catalog, definition, catalogContext); }
        }
        var generator = ConstructionCatalogAssessor.Assess(districtCatalog, "district_generator", catalogContext);
        var generatorBinding = ConstructionResourceCategoryBinding.Bind(generator, economicCategories);
        Assert(generatorBinding.BindingGates.Count == 0 && generatorBinding.Branches.All(item => item.Mult.TargetCategory == "planet_districts_generator"), "Real generator resource category unresolved.");
        AssertBasisAssociation(ConstructionNumericalBasisAssociation.Bind(generatorBinding, civicBasis, "economic"));
        var extendedGenerator = ConstructionCatalogAssessor.Assess(districtCatalog, "district_generator", catalogContext, ownerConditions);
        var extendedBinding = ConstructionResourceCategoryBinding.Bind(extendedGenerator, economicCategories);
        Assert(extendedBinding.BindingGates.Count == 0 && ReferenceEquals(extendedBinding.Assessment.ConditionSource, ownerConditions), "Extended owner condition source lost or mismatch verification changed.");
        AssertBasisAssociation(ConstructionNumericalBasisAssociation.Bind(extendedBinding, civicBasis, "economic"));
        Assert(generator.Potential.Truth == ConstructionTruth.True && generator.Allow.Truth == ConstructionTruth.True && generator.Prerequisites.Truth == ConstructionTruth.True, "Real generator eligibility conditions unresolved despite explicit facts.");
        Assert(generator.Contributions.Single(item => item.Kind == "buildtime").Amounts.Single().Value == 240 && generator.Contributions.Single(item => item.Kind == "cost").Amounts.Single().Value == 300 && generator.Contributions.Single(item => item.Kind == "upkeep").Amounts.Single().Value == 1, "Real raw generator cost/time/infrastructure upkeep changed.");
        Assert(generator.Contributions.Where(item => item.Condition.Truth == ConstructionTruth.True).Any(item => item.Amounts.Any(amount => amount.Resource == "job_technician_add" && amount.Value == 200)) && generator.Contributions.Any(item => item.Kind == "planet_modifier" && item.Amounts.Single().Value == 200), "Real generator independent housing/job capacity lost.");
        var zoneEnergy = ConstructionCatalogAssessor.Assess(zoneCatalog, "zone_energy", catalogContext, conditionCatalog);
        var zoneBinding = ConstructionResourceCategoryBinding.Bind(zoneEnergy, economicCategories);
        Assert(zoneBinding.BindingGates.Count == 0 && zoneBinding.Branches.All(item => item.Mult.TargetCategory == "planet_zones"), "Real zone resource category unresolved.");
        AssertBasisAssociation(ConstructionNumericalBasisAssociation.Bind(zoneBinding, civicBasis, "economic"));
        Assert(conditionCatalog.Definitions.Count == 4 && zoneEnergy.Contributions.Any(item => item.Kind == "triggered_district_planet_modifier" && item.Condition.Truth == ConstructionTruth.True && item.Amounts.Any(amount => amount.Resource == "job_technician_add" && amount.Value == 100)), "Real source-backed ordinary zone technician condition/capacity failed.");
        var realCity = ConstructionCatalogAssessor.Assess(cityCatalog, "district_city", catalogContext, conditionCatalog);
        Assert(realCity.Potential.Truth == ConstructionTruth.True && realCity.Allow.Truth == ConstructionTruth.True && ReferenceEquals(realCity.ConditionSource, conditionCatalog), "Real city source-backed special-colony condition failed.");
        var zoneStage = ConstructionZoneContributionStager.Stage(zoneEnergy, "explicit-colony", "explicit-generator", 1, 3);
        InstalledCapacityChecks.RealZone(generator, zoneEnergy);
        InstalledCapacityChecks.RealZoneBoundary(zoneCatalog, catalogContext, conditionCatalog);
        InstalledCapacityChecks.RealOwnerFlags(zoneCatalog, conditionCatalog);
        Assert(zoneStage.Contributions.Any(item => item.DistrictScaledAmounts.Any(amount => amount.Resource == "job_technician_add" && amount.Value == 300)) && zoneStage.Contributions.Any(item => item.DistrictScaledAmounts.Any(amount => amount.Resource == "zone_building_slots_add" && amount.Value == 3)) && zoneStage.Gates.Count == 0, "Real source district-level technician scaling or flat slot contribution changed.");
        Console.WriteLine($"Condition catalog: {conditionCatalog.Files.Count} files / {conditionCatalog.Definitions.Count} definitions.");
        var shelterClosure = await new ConstructionDefinitionImporter().ImportShelterClosureAsync(root);
        foreach (var id in _acceptedBuildingFiles.Values.SelectMany(ids => ids))
        {
            var building = await new ConstructionDefinitionImporter().ImportBuildingCatalogAsync(root, id);
            var assessment = ConstructionCatalogAssessor.Assess(building, id, catalogContext, conditionCatalog);
            if (id == "building_hydroponics_farm") { InstalledCapacityChecks.Real(generator, assessment); }
            var buildingBinding = ConstructionResourceCategoryBinding.Bind(assessment, economicCategories);
            Assert(buildingBinding.BindingGates.Count == 0 && buildingBinding.Branches.All(item => item.Mult.TargetCategory is "planet_buildings" or "planet_buildings_strongholds"), "Real building infrastructure category unresolved.");
            var buildingBasis = ConstructionNumericalBasisAssociation.Bind(buildingBinding, civicBasis, "economic");
            if (assessment.Contributions.Any(item => item.Kind == "cost")) { AssertBasisAssociation(buildingBasis); }
            else { Assert(buildingBasis.CostCandidates.Count == 0 && buildingBasis.AssociationGates.Single().DependencyId == "cost-branch-unavailable", "Absent real cost invented."); }
            Assert(buildingBasis.Civic.ValidatedApplicability.Truth == ConstructionTruth.False && ReferenceEquals(buildingBasis.Resources.Assessment.ConditionSource, conditionCatalog), "Real association promoted explicit invalid civic or lost condition source.");
            Assert(ReferenceEquals(assessment.Source, building) && building.Definitions.Count == 1 && building.Definitions[id].Kind == "building", "Real building catalog source identity lost.");
            if (id == "building_colony_shelter")
            {
                Assert(building.Definitions[id].Fields.Any(field => field.Key == "can_build" && field.Scalar == "no") && assessment.Contributions.Any(item => item.Amounts.Any(amount => amount.Resource == "job_colonist_add" && amount.Value == -100)), "Real shelter restriction/signed raw adjustments lost.");
            }
            if (id is "building_colony_shelter" or "building_capital")
            {
                ShelterScopeChecks.Real(assessment);
                ShelterClosureChecks.Real(building, shelterClosure, economicCategories);
                CapitalConditionChecks.Real(building, startupConditions, economicCategories);
                CapitalHabitatChecks.Real(building, capitalConditions, economicCategories);
                InstalledCapacityChecks.RealCapitalRefusal(building, districtCatalog, catalogContext, capitalConditions);
            }
            Console.WriteLine($"Building catalog {id}: {building.Files.Count} files / {building.Parameters.Count} raw parameters.");
        }
        Console.WriteLine($"City catalog: {cityCatalog.Files.Count} files / {cityCatalog.Definitions.Count} definitions / {cityCatalog.Parameters.Count} raw parameters; rural catalog: {districtCatalog.Files.Count} files / {districtCatalog.Definitions.Count} definitions / {districtCatalog.Parameters.Count} raw parameters; zone catalog: {zoneCatalog.Files.Count} files / {zoneCatalog.Definitions.Count} definitions / {zoneCatalog.Parameters.Count} raw parameters.");
        var placement = await new ConstructionDefinitionImporter().ImportPlacementAsync(root);
        Assert(placement.Definitions.Count == 7 && placement.Definitions["district_generator"].Kind == "district", "Real placement inventory changed.");
        Console.WriteLine($"Placement profile: {placement.ProfileId}; files: {placement.Files.Count}; definitions: {placement.Definitions.Count}");
        var features = await new ConstructionDefinitionImporter().ImportFeaturesAsync(root);
        Assert(features.Definitions.Count == 11 && features.Definitions["d_decrepit_dwellings"].Fields.Any(field => field.Key == "on_cleared") && features.Definitions["d_failing_infrastructure"].Fields.Any(field => field.Key == "blocker_swap_types"), "Real feature dependencies lost.");
        Console.WriteLine($"Feature profile: {features.ProfileId}; files: {features.Files.Count}; definitions: {features.Definitions.Count}");
        Console.WriteLine($"Source profile: {projection.ProfileId}; files: {projection.Files.Count}; coverage: {projection.Coverage.Count}");
    }

    private static void AssertValues(ConstructionProjection projection)
    {
        var definition = projection.Definition;
        Assert(projection.GameVersion == "4.5.2" && definition.BaseBuildtime == 360 && definition.Id == "building_hydroponics_farm", "Wrong raw definition.");
        Assert(definition.Category == "resource" && definition.EconomicCategory == "planet_buildings", "Categories conflated.");
        Assert(definition.BuildingSets.SequenceEqual(["hydroponics"]) && definition.Prerequisites.SequenceEqual(["tech_hydroponics"]), "Wrong sets/prerequisites.");
        Assert(definition.Upkeep.Single().Resource == "energy" && definition.Upkeep.Single().Value == 2, "Infrastructure upkeep lost.");
        Assert(definition.Costs.Count == 2 && definition.Costs[0].Amounts.Single().Resource == "minerals" && definition.Costs[0].Amounts.Single().Value == 400 && definition.Costs[0].RawMultiplier is null, "Regular raw branch changed.");
        Assert(definition.Costs[1].Amounts.Single().Resource == "volatile_motes" && definition.Costs[1].Amounts.Single().Value == 400 && definition.Costs[1].RawMultiplier == .1m, "Nomadic raw branch/mult conflated.");
    }

    private static async Task Hashes(string root, ConstructionProjection projection)
    {
        foreach (var file in projection.Files)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(root, file.RelativePath));
            Assert(file.ByteLength == bytes.LongLength && file.Sha256 == Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), "Source hash/length mismatch.");
        }
    }

    private static Task<ConstructionProjection> Import(string root, CancellationToken token = default) => new ConstructionDefinitionImporter().ImportAsync(root, token);
    private static async Task Fail(string root, Func<Task>? run = null)
    {
        try
        {
            await (run is null ? Import(root) : run());
        }
        catch (ConstructionImportException exception)
        {
            Assert(exception.InnerException is null && !exception.ToString().Contains(root, StringComparison.OrdinalIgnoreCase), "Unsafe exception details.");
            return;
        }
        throw new Exception("Expected fail-closed import.");
    }
    private static async Task Cancelled(Func<Task> run)
    {
        try { await run(); }
        catch (OperationCanceledException) { return; }
        throw new Exception("Expected cancellation.");
    }
    public static Task ShelterClosureImport() => Fixture(async root =>
    {
        var bodies = ConstructionShelterClosureInventory.Create().GroupBy(item => item.Path).ToDictionary(group => group.Key, group => string.Join("\n", group.SelectMany(item => item.Ids).Select(id => id + " = { always = no }")), StringComparer.Ordinal);
        var biogenesis = "common/scripted_triggers/05_scripted_triggers_biogenesis.txt";
        bodies[biogenesis] += "\nunselected = { [[!SPECIES] always = no ] [[SPECIES] always = yes ] }";
        foreach (var pair in bodies) { await Write(root, pair.Key, pair.Value); }
        var importer = new ConstructionDefinitionImporter();
        var closure = await importer.ImportShelterClosureAsync(root);
        ConstructionShelterClosureInventory.Validate(closure);
        Assert(closure.Definitions.Count == 41 && closure.Definitions["set_species_traits_evopred_count_variable"].Kind == "storedVariableEffect", "Whole closure import inventory/lifecycle kind changed.");
        Assert((await importer.ImportConditionCatalogAsync(root)).Definitions.Count == 4 && (await importer.ImportOwnerConditionCatalogAsync(root)).Definitions.Count == 6 && (await importer.ImportStartupConditionCatalogAsync(root)).Definitions.Count == 7 && (await importer.ImportCapitalConditionCatalogAsync(root)).Definitions.Count == 8, "Whole import changed legacy inventory.");
        await Write(root, biogenesis, bodies[biogenesis].Replace("is_purity_authority = { always = no }", "is_purity_authority = { [[!SPECIES] always = no ] }", StringComparison.Ordinal));
        await Fail(root, () => importer.ImportShelterClosureAsync(root));
        await Write(root, biogenesis, bodies[biogenesis]);
        await Write(root, biogenesis, bodies[biogenesis] + "\nnested_unselected = { [[OUTER] [[!INNER] always = no ] ] }");
        Assert((await importer.ImportShelterClosureAsync(root)).Definitions.Count == 41, "Nested unselected headers changed selected closure.");
        foreach (var replacement in new[] { "is_purity_authority = { [[!SPECIES] [[INNER] always = no ] ] }", "is_purity_authority = { inline_script = checks/conditional }" })
        {
            await Write(root, "common/inline_scripts/checks/conditional.txt", "[[!SPECIES] always = no ]");
            await Write(root, biogenesis, bodies[biogenesis].Replace("is_purity_authority = { always = no }", replacement, StringComparison.Ordinal));
            await Fail(root, () => importer.ImportShelterClosureAsync(root));
        }
        foreach (var suffix in new[] { "\nunterminated = { [[!SPECIES] always = no }", "\nexcessive_depth = { " + string.Concat(Enumerable.Repeat("[[A] ", 65)) + string.Concat(Enumerable.Repeat("] ", 65)) + "}", "\nexcessive_headers = { " + string.Concat(Enumerable.Repeat("[[A] ] ", 50001)) + "}" })
        {
            await Write(root, biogenesis, bodies[biogenesis] + suffix);
            await Fail(root, () => importer.ImportShelterClosureAsync(root));
        }
        await Write(root, biogenesis, bodies[biogenesis].Replace("is_purity_authority = { always = no }", "is_purity_authority = { " + string.Concat(Enumerable.Repeat("always = yes ", 50001)) + " }", StringComparison.Ordinal));
        await Fail(root, () => importer.ImportShelterClosureAsync(root));
        await Write(root, biogenesis, bodies[biogenesis]);
        var changing = new ConstructionDefinitionImporter(async _ => await Write(root, biogenesis, bodies[biogenesis] + "\n# changed"));
        await Fail(root, () => changing.ImportShelterClosureAsync(root));
        await Write(root, biogenesis, bodies[biogenesis]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Cancelled(() => importer.ImportShelterClosureAsync(root, cancellation.Token));
        foreach (var invalid in new[] { "[[!SPECIES", "[[!]", "[[bad-name]", "[[" + new string('x', 129) + "]", "bare ! flag" })
        {
            var rejected = false;
            try { _ = Stellaris.Core.Scripting.ParadoxParser.Parse("check = { " + invalid + " }"); }
            catch (FormatException) { rejected = true; }
            Assert(rejected, "Malformed conditional header/operator was accepted.");
        }
        var parsed = Stellaris.Core.Scripting.ParadoxParser.Parse("check = { [[!SPECIES] value = 1 ] [[SPECIES] value = 2 ] quoted = \"[[!not_a_header\" math = @[1 + 2] }");
        Assert(parsed.Entries.Single().Block!.Entries.Count(entry => entry.Key.StartsWith("[[", StringComparison.Ordinal)) == 2 && parsed.Entries.Single().Block!.Entries.Single(entry => entry.Key == "math").Scalar == "@[1 + 2]", "Conditional lexical retention changed quoted strings/inline math.");
    });

    private static IEnumerable<ConstructionField> Flatten(IEnumerable<ConstructionField> fields) => fields.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));
    private static async Task Fixture(Func<string, Task> run)
    {
        var root = Path.Combine(Path.GetTempPath(), "stellaris-construction-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await Write(root, "launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}");
            await Write(root, _globals[0], "@time = 360\n@cost = 400\n@capacity = 200");
            await Write(root, _globals[1], "@unused = 7");
            await Write(root, _globals[2], "@ratio = .1");
            await Write(root, BUILDINGS, Definition());
            await Write(root, "common/inline_scripts/checks/jobs.txt", "triggered_planet_modifier = { job_farmer_add = $AMOUNT$ }");
            await Write(root, "common/inline_scripts/checks/cost.txt", "cost = { trigger = { from? = { is_nomadic = no } } minerals = $COST$ } cost = { trigger = { from? = { is_nomadic = yes } } volatile_motes = $COST$ mult = $MULT$ }");
            await run(root);
        }
        finally
        {
            Assert(Path.GetDirectoryName(root) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) && Path.GetFileName(root).StartsWith("stellaris-construction-check-", StringComparison.Ordinal), "Unsafe fixture cleanup.");
            NoReparse(root);
            Directory.Delete(root, true);
        }
    }
    private static void NoReparse(string directory)
    {
        Assert((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Reparse cleanup refused.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            Assert((attributes & FileAttributes.ReparsePoint) == 0, "Unknown reparse cleanup refused.");
            if ((attributes & FileAttributes.Directory) != 0) { NoReparse(entry); }
        }
    }
    private static async Task Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) { throw new Exception(message); }
    }
    private static string Definition() => """
building_hydroponics_farm = {
 base_buildtime = @time
 category = resource
 building_sets = { hydroponics }
 prerequisites = { "tech_hydroponics" }
 potential = { exists = owner from? = { is_nomadic = no } value < 2 }
 inline_script = { script = checks/jobs AMOUNT = @capacity }
 resources = {
  category = planet_buildings
  inline_script = { script = checks/cost COST = @cost MULT = @ratio }
  upkeep = { energy = 2 }
 }
}
""";
}

sealed class SkipException(string message) : Exception(message);
