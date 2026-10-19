using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;

static class ShelterClosureChecks
{
    private static readonly ConstructionOrigin _origin = new("checks/shelter-closure", 1, 1);
    private static ConstructionField F(string key, string? value = null, string operation = "=", params ConstructionField[] children) => new(key, operation, value, children, _origin, []);
    private static ConstructionField B(string key, params ConstructionField[] children) => F(key, children: children);

    private static ConstructionCatalogProjection Fixture()
    {
        var definitions = ConstructionShelterClosureInventory.Create().SelectMany(item => item.Ids.Select(id => new ConstructionPlacementDefinition(id, item.Kind, new(item.Path, 1, 1), [F("always", "no")]))).ToArray();
        ConstructionCatalogProjection Source() => new(ConstructionShelterClosureInventory.PROFILE, definitions, [], [], []);
        var source = Source();
        source = Replace(source, "is_purity_authority", [F("has_country_flag", "bio_purity")]);
        source = Replace(source, "is_gestalt", [F("has_ethic", "ethic_gestalt_consciousness")]);
        source = Replace(source, "is_individual_machine", [B("founder_species", F("is_archetype", "MACHINE")), F("is_gestalt", "no")]);
        source = Replace(source, "is_worker_coop_empire", [F("has_valid_civic", "civic_worker_coop")]);
        source = Replace(source, "has_zone", [B("num_zones", F("type", "$zone$"), F("value", "0", ">"))]);
        return Replace(source, "has_knights_zone", [B("OR", B("has_zone", F("zone", "zone_habitat_knights")), B("has_zone", F("zone", "zone_ark_knights")))]);
    }

    private static ConstructionCatalogProjection Replace(ConstructionCatalogProjection source, string id, ConstructionField[] fields) => new(source.ProfileId, source.Definitions.Values.Select(definition => definition.Id == id ? new(definition.Id, definition.Kind, definition.Origin, fields) : definition), source.Parameters, source.Files, source.Coverage);
    private static ConstructionContext Context(ConstructionShelterFacts? facts = null, IEnumerable<string>? flags = null, IEnumerable<string>? selected = null, IEnumerable<string>? valid = null, IEnumerable<string>? active = null, bool? ownerExists = true) => new(ownerExists: ownerExists, sourceFacts: new(ownerFlags: flags, civics: selected, validCivics: valid, traditions: active, shelter: facts));
    private static ConstructionCondition Evaluate(ConstructionCatalogProjection source, ConstructionContext context, ConstructionField field, ConstructionAssessor.Scope scope = ConstructionAssessor.Scope.Planet) => new ConstructionAssessor.Evaluator(context, source).Condition([field], scope);

    public static Task Facts()
    {
        var source = Fixture();
        Check(source.Definitions.Count == 41, "Whole closure inventory count changed.");
        var mutable = new List<string> { "held" };
        var facts = new ConstructionShelterFacts(ownerOrigin: "origin_default", ownerEthics: [], heldTraditions: mutable, ownerEdicts: [], ownerPolicyFlags: [], ownerModifiers: [], planetFlags: [], zones: ["zone_ark_knights"], hostDlc: [], carrierType: "planet", founderSpeciesExists: true, founderSpeciesArchetype: "MACHINE", capitalTier: 2);
        mutable.Clear();
        Check(facts.HeldTraditions!.Contains("held"), "Runtime fact collection retained mutable caller storage.");
        foreach (var invalid in new Action[] { () => _ = new ConstructionShelterFacts(capitalTier: -1), () => _ = new ConstructionShelterFacts(capitalTier: 4097), () => _ = new ConstructionShelterFacts(carrierType: "unknown"), () => _ = new ConstructionShelterFacts(ownerOrigin: ""), () => _ = new ConstructionShelterFacts(zones: Enumerable.Range(0, 257).Select(index => "zone_" + index)) })
        {
            var rejected = false;
            try { invalid(); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid realized runtime facts bypassed bounds.");
        }
        var context = Context(facts, flags: ["bio_purity"], selected: ["selected"], valid: [], active: ["active"]);
        Check(Evaluate(source, context, B("owner", F("is_purity_authority", "yes"))).Truth == ConstructionTruth.True, "Imported flag authority helper was replaced by authority matching.");
        Check(Evaluate(source, context, B("owner", F("is_individual_machine", "yes"))).Truth == ConstructionTruth.True, "Imported founder/archetype/ethic scope failed.");
        Check(Evaluate(source, Context(new(founderSpeciesExists: true, founderSpeciesArchetype: "HUMANOID", ownerEthics: [])), B("owner", F("is_individual_machine", "yes"))).Truth == ConstructionTruth.False, "Organic founder was inferred machine or robotic.");
        foreach (var exists in new bool?[] { null, false })
        {
            Check(Evaluate(source, Context(new(founderSpeciesExists: exists, founderSpeciesArchetype: "MACHINE", ownerEthics: [])), B("owner", F("is_individual_machine", "yes"))).Truth == ConstructionTruth.Unknown, "Missing founder target consumed provided archetype.");
            Check(Evaluate(source, Context(facts, ownerExists: exists), B("owner?", F("always", "yes"))).Truth == ConstructionTruth.Unknown, "Missing optional owner truth was invented.");
        }
        foreach (var (key, value, expected) in new[] { ("has_tradition", "held", ConstructionTruth.True), ("has_tradition", "active", ConstructionTruth.False), ("has_active_tradition", "active", ConstructionTruth.True), ("has_civic", "selected", ConstructionTruth.True), ("has_valid_civic", "selected", ConstructionTruth.False), ("has_origin", "origin_default", ConstructionTruth.True), ("has_edict", "absent", ConstructionTruth.False), ("has_policy_flag", "absent", ConstructionTruth.False), ("has_modifier", "absent", ConstructionTruth.False), ("has_authority", "auth_imperial", ConstructionTruth.Unknown) })
        {
            Check(Evaluate(source, context, B("owner", F(key, value))).Truth == expected, "Literal runtime fact distinction failed: " + key);
        }
        Check(Evaluate(source, context, B("owner?", F("has_tradition", "held"))).Truth == ConstructionTruth.True, "Explicit existing optional owner failed.");
        Check(Evaluate(source, context, F("has_knights_zone", "yes")).Truth == ConstructionTruth.True && Evaluate(source, Context(new(zones: [])), F("has_knights_zone", "yes")).Truth == ConstructionTruth.False && Evaluate(source, Context(), F("has_knights_zone", "yes")).Truth == ConstructionTruth.Unknown, "Parameterized original zone helper lost known/empty/unknown distinction.");
        var previous = B("prev", F("capital_tier", "2", ">="));
        Check(Evaluate(source, context, B("owner", B("AND", previous))).Truth == ConstructionTruth.True && Evaluate(source, context, previous, ConstructionAssessor.Scope.Owner).Truth == ConstructionTruth.Unknown, "PREV fabricated a prior scope or lost actual owner frame.");
        Check(Evaluate(source, Context(), F("capital_tier", "2", ">=")).Truth == ConstructionTruth.Unknown && Evaluate(source, context, F("capital_tier", "3", ">=")).Truth == ConstructionTruth.False, "Missing tier defaulted or numeric comparison failed.");
        foreach (var bad in new[] { F("is_purity_authority", "yes", children: [F("always", "yes")]), F("is_purity_authority", "invalid"), F("is_purity_authority", "yes", "!="), F("has_origin", "$ORIGIN$"), F("always", "yes", children: [F("always", "yes")]) })
        {
            Check(Evaluate(source, context, B("owner", bad)).Truth == ConstructionTruth.Unknown, "Malformed closure condition became known.");
        }
        Check(Evaluate(Replace(source, "is_purity_authority", [F("always", "no")]), context, B("owner", F("is_purity_authority", "yes"))).Truth == ConstructionTruth.False, "Changed supported helper body was ignored.");
        Check(Evaluate(Replace(source, "is_purity_authority", [F("is_purity_authority", "yes")]), context, B("owner", F("is_purity_authority", "yes"))).Truth == ConstructionTruth.Unknown, "Recursive imported helper escaped traversal budget.");
        foreach (var id in new[] { "is_robotic_species", "is_specialist_category", "count_specialists", "set_species_traits_evopred_count_variable" })
        {
            Check(Evaluate(source, context, F(id, "yes")).Truth == ConstructionTruth.Unknown && Evaluate(source, context, B("owner", F(id, "yes"))).Truth == ConstructionTruth.Unknown, "Wrong-scope/lifecycle definition executed as colony/owner predicate.");
        }
        foreach (var invalid in new[] { new ConstructionCatalogProjection(source.ProfileId, source.Definitions.Values.Skip(1), [], [], []), new ConstructionCatalogProjection(source.ProfileId, source.Definitions.Values.Select(definition => definition.Id == "is_purity_authority" ? new(definition.Id, "planetCondition", definition.Origin, definition.Fields) : definition), [], [], []) })
        {
            var rejected = false;
            try { _ = new ConstructionAssessor.Evaluator(context, invalid); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Malformed whole closure inventory accepted.");
        }
        return Task.CompletedTask;
    }

    public static Task Systems()
    {
        var source = Fixture();
        var flags = new List<string> { "storm_system" };
        var facts = new ConstructionShelterFacts(systemExists: true, systemStarFlags: flags);
        flags.Clear();
        var storm = B("solar_system?", F("has_star_flag", "storm_system"));
        Check(facts.SystemStarFlags!.Contains("storm_system") && Evaluate(source, Context(facts), storm).Truth == ConstructionTruth.True, "System flags retained caller mutation or failed original membership.");
        Check(Evaluate(source, Context(facts), F("exists", "solar_system")).Truth == ConstructionTruth.True && Evaluate(source, Context(new(systemExists: false)), F("exists", "solar_system")).Truth == ConstructionTruth.False && Evaluate(source, Context(), F("exists", "solar_system")).Truth == ConstructionTruth.Unknown, "Explicit system existence lost true/false/unknown distinction.");
        Check(Evaluate(source, Context(new(systemExists: true, systemStarFlags: [])), storm).Truth == ConstructionTruth.False && Evaluate(source, Context(new(systemExists: true)), storm).Truth == ConstructionTruth.Unknown, "Empty or unknown system flags were defaulted.");
        foreach (var exists in new bool?[] { null, false })
        {
            foreach (var key in new[] { "solar_system", "solar_system?" })
            {
                Check(Evaluate(source, Context(new(systemExists: exists, systemStarFlags: ["storm_system"])), B(key, F("has_star_flag", "storm_system"))).Truth == ConstructionTruth.Unknown, "Missing system target consumed provided flags or guessed optional truth.");
            }
        }
        Check(Evaluate(source, Context(facts), B("solar_system", B("NOT", F("has_star_flag", "absent")))).Truth == ConstructionTruth.True && Evaluate(source, Context(facts), B("solar_system?", B("OR", F("has_star_flag", "absent"), F("has_star_flag", "storm_system")))).Truth == ConstructionTruth.True, "Present system boolean combinations failed.");
        foreach (var bad in new[] { F("has_star_flag", "storm_system"), B("owner", storm), B("solar_system?", B("owner", F("always", "yes"))), B("solar_system?", B("prev", F("always", "yes"))), F("solar_system?", "yes"), F("solar_system?", operation: "!=", children: [F("has_star_flag", "storm_system")]), B("solar_system?", F("has_star_flag", "$FLAG$")), B("solar_system?", F("has_star_flag", "storm_system", children: [F("always", "yes")])) })
        {
            Check(Evaluate(source, Context(facts), bad).Truth == ConstructionTruth.Unknown, "Wrong-scope or malformed system condition became known.");
        }
        var legacy = new ConstructionCatalogProjection("construction-conditions-catalog-452-v1", source.Definitions.Values.Where(item => new[] { "planet_is_habitat_equivalent", "planet_is_ecu_equivalent", "planet_is_ring_world_equivalent", "is_special_colony_type" }.Contains(item.Id)), [], [], []);
        Check(Evaluate(legacy, Context(facts), storm).Truth == ConstructionTruth.Unknown && Evaluate(legacy, Context(facts), F("exists", "solar_system")).Truth == ConstructionTruth.Unknown, "Legacy profile gained system scope support.");
        var rejected = false;
        try { _ = new ConstructionShelterFacts(systemStarFlags: Enumerable.Range(0, 257).Select(index => "star_" + index)); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "System flag collection escaped existing fact bounds.");
        return Task.CompletedTask;
    }

    public static Task CapitalIdentities()
    {
        var source = Fixture();
        var system = B("owner", B("capital_scope.solar_system?", F("is_same_value", "ROOT.solar_system")));
        var colony = B("owner", B("capital_scope", F("is_same_value", "ROOT")));
        ConstructionContext Identities(ConstructionCapitalLinks? links, bool? systemExists = true, bool? ownerExists = true) => Context(new(systemExists: systemExists, capitalLinks: links), ownerExists: ownerExists);
        var same = new ConstructionCapitalLinks("colony_a", "system_a", "colony_a", "system_a");
        var otherColony = new ConstructionCapitalLinks("colony_a", "system_a", "colony_b", "system_a");
        var otherSystem = new ConstructionCapitalLinks("colony_a", "system_a", "colony_b", "system_b");
        Check(Evaluate(source, Identities(same), system).Truth == ConstructionTruth.True && Evaluate(source, Identities(otherColony), system).Truth == ConstructionTruth.True && Evaluate(source, Identities(otherSystem), system).Truth == ConstructionTruth.False, "Capital-system comparison confused colony versus system identity.");
        Check(Evaluate(source, Identities(same), colony).Truth == ConstructionTruth.True && Evaluate(source, Identities(otherColony), colony).Truth == ConstructionTruth.False && Evaluate(source, Identities(otherSystem), B("owner", B("capital_scope", B("NOT", F("is_same_value", "ROOT"))))).Truth == ConstructionTruth.True, "Capital colony ROOT or NOT comparison failed.");
        Check(Evaluate(source, Identities(new("colony_a", "system_a", "COLONY_A", "SYSTEM_A")), colony).Truth == ConstructionTruth.False, "Identity comparison ignored case.");
        foreach (var links in new ConstructionCapitalLinks?[] { null, new(), new("colony_a", "system_a"), new("colony_a", "system_a", ownerCapitalSystemId: "system_a"), new("colony_a", "system_a", "colony_b"), new(ownerCapitalColonyId: "colony_b", ownerCapitalSystemId: "system_a") })
        {
            Check(Evaluate(source, Identities(links), system).Truth == ConstructionTruth.Unknown, "Missing capital-system link inferred an identity or optional truth.");
        }
        foreach (var exists in new bool?[] { null, false })
        {
            Check(Evaluate(source, Identities(same, systemExists: exists), system).Truth == ConstructionTruth.Unknown && Evaluate(source, Identities(same, ownerExists: exists), colony).Truth == ConstructionTruth.Unknown, "Provided identities overrode missing root-system or owner existence.");
        }
        foreach (var bad in new[] { B("capital_scope", F("is_same_value", "ROOT")), B("solar_system?", F("is_same_value", "ROOT.solar_system")), B("owner", B("capital_scope", F("is_same_value", "ROOT.solar_system"))), B("owner", B("capital_scope.solar_system?", F("is_same_value", "ROOT"))), B("owner", B("capital_scope", B("prev", F("always", "yes")))), B("owner", B("capital_scope", B("owner", F("always", "yes")))), B("owner", B("capital_scope.solar_system?", F("has_star_flag", "storm_system"))), B("owner", B("capital_scope?", F("is_same_value", "ROOT"))), B("owner", F("capital_scope", "ROOT")), B("owner", F("capital_scope", operation: "!=", children: [F("is_same_value", "ROOT")])), B("owner", B("capital_scope", F("is_same_value", "ROOT", children: [F("always", "yes")]))), B("owner", B("capital_scope", F("is_same_value", "PREV"))) })
        {
            Check(Evaluate(source, Identities(same), bad).Truth == ConstructionTruth.Unknown, "Malformed/wrong capital path, operand or scope leaked root facts.");
        }
        foreach (var invalid in new Action[] { () => _ = new ConstructionCapitalLinks(""), () => _ = new ConstructionCapitalLinks(systemId: new string('a', 161)), () => _ = new ConstructionCapitalLinks(ownerCapitalColonyId: "bad\n"), () => _ = new ConstructionCapitalLinks("colony_a", "system_a", "colony_a", "system_b") })
        {
            var rejected = false;
            try { invalid(); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Malformed or contradictory actual identity links accepted.");
        }
        foreach (var exists in new bool?[] { null, false, true })
        {
            Check(Evaluate(source, Context(new(leaderExists: exists)), F("exists", "leader")).Truth == (exists is null ? ConstructionTruth.Unknown : exists.Value ? ConstructionTruth.True : ConstructionTruth.False), "Leader existence lost nullable distinction.");
        }
        var legacy = new ConstructionCatalogProjection("construction-conditions-catalog-452-v1", source.Definitions.Values.Where(item => new[] { "planet_is_habitat_equivalent", "planet_is_ecu_equivalent", "planet_is_ring_world_equivalent", "is_special_colony_type" }.Contains(item.Id)), [], [], []);
        Check(Evaluate(legacy, Identities(same), system).Truth == ConstructionTruth.Unknown && Evaluate(legacy, Context(new(leaderExists: true)), F("exists", "leader")).Truth == ConstructionTruth.Unknown, "Legacy profile gained capital/leader traversal.");
        return Task.CompletedTask;
    }

    public static Task GrowingSpecies()
    {
        var source = Fixture();
        var traits = new List<string> { "trait_cybernetic" };
        var links = new ConstructionGrowingSpeciesLinks(true, "growing_target", "actual_species", traits);
        traits.Clear();
        var chain = B("growing_species", B("species", F("has_trait", "trait_cybernetic")));
        Check(links.SpeciesTraits!.Contains("trait_cybernetic") && Evaluate(source, Context(new(growingSpecies: links)), chain).Truth == ConstructionTruth.True, "Growing species traits retained caller mutation or skipped actual linked membership.");
        Check(Evaluate(source, Context(new(growingSpecies: new(true, "target", "species", []))), chain).Truth == ConstructionTruth.False && Evaluate(source, Context(new(growingSpecies: new(true, "target", "species"))), chain).Truth == ConstructionTruth.Unknown, "Empty or unknown growing species traits were defaulted.");
        foreach (var missing in new ConstructionGrowingSpeciesLinks?[] { null, new(), new(null, "target", "species", ["trait_cybernetic"]), new(false, "target", "species", ["trait_cybernetic"]), new(true, speciesId: "species", speciesTraits: ["trait_cybernetic"]), new(true, "target", speciesTraits: ["trait_cybernetic"]) })
        {
            Check(Evaluate(source, Context(new(founderSpeciesExists: true, founderSpeciesArchetype: "MACHINE", growingSpecies: missing)), chain).Truth == ConstructionTruth.Unknown, "Missing growing chain inferred identity, trait or founder fallback.");
        }
        foreach (var exists in new bool?[] { null, false, true })
        {
            Check(Evaluate(source, Context(new(growingSpecies: new(exists))), F("exists", "growing_species")).Truth == (exists is null ? ConstructionTruth.Unknown : exists.Value ? ConstructionTruth.True : ConstructionTruth.False), "Growing target existence lost nullable distinction.");
        }
        foreach (var bad in new[] { F("has_trait", "trait_cybernetic"), B("species", F("has_trait", "trait_cybernetic")), B("growing_species", F("has_trait", "trait_cybernetic")), B("owner", chain), B("growing_species?", B("species", F("has_trait", "trait_cybernetic"))), B("growing_species", B("species", B("prev", F("always", "yes")))), B("growing_species", B("species", B("owner", F("always", "yes")))), B("growing_species", B("species", F("is_robotic", "yes"))), F("growing_species", "yes"), B("growing_species", F("species", "yes")), B("growing_species", B("species", F("has_trait", "$TRAIT$"))), B("growing_species", B("species", F("has_trait", "trait_cybernetic", children: [F("always", "yes")]))) })
        {
            Check(Evaluate(source, Context(new(growingSpecies: links)), bad).Truth == ConstructionTruth.Unknown, "Growing chain accepted malformed operands or leaked unrelated scopes.");
        }
        foreach (var invalid in new Action[] { () => _ = new ConstructionGrowingSpeciesLinks(true, ""), () => _ = new ConstructionGrowingSpeciesLinks(speciesId: new string('s', 161)), () => _ = new ConstructionGrowingSpeciesLinks(speciesTraits: Enumerable.Range(0, 257).Select(index => "trait_" + index)) })
        {
            var rejected = false;
            try { invalid(); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Growing species link or trait collection escaped bounds.");
        }
        var legacy = new ConstructionCatalogProjection("construction-conditions-catalog-452-v1", source.Definitions.Values.Where(item => new[] { "planet_is_habitat_equivalent", "planet_is_ecu_equivalent", "planet_is_ring_world_equivalent", "is_special_colony_type" }.Contains(item.Id)), [], [], []);
        Check(Evaluate(legacy, Context(new(growingSpecies: links)), chain).Truth == ConstructionTruth.Unknown, "Legacy profile gained growing species traversal.");
        return Task.CompletedTask;
    }

    public static Task Values()
    {
        var resolutionIds = new[] { "resolution_a", "resolution_b", "resolution_c", "resolution_d" };
        var body = new[] { F("base", "0"), B("modifier", F("add", "0.05"), F("is_active_resolution", resolutionIds[0])), B("modifier", F("add", "0.1"), F("is_active_resolution", resolutionIds[1])), B("modifier", F("add", "0.1"), F("is_active_resolution", resolutionIds[2])), B("modifier", F("add", "0.15"), F("is_active_resolution", resolutionIds[3])) };
        ConstructionCatalogProjection Source(ConstructionField[] fields)
        {
            const string ID = "storm_building_production_multiplier";
            var projection = Replace(Fixture(), ID, fields);
            var numbers = new List<ConstructionCatalogParameter>();
            Visit(fields, "");
            return new(projection.ProfileId, projection.Definitions.Values, numbers, [], []);
            void Visit(IReadOnlyList<ConstructionField> items, string parent)
            {
                foreach (var (field, index) in items.Select((field, index) => (field, index)))
                {
                    var path = parent + "/" + index + ":" + field.Key;
                    if (decimal.TryParse(field.Scalar, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var number)) { numbers.Add(new(ID, path, field.Operator, number, field.Origin)); }
                    Visit(field.Children, path);
                }
            }
        }
        decimal? Value(ConstructionCatalogProjection source, ConstructionShelterFacts facts, ConstructionField? operand = null)
        {
            var gates = new List<ConstructionGate>();
            var value = new ConstructionAssessor.Evaluator(Context(facts), source).Value(operand ?? F("mult", "value:storm_building_production_multiplier"), gates);
            Check(value is null ? gates.Count != 0 : gates.Count == 0, "Script value lost refusal gate or leaked partial result.");
            return value;
        }
        var source = Source(body);
        var mutable = resolutionIds.ToList();
        var all = new ConstructionShelterFacts(activeResolutions: mutable);
        mutable.Clear();
        Check(Value(source, all) == 0.4m && Value(source, new(activeResolutions: [])) == 0 && Value(source, new(activeResolutions: [resolutionIds[1], resolutionIds[3]])) == 0.25m && Value(source, new()) is null, "Resolution state defaulted, mutated or selected highest-only.");
        Check(Value(Source([F("base", "1"), B("modifier", F("add", "0.3"), F("always", "yes"))]), new()) == 1.3m, "Changed supported value body was ignored.");
        Check(Value(Source([F("base", "0"), B("modifier", F("add", "0.3"), F("always", "no"))]), new()) == 0, "Changed false condition still contributed.");
        var membership = Source([F("base", "0"), B("modifier", F("add", "0.3"), B("owner", F("is_galactic_community_member", "yes")))]);
        Check(Value(membership, new(ownerGalacticCommunityMember: true)) == 0.3m && Value(membership, new(ownerGalacticCommunityMember: false)) == 0 && Value(membership, new()) is null, "Community membership invented an absent-state default.");
        foreach (var bad in new[] { Array.Empty<ConstructionField>(), new[] { B("modifier", F("add", "1"), F("always", "yes")) }, new[] { F("base", "0"), F("base", "1") }, new[] { F("base", "bad") }, new[] { F("base", "0"), B("modifier", F("add", "1")) }, new[] { F("base", "0"), B("modifier", F("add", "1"), F("add", "2"), F("always", "yes")) }, new[] { F("base", "0"), F("mult", "2") }, new[] { F("base", "0"), B("modifier", F("add", "1"), F("unknown_condition", "yes")) }, new[] { F("base", decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)), B("modifier", F("add", "1"), F("always", "yes")) }, Enumerable.Repeat(F("base", "0"), 257).ToArray() })
        {
            Check(Value(Source(bad), all) is null, "Malformed, unknown or overflowed script value produced a total.");
        }
        foreach (var bad in new[] { F("mult", "value:storm_building_production_multiplier", "!="), F("mult", "value:storm_building_production_multiplier", children: [F("always", "yes")]), F("mult", "value:count_specialists") })
        {
            Check(Value(source, all, bad) is null, "Wrong operand shape or unsupported value resolved.");
        }
        var mismatched = new ConstructionCatalogProjection(source.ProfileId, source.Definitions.Values, source.Parameters.Select(parameter => parameter.FieldPath == "/0:base" ? parameter with { Value = 1 } : parameter), [], []);
        Check(Value(mismatched, all) is null, "Numeric parameter mismatch accepted.");
        var legacy = new ConstructionCatalogProjection("construction-conditions-catalog-452-v1", source.Definitions.Values.Where(item => new[] { "planet_is_habitat_equivalent", "planet_is_ecu_equivalent", "planet_is_ring_world_equivalent", "is_special_colony_type" }.Contains(item.Id)), [], [], []);
        Check(Value(legacy, all) is null, "Legacy condition profile gained dynamic execution.");
        return Task.CompletedTask;
    }

    public static void Real(ConstructionCatalogProjection building, ConstructionCatalogProjection closure, ConstructionEconomicCategoryProjection categories)
    {
        ConstructionShelterClosureInventory.Validate(closure);
        var facts = new ConstructionShelterFacts(ownerOrigin: "origin_default", ownerEthics: [], heldTraditions: [], ownerEdicts: [], ownerPolicyFlags: [], ownerModifiers: [], planetFlags: [], zones: [], hostDlc: [], carrierType: "planet", founderSpeciesExists: true, founderSpeciesArchetype: "HUMANOID", capitalTier: 1);
        var context = new ConstructionContext(ownerExists: true, planetClass: "pc_continental", modifiers: [], technologies: [], sourceFacts: new(districtSet: "standard", ownerCountryType: "default", civics: [], validCivics: [], ownerFlags: [], traditions: [], deposits: [], planetFacts: new Dictionary<ConstructionPlanetPredicate, bool?> { [ConstructionPlanetPredicate.Capital] = false }, shelter: facts));
        var assessment = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), context, closure);
        Check(assessment.Potential.Truth == ConstructionTruth.True && ReferenceEquals(assessment.ConditionSource, closure) && ConstructionResourceCategoryBinding.Bind(assessment, categories).BindingGates.Count == 0, "Real complete closure failed explicit potential/source binding.");
        var expectedRoots = new[] { 3, 24, 36, 47, 57, 70, 85, 100, 112, 122, 137, 150, 164, 175, 196, 211, 220, 228, 242, 255, 264, 273, 284, 296, 307, 321, 330, 350, 370, 389, 402, 416, 430, 444, 458, 472, 487, 503, 519 };
        var inline = assessment.Contributions.Where(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt").ToArray();
        Check(inline.Select(item => item.Origin.Line).SequenceEqual(expectedRoots), "Real shared-capital root inventory was omitted, duplicated or reordered.");
        Check(inline.Single(item => item.Origin.Line == 3).Condition.Truth == ConstructionTruth.Unknown, "Unresolved real authority/system scope was guessed inactive or active.");
        foreach (var line in expectedRoots.Where(line => line is not (3 or 24 or 519)))
        {
            var branch = inline.Single(item => item.Origin.Line == line);
            Check(branch.Condition.Truth == ConstructionTruth.False, "Real reached branch not proven inactive with explicit facts: " + line);
        }
        Check(assessment.Contributions.Any(item => item.Origin.Line == 24 && item.Condition.Truth == ConstructionTruth.True && item.BlockingGates.Any(gate => gate.DependencyId == "catalog-number-unresolved")), "Real active soldier operand was defaulted or lost.");
        Check(assessment.Contributions.Any(item => item.Kind == "triggered_planet_pop_group_modifier_for_species" && item.Condition.Truth == ConstructionTruth.Unknown), "Missing actual group target defaulted.");
        foreach (var tier in new[] { 1, 2, 3, 4 })
        {
            var productivityContext = new ConstructionContext(ownerExists: true, technologies: ["tech_capital_productivity_1", "tech_capital_productivity_2", "tech_capital_productivity_3"], sourceFacts: new(shelter: new ConstructionShelterFacts(capitalTier: tier)));
            var productivity = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), productivityContext, closure).Contributions.Where(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt" && item.Origin.Line is 70 or 85 or 100).ToArray();
            var activeProductivity = productivity.Where(item => item.Condition.Truth == ConstructionTruth.True).ToArray();
            var expected = tier == 1 ? 0 : tier == 2 ? .05m : tier == 3 ? .1m : .15m;
            Check(productivity.Length == 3 && productivity.All(item => item.Condition.Truth != ConstructionTruth.Unknown && item.Gates.Count == 0) && activeProductivity.Length == (tier == 1 ? 0 : 1) && activeProductivity.Sum(item => item.Amounts.Single(amount => amount.Resource == "pop_bonus_workforce_mult").Value) == expected, "Real productivity source amounts ignored actual tier/PREV or stacked highest-tech branches.");
        }
        var resolutions = new[] { "resolution_cosmic_storms_protection_initiative", "resolution_cosmic_storms_galactic_management", "resolution_cosmic_storms_utilization_protocols", "resolution_cosmic_storms_manipulation_mandate" };
        foreach (var ownerModifiers in new string[]?[] { null, [], ["cyber_implant_taxes"] })
        {
            foreach (var (links, expectedTrait) in new (ConstructionGrowingSpeciesLinks?, ConstructionTruth)[] { (null, ConstructionTruth.Unknown), (new(false), ConstructionTruth.False), (new(true, "target", "species", ["trait_cybernetic"]), ConstructionTruth.True), (new(true, "target", "species", []), ConstructionTruth.False), (new(true, "target", "species"), ConstructionTruth.Unknown), (new(true, "target"), ConstructionTruth.Unknown), (new(true, speciesId: "species", speciesTraits: ["trait_cybernetic"]), ConstructionTruth.Unknown) })
            {
                var growingContext = new ConstructionContext(ownerExists: true, sourceFacts: new(shelter: new(ownerModifiers: ownerModifiers, growingSpecies: links)));
                var growingAssessment = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), growingContext, closure);
                var augmentor = growingAssessment.Contributions.Single(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt" && item.Origin.Line == 137);
                var expected = ownerModifiers is { Length: 0 } || expectedTrait == ConstructionTruth.False ? ConstructionTruth.False : ownerModifiers is null ? ConstructionTruth.Unknown : expectedTrait;
                Check(augmentor.Condition.Truth == expected && augmentor.Amounts.Single().Resource == "planet_augmentors_upkeep_mult" && augmentor.Amounts.Single().Value == .1m && ConstructionJobEconomyMapping.Find(augmentor.Amounts.Single().Resource) is { Operation: "upkeep" } && (augmentor.BlockingGates.Count != 0) == (expected == ConstructionTruth.Unknown), "Real cyber implant upkeep ignored actual growing-species chain, owner modifier or typed amount.");
                Check(growingAssessment.Contributions.Any(item => item.BlockingGates.Count != 0), "Growing species traversal bypassed unrelated shelter gates.");
            }
        }
        if (assessment.DefinitionId == "building_colony_shelter") { SourceCategoryEconomyChecks.GrowingSpecies(closure); }
        foreach (var links in new ConstructionCapitalLinks?[] { null, new("colony_a", "system_a", "colony_a", "system_a"), new("colony_a", "system_a", "colony_b", "system_a"), new("colony_a", "system_a", "colony_b", "system_b"), new("colony_a", "system_a", "colony_b") })
        {
            var capitalContext = new ConstructionContext(ownerExists: true, sourceFacts: new(ownerFlags: [], shelter: new(systemExists: true, capitalLinks: links)));
            var capitalAssessment = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), capitalContext, closure);
            var capitalOutput = capitalAssessment.Contributions.Single(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt" && item.Origin.Line == 3);
            var expectedOutput = links?.OwnerCapitalColonyId is not null && links.OwnerCapitalSystemId is not null && links.SystemId != links.OwnerCapitalSystemId ? ConstructionTruth.False : ConstructionTruth.Unknown;
            Check(capitalOutput.Condition.Truth == expectedOutput && capitalOutput.Amounts.Single().Value == .1m && capitalOutput.Amounts.Single().Resource == "planet_jobs_produces_mult", "Real capital-system output ignored exact identities or defaulted native authority.");
            var cyberContext = new ConstructionContext(ownerExists: true, sourceFacts: new(ownerFlags: ["cyber_collectivist"], shelter: new(ownerEdicts: ["neurocasted_spectacles"], capitalLinks: links)));
            var cyberAssessment = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), cyberContext, closure);
            var capitalHappiness = cyberAssessment.Contributions.Single(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt" && item.Origin.Line == 196);
            Check(capitalHappiness.Condition.Truth == (links?.ColonyId is not null && links.ColonyId == links.OwnerCapitalColonyId ? ConstructionTruth.False : ConstructionTruth.Unknown), "Real capital ROOT comparison lost native authority gate or negation.");
            Check(capitalAssessment.Contributions.Any(item => item.BlockingGates.Count != 0) && cyberAssessment.Contributions.Any(item => item.BlockingGates.Count != 0), "Capital identity traversal bypassed mandatory shelter refusal.");
        }
        foreach (var leaderExists in new bool?[] { null, false, true })
        {
            var leaderContext = new ConstructionContext(ownerExists: true, sourceFacts: new(shelter: new(ownerEthics: [], heldTraditions: ["tr_aptitude_specialist_training"], leaderExists: leaderExists)));
            var leaderAssessment = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), leaderContext, closure);
            var leaderBranches = leaderAssessment.Contributions.Where(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt" && item.Origin.Line is 444 or 458 or 472).ToArray();
            Check(leaderBranches.Length == 3 && (leaderExists != false || leaderBranches.All(item => item.Condition.Truth == ConstructionTruth.False && item.BlockingGates.Count == 0)), "Explicit absent leader did not retain all original inactive roots.");
            var ordinaryLeader = leaderBranches.Single(item => item.Origin.Line == 444);
            Check(ordinaryLeader.Condition.Truth == (leaderExists is null ? ConstructionTruth.Unknown : leaderExists.Value ? ConstructionTruth.True : ConstructionTruth.False) && ordinaryLeader.RawMultiplier is null && ordinaryLeader.Gates.Any(gate => gate.DependencyId == "catalog-number-unresolved"), "Leader existence inferred numeric skill or lost original operand gate.");
        }
        foreach (var technologies in new[] { Array.Empty<string>(), new[] { "tech_industrial_storm_protection" }, new[] { "tech_advanced_industrial_storm_protection" }, new[] { "tech_industrial_storm_protection", "tech_advanced_industrial_storm_protection" } })
        {
            foreach (var (exists, flags) in new (bool?, string[]?)[] { (true, ["storm_system"]), (true, []), (true, null), (false, ["storm_system"]), (null, ["storm_system"]) })
            {
                var systemContext = new ConstructionContext(ownerExists: true, technologies: technologies, sourceFacts: new(shelter: new(ownerOrigin: "origin_storm_chasers", systemExists: exists, systemStarFlags: flags)));
                var systemAssessment = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), systemContext, closure);
                foreach (var (line, technology) in new[] { (330, "tech_industrial_storm_protection"), (350, "tech_advanced_industrial_storm_protection") })
                {
                    var branch = systemAssessment.Contributions.Single(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt" && item.Origin.Line == line);
                    var expected = !technologies.Contains(technology) ? ConstructionTruth.False : exists != true || flags is null ? ConstructionTruth.Unknown : flags.Contains("storm_system") ? ConstructionTruth.True : ConstructionTruth.False;
                    Check(branch.Condition.Truth == expected && branch.Amounts.Count == 3 && branch.Amounts.All(amount => amount.Value == 1 && ConstructionJobEconomyMapping.Find(amount.Resource) is not null) && (branch.BlockingGates.Count != 0) == (expected == ConstructionTruth.Unknown), "Real independent storm base-add condition or typed amounts failed: " + line);
                }
                Check(systemAssessment.Contributions.Any(item => item.BlockingGates.Count != 0), "Present system bypassed unrelated mandatory shelter effects.");
            }
        }
        foreach (var active in new IEnumerable<string>?[] { null, [], resolutions, resolutions.Take(2).ToArray() })
        {
            var stormFacts = new ConstructionShelterFacts(ownerOrigin: "origin_storm_chasers", activeResolutions: active, ownerGalacticCommunityMember: true);
            var stormContext = new ConstructionContext(ownerExists: true, technologies: ["tech_industrial_storm_protection"], sourceFacts: new(shelter: stormFacts));
            var stormAssessment = ConstructionCatalogAssessor.Assess(building, building.Definitions.Keys.Single(), stormContext, closure);
            var storm = stormAssessment.Contributions.Single(item => item.Origin.Source == "common/inline_scripts/buildings/on_all_capital_buildings.txt" && item.Origin.Line == 370);
            var expected = active is null ? (decimal?)null : active.Count() == 4 ? 0.4m : active.Any() ? 0.15m : 0;
            Check(storm.Condition.Truth == ConstructionTruth.True && storm.RawMultiplier == expected, "Real storm branch failed original independent resolution value.");
            Check(storm.Amounts.Count == 3 && storm.Amounts.All(amount => amount.Value == 1 && ConstructionJobEconomyMapping.Find(amount.Resource) is not null) && (storm.BlockingGates.Count != 0) == (active is null), "Real storm category amounts or unresolved operand gates lost.");
            Check(stormAssessment.Contributions.Any(item => item.BlockingGates.Count != 0 && item.Kind.Contains("modifier", StringComparison.Ordinal)), "Resolved storm category effects suppressed unrelated mandatory shelter gates.");
        }
        Console.WriteLine($"Shelter closure {assessment.DefinitionId}: 41 original definitions; explicit inactive branches resolved; active dynamic and domain gates retained.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
