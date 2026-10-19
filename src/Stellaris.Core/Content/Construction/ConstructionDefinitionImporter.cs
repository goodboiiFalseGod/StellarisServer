using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Construction;

public sealed class ConstructionDefinitionImporter
{
    private readonly Func<CancellationToken, Task>? _beforeVerification;

    public ConstructionDefinitionImporter()
    {
    }

    internal ConstructionDefinitionImporter(Func<CancellationToken, Task> beforeVerification)
    {
        ArgumentNullException.ThrowIfNull(beforeVerification);
        _beforeVerification = beforeVerification;
    }

    public Task<ConstructionProjection> ImportAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportAsync();
    }

    public Task<ConstructionPlacementProjection> ImportPlacementAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportPlacementAsync();
    }

    public Task<ConstructionFeatureProjection> ImportFeaturesAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportFeaturesAsync();
    }

    public Task<ConstructionCatalogProjection> ImportCityCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("city");
    }

    public Task<ConstructionCatalogProjection> ImportRuralCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("rural");
    }

    public Task<ConstructionCatalogProjection> ImportZoneCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("zone");
    }

    public Task<ConstructionCatalogProjection> ImportDistrictCapacityCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("district-capacity");
    }

    public Task<ConstructionCatalogProjection> ImportRuralConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-rural");
    }

    public Task<ConstructionCatalogProjection> ImportCityConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-city");
    }

    public Task<ConstructionCatalogProjection> ImportFederationConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-federation");
    }

    public Task<ConstructionCatalogProjection> ImportDepositConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-deposits");
    }

    public Task<ConstructionCatalogProjection> ImportZoneConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-zones");
    }

    public Task<ConstructionCatalogProjection> ImportCivicConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-civics");
    }

    public Task<ConstructionCatalogProjection> ImportAuthorityConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-authority");
    }

    public Task<ConstructionCatalogProjection> ImportConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions");
    }

    public Task<ConstructionEconomicCategoryProjection> ImportEconomicCategoriesAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportEconomicCategoriesAsync();
    }

    public Task<ConstructionCatalogProjection> ImportOwnerConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-owner");
    }

    public Task<ConstructionCatalogProjection> ImportStartupConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-startup");
    }

    public Task<ConstructionCatalogProjection> ImportCapitalConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-capital");
    }

    public Task<ConstructionCatalogProjection> ImportRareProducerConditionCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("conditions-rare");
    }

    public Task<ConstructionCatalogProjection> ImportShelterClosureAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("shelter-closure");
    }

    public Task<ConstructionCatalogProjection> ImportFunctionalCivicCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("functional-civic");
    }

    public Task<ConstructionCatalogProjection> ImportBuildingCatalogAsync(string gameRoot, string definitionId, CancellationToken cancellationToken = default)
    {
        var path = definitionId switch
        {
            "building_colony_shelter" or "building_capital" => "common/buildings/00_capital_buildings.txt",
            "building_precinct_house" => "common/buildings/02_government_buildings.txt",
            "building_hydroponics_farm" => "common/buildings/03_resource_buildings.txt",
            "building_foundry_1" or "building_factory_1" => "common/buildings/04_manufacturing_buildings.txt",
            "building_research_lab_1" => "common/buildings/05_research_buildings.txt",
            "building_commercial_zone" => "common/buildings/06_trade_buildings.txt",
            "building_holo_theatres" or "building_medical_1" or "building_luxury_residence" or "building_communal_housing_large" => "common/buildings/07_amenity_buildings.txt",
            "building_bureaucratic_1" or "building_temple" => "common/buildings/08_unity_buildings.txt",
            "building_stronghold" => "common/buildings/09_army_buildings.txt",
            _ => throw new ArgumentException("Unsupported ordinary construction building.", nameof(definitionId))
        };
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("building-" + definitionId, path, definitionId);
    }

    public Task<ConstructionCatalogProjection> ImportBuildingCatalogAsync(string gameRoot, string definitionId, string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!definitionId.StartsWith("building_", StringComparison.Ordinal) || definitionId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')
            || !sourcePath.StartsWith("common/buildings/", StringComparison.Ordinal) || sourcePath["common/buildings/".Length..].Contains('/') || !sourcePath.EndsWith(".txt", StringComparison.Ordinal))
        { throw new ArgumentException("Invalid source building identity."); }
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("building-" + definitionId, sourcePath, definitionId);
    }

    /// <summary>Импортирует definition из уже проверенного ordinary inventory, сохраняя проверку snapshot в live content.</summary>
    public Task<ConstructionCatalogProjection> ImportOrdinaryBuildingCatalogAsync(string gameRoot,
        PlanetEconomy.PlanetEconomyDefinition definition, CancellationToken cancellationToken = default)
    {
        if (definition.Kind != PlanetEconomy.PlanetEconomyDefinitionKind.Building
            || !definition.Provenance.RelativePath.StartsWith("common/buildings/", StringComparison.Ordinal)
            || definition.Provenance.RelativePath.Contains("..", StringComparison.Ordinal))
        { throw new ArgumentException("Expected source-backed ordinary building.", nameof(definition)); }
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("ordinary-building-" + definition.Id,
            definition.Provenance.RelativePath, definition.Id);
    }

    /// <summary>Импортирует source-backed ordinary zone и её раскрытое AST.</summary>
    public Task<ConstructionCatalogProjection> ImportOrdinaryZoneCatalogAsync(string gameRoot,
        PlanetEconomy.PlanetEconomyDefinition definition, CancellationToken cancellationToken = default)
    {
        if (definition.Kind != PlanetEconomy.PlanetEconomyDefinitionKind.Zone
            || !definition.Provenance.RelativePath.StartsWith("common/zones/", StringComparison.Ordinal)
            || definition.Provenance.RelativePath.Contains("..", StringComparison.Ordinal))
        { throw new ArgumentException("Expected source-backed ordinary zone.", nameof(definition)); }
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("ordinary-zone-" + definition.Id,
            definition.Provenance.RelativePath, definition.Id, "zone");
    }

    /// <summary>Полный реестр зданий и зон, независимый от поддержки экономики и профессий.</summary>
    public Task<ConstructionCatalogProjection> ImportFullCatalogAsync(string gameRoot, CancellationToken cancellationToken = default)
        => new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportCatalogAsync("full");

    /// <summary>Typed predicates и их самостоятельное source замыкание scripted вызовов.</summary>
    public async Task<ConstructionCatalogProjection> ImportCatalogConditionsAsync(string gameRoot, ConstructionCatalogProjection catalog, CancellationToken cancellationToken = default)
    {
        var typed = await ImportAuthorityConditionCatalogAsync(gameRoot, cancellationToken);
        var closure = await new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportScriptedClosureAsync(catalog);
        var files = typed.Files.Concat(closure.Files).Concat(catalog.Files).GroupBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        if (files.Any(group => group.Select(file => (file.ByteLength, file.Sha256)).Distinct().Count() != 1))
            throw new ArgumentException("Scripted condition source snapshot mismatch.");
        return new(typed.ProfileId, typed.Definitions.Values, typed.Parameters, files.Select(group => group.First()),
            typed.Coverage.Concat(closure.Coverage), closure.Definitions.Values.Where(definition => !typed.Definitions.ContainsKey(definition.Id)));
    }

    private sealed class ImportRun
    {
        private const string BUILDINGS = "common/buildings/03_resource_buildings.txt";
        private const string METADATA = "launcher-settings.json";
        private const int FILE_LIMIT = 2 * 1024 * 1024;
        private const int TOTAL_LIMIT = 8 * 1024 * 1024;
        private static readonly UTF8Encoding _utf8 = new(false, true);
        private static readonly string[] _variableFiles = ["common/scripted_variables/00_scripted_variables.txt", "common/scripted_variables/100_scripted_variables_zones.txt", "common/scripted_variables/10_scripted_variables_nomads.txt"];
        private readonly string _root;
        private readonly CancellationToken _cancellation;
        private readonly Func<CancellationToken, Task>? _beforeVerification;
        private readonly Dictionary<string, Source> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ConstructionFile> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptEntry> _globals = new(StringComparer.Ordinal);
        private readonly List<ConstructionCoverage> _coverage = [];
        private long _totalBytes;
        private int _expandedNodes;
        private int _expandedBytes;
        private int _substitutionBytes;
        private int _fileCountLimit = 32;
        private int _nodeLimit = 50000;
        private int _pathByteLimit = TOTAL_LIMIT;

        public ImportRun(string root, CancellationToken cancellation, Func<CancellationToken, Task>? beforeVerification)
        {
            cancellation.ThrowIfCancellationRequested();
            _cancellation = cancellation;
            _beforeVerification = beforeVerification;
            _root = ResolveRoot(root);
        }

        public async Task<ConstructionProjection> ImportAsync()
        {
            await PrepareAsync();
            var buildings = await LoadAsync(BUILDINGS);
            var candidates = buildings.Ast.Named("building_hydroponics_farm").ToArray();
            if (candidates.Length != 1 || candidates[0].Operator != "=" || candidates[0].Block is null)
            {
                throw Error(BUILDINGS, "Expected one hydroponics block definition.");
            }
            var selected = candidates[0];
            var fields = await ExpandAsync(selected.Block!.Entries, new Dictionary<string, string>(StringComparer.Ordinal), [], [], 1);
            var definition = Compile(selected, fields);
            _coverage.Add(new ConstructionCoverage("profile", "excluded", "Diagnostic profile only: no DLC/mod load order, complete dependency closure or vanilla purity proof.", null));
            _coverage.Add(new ConstructionCoverage("jobs", "unsupported", "Raw capacity and conditions retained; job definitions, swaps and effects are not imported.", Origin(selected)));
            _coverage.Add(new ConstructionCoverage("scripted-triggers", "unsupported", "Scripted trigger names retained without definitions or evaluation.", Origin(selected)));
            _coverage.Add(new ConstructionCoverage("technology", "context-required", "Prerequisites are identifiers, not researched technology state; technology files are not imported.", Origin(selected)));
            foreach (var field in fields)
            {
                Cover(field, field.Key);
            }
            await VerifyAsync();
            return new ConstructionProjection(definition, _files.Values, _coverage);
        }

        public async Task<ConstructionPlacementProjection> ImportPlacementAsync()
        {
            await PrepareAsync();
            (string Path, string Kind, string[] Ids)[] inventory =
            [
                ("common/zones/00_zones.txt", "zone", ["zone_default", "zone_urban"]),
                ("common/zone_slots/00_zone_slots.txt", "zoneSlot", ["slot_city_government", "slot_city_01", "slot_city_02", "slot_energy"]),
                ("common/districts/02_rural_districts.txt", "district", ["district_generator"])
            ];
            var definitions = await ReadDefinitionsAsync(inventory);
            await VerifyAsync();
            return new ConstructionPlacementProjection(definitions, _files.Values);
        }

        public async Task<ConstructionFeatureProjection> ImportFeaturesAsync()
        {
            await PrepareAsync();
            (string Path, string Kind, string[] Ids)[] inventory =
            [
                ("common/deposits/01_blocker_deposits.txt", "blocker", ["d_failing_infrastructure", "d_decrepit_dwellings"]),
                ("common/deposits/01_planetary_deposits.txt", "feature", ["d_hot_springs", "d_rushing_waterfalls", "d_tempestous_mountain", "d_veiny_cliffs", "d_prosperous_mesa", "d_rich_mountain", "d_rugged_woods", "d_fertile_lands", "d_black_soil"])
            ];
            var definitions = await ReadDefinitionsAsync(inventory);
            foreach (var definition in definitions)
            {
                var categories = definition.Fields.Where(field => field.Key == "category").ToArray();
                if (categories.Length != 1) { throw Error(definition.Origin.Source, "Expected one feature category."); }
                var blocker = Scalar(categories[0]) == "deposit_cat_blockers";
                if (blocker != (definition.Kind == "blocker")) { throw Error(definition.Origin.Source, "Feature/blocker category disagrees with selected inventory."); }
            }
            await VerifyAsync();
            return new ConstructionFeatureProjection(definitions, _files.Values);
        }

        public async Task<ConstructionEconomicCategoryProjection> ImportEconomicCategoriesAsync()
        {
            const string PATH = "common/economic_categories/00_common_categories.txt";
            await PrepareAsync();
            await LoadAsync("common/economic_categories/99_README_ECONOMIC_CATEGORIES.txt");
            var source = await LoadAsync(PATH);
            var definitions = new Dictionary<string, ConstructionPlacementDefinition>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ConstructionEconomicCategoryProjection.Seeds)
            {
                await Reach(id);
            }
            var result = new ConstructionEconomicCategoryProjection(new("construction-economic-categories-452-v1", definitions.Values, [], _files.Values, []));
            await VerifyAsync();
            return result;

            async Task Reach(string id)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (visiting.Contains(id)) { throw Error(PATH, "Economic category parent cycle."); }
                if (definitions.ContainsKey(id)) { return; }
                if (visiting.Count >= 32 || definitions.Count + visiting.Count >= 32) { throw Error(PATH, "Economic category closure exceeds 32 definitions."); }
                var matches = source.Ast.Named(id).ToArray();
                if (matches.Length != 1 || matches[0].Operator != "=" || matches[0].Block is null) { throw Error(PATH, "Missing or ambiguous reached economic category."); }
                visiting.Add(id);
                var fields = await ExpandAsync(matches[0].Block!.Entries, new Dictionary<string, string>(StringComparer.Ordinal), [], [], 1);
                var definition = new ConstructionPlacementDefinition(id, "economicCategory", Origin(matches[0]), fields);
                var parent = ConstructionEconomicCategoryProjection.ReadParent(definition);
                if (parent is not null) { await Reach(parent); }
                definitions.Add(id, definition);
                visiting.Remove(id);
            }
        }

        public async Task<ConstructionCatalogProjection> ImportScriptedClosureAsync(ConstructionCatalogProjection catalog)
        {
            _fileCountLimit = 512; _nodeLimit = 250000;
            await PrepareAsync(Directory.EnumerateFiles(Path.Combine(_root, "common", "scripted_variables"), "*.txt")
                .Select(path => "common/scripted_variables/" + Path.GetFileName(path)).Except(_variableFiles, StringComparer.Ordinal));
            var paths = Directory.EnumerateFiles(Path.Combine(_root, "common", "scripted_triggers"), "*.txt")
                .Select(path => "common/scripted_triggers/" + Path.GetFileName(path)).Order(StringComparer.Ordinal).Take(129).ToArray();
            if (paths.Length is 0 or > 128) throw Error("scripted_triggers", "Scripted source inventory exceeds budget.");
            var entries = new List<ScriptEntry>();
            foreach (var path in paths) entries.AddRange((await LoadAsync(path)).Ast.Entries.Where(entry => !entry.Key.StartsWith('@')));
            var index = entries.GroupBy(entry => entry.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            static IEnumerable<ConstructionField> Fields(IEnumerable<ConstructionField> values) => values.SelectMany(value => new[] { value }.Concat(Fields(value.Children)));
            static IEnumerable<string> Calls(IEnumerable<ConstructionField> values) => Fields(values)
                .Where(field => field.Operator == "=" && field.Children.Count == 0 && field.Scalar is "yes" or "no").Select(field => field.Key);
            static IEnumerable<ScriptEntry> Native(IEnumerable<ScriptEntry> values) => values.SelectMany(value => new[] { value }.Concat(value.Block is null ? [] : Native(value.Block.Entries)));
            var pending = new Queue<string>(Calls(catalog.Definitions.Values.SelectMany(definition => definition.Fields)).Where(index.ContainsKey).Distinct(StringComparer.Ordinal));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var definitions = new List<ConstructionPlacementDefinition>();
            while (pending.TryDequeue(out var id))
            {
                _cancellation.ThrowIfCancellationRequested();
                if (!visited.Add(id)) continue;
                if (visited.Count > 2048) throw Error("scripted_triggers", "Scripted dependency closure exceeds 2048 definitions.");
                var matches = index[id];
                if (matches.Length != 1 || matches[0].Operator != "=" || matches[0].Block is null) throw Error(matches[0].Source, "Ambiguous scripted condition.");
                var entry = matches[0];
                var body = Native(entry.Block!.Entries).ToArray();
                if (body.Any(field => field.Key.Contains('$') || field.Scalar?.Contains('$') == true))
                { _coverage.Add(new(id, "unsupported", "Parameterized scripted condition remains unresolved.", Origin(entry))); continue; }
                var fields = await ExpandAsync(entry.Block.Entries, new Dictionary<string, string>(StringComparer.Ordinal), [], [], 1);
                definitions.Add(new(id, "scriptedCondition", Origin(entry), fields));
                foreach (var dependency in Calls(fields).Where(index.ContainsKey)) pending.Enqueue(dependency);
            }
            await VerifyAsync();
            return new("construction-scripted-closure-452-v1", definitions, [], _files.Values, _coverage);
        }

        public async Task<ConstructionCatalogProjection> ImportCatalogAsync(string kind, string? buildingPath = null, string? buildingId = null, string? definitionKind = null)
        {
            if (kind == "full") { _fileCountLimit = 512; _nodeLimit = 250000; _pathByteLimit = 16 * 1024 * 1024; }
            var authorityConditions = kind == "conditions-authority";
            var civicConditions = authorityConditions || kind == "conditions-civics";
            var zoneConditions = civicConditions || kind == "conditions-zones";
            var depositConditions = zoneConditions || kind == "conditions-deposits";
            if (depositConditions) { kind = "conditions-federation"; }
            var federationConditions = kind == "conditions-federation";
            if (federationConditions) { kind = "conditions-city"; }
            var extraVariables = buildingId switch
            {
                _ when buildingPath == "common/buildings/00_capital_buildings.txt" => ["common/scripted_variables/07_scripted_variables_cosmic_storms.txt"],
                "building_medical_3" => ["common/scripted_variables/01_scripted_variables_megacorp.txt", "common/scripted_variables/08_scripted_variables_biogenesis.txt"],
                "building_medical_1" or "building_medical_2" => ["common/scripted_variables/08_scripted_variables_biogenesis.txt"],
                _ => Array.Empty<string>()
            };
            if (kind == "full" || kind.StartsWith("ordinary-building-", StringComparison.Ordinal) || kind.StartsWith("ordinary-zone-", StringComparison.Ordinal))
            {
                ResolveFile(_variableFiles[0]);
                extraVariables = Directory.EnumerateFiles(Path.Combine(_root, "common", "scripted_variables"), "*.txt")
                    .Select(path => "common/scripted_variables/" + Path.GetFileName(path))
                    .Except(_variableFiles, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            }
            await PrepareAsync(extraVariables.Concat(kind == "functional-civic" ? new[] { "common/scripted_variables/09_scripted_variables_shroud.txt" } : Array.Empty<string>()));
            if (kind == "functional-civic") { await LoadAsync("common/governments/99_README_GOVERNMENT.txt"); }
            (string Path, string Kind, string[] Ids)[] inventory = kind == "full" ? [] : kind == "shelter-closure" ? ConstructionShelterClosureInventory.Create() : kind == "zone" ?
            [
                ("common/zones/00_zones.txt", "zone", ["zone_default", "zone_urban", "zone_research_unity", "zone_industrial", "zone_energy"]),
                ("common/zone_slots/00_zone_slots.txt", "zoneSlot", ["slot_city_government", "slot_city_01", "slot_city_02", "slot_energy", "slot_minerals", "slot_food"])
            ] : kind == "city" ?
            [
                ("common/districts/00_urban_districts.txt", "district", ["district_city"])
            ] : kind == "rural" ?
            [
                ("common/districts/02_rural_districts.txt", "district", ["district_generator", "district_mining", "district_farming"])
            ] : kind == "district-capacity" ?
            [
                ("common/scripted_triggers/00_scripted_triggers.txt", "planetCondition", ["is_generator_district_uncapped", "is_mining_district_uncapped", "is_farming_district_uncapped"]),
                ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition", ["is_anglers_empire", "is_subterranean_empire"])
            ] : kind == "functional-civic" ?
            [
                ("common/governments/civics/00_civics.txt", "civic", ["civic_functional_architecture"])
            ] : kind is "conditions" or "conditions-owner" or "conditions-startup" or "conditions-capital" or "conditions-rare" or "conditions-rural" or "conditions-city" ?
            [
                ("common/scripted_triggers/01_scripted_triggers_buildings.txt", "planetCondition", ["planet_is_habitat_equivalent", "planet_is_ecu_equivalent", "planet_is_ring_world_equivalent"]),
                ("common/scripted_triggers/00_scripted_triggers.txt", "planetCondition", ["is_special_colony_type"])
            ] : buildingPath is not null && buildingId is not null ? [(buildingPath, definitionKind ?? "building", [buildingId])] : throw new ArgumentException("Unknown fixed construction catalog.");
            if (kind is "conditions-owner" or "conditions-startup" or "conditions-capital" or "conditions-rare" or "conditions-rural" or "conditions-city") { inventory = [.. inventory, ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition", ["has_functional_civic", "has_tech_or_functional_civic"])]; }
            if (kind is "conditions-startup" or "conditions-capital" or "conditions-rare" or "conditions-rural" or "conditions-city") { inventory = [.. inventory, ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition", ["is_fallen_empire"])]; }
            if (kind is "conditions-capital" or "conditions-rare" or "conditions-rural" or "conditions-city") { inventory = [.. inventory, ("common/scripted_triggers/00_scripted_triggers.txt", "planetCondition", ["uses_habitat_capitals"])]; }
            if (kind is "conditions-rare" or "conditions-rural" or "conditions-city") { inventory = [.. inventory,
                ("common/scripted_triggers/01_scripted_triggers_jobs.txt", "planetCondition", ["industrial_jobs_are_catalytic_trigger"]),
                ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition", ["is_catalytic_empire"])]; }
            if (kind is "conditions-rural" or "conditions-city") { inventory = [.. inventory, ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition", ["is_anglers_empire"])]; }
            if (kind == "conditions-city") { inventory = [.. inventory, ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition", ["is_spiritualist", "is_fanatic_spiritualist"])]; }
            if (federationConditions) { inventory = [.. inventory, ("common/scripted_triggers/02_scripted_triggers_federations.txt", "ownerCondition", ["has_make_spiritualist_perk"])]; }
            if (depositConditions) { inventory = [.. inventory, ("common/scripted_triggers/01_scripted_triggers_buildings.txt", "planetCondition", ["has_volatile_motes_deposit", "has_exotic_gases_deposit", "has_rare_crystals_deposit"])]; }
            if (zoneConditions)
            {
                inventory = [.. inventory,
                    ("common/scripted_triggers/01_scripted_triggers_buildings.txt", "planetCondition", ConstructionZoneConditionInventory.Helpers.ToArray()),
                    ("common/scripted_triggers/00_scripted_triggers.txt", "planetCondition", ["has_zone"])];
            }
            if (civicConditions) { inventory = [.. inventory, .. ConstructionOwnerCivicConditionInventory.Create()]; }
            if (authorityConditions)
            {
                inventory = [.. inventory, (ConstructionAuthorityConditionInventory.Source, "ownerCondition", ConstructionAuthorityConditionInventory.Helpers.ToArray()),
                    .. ConstructionAuthorityConditionInventory.TraditionHelpers.Select(group => (group.Source, "ownerCondition", group.Helpers))];
            }
            if (kind == "full")
            {
                var complete = new List<(string Path, string Kind, string[] Ids)>();
                foreach (var (directory, type) in new[] { ("buildings", "building"), ("zones", "zone") })
                {
                    var paths = Directory.EnumerateFiles(Path.Combine(_root, "common", directory), "*.txt")
                        .Select(path => "common/" + directory + "/" + Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();
                    if (paths.Length == 0 || paths.Length > 128) throw Error(directory, "Недопустимый размер полного source inventory.");
                    foreach (var path in paths)
                    {
                        _cancellation.ThrowIfCancellationRequested();
                        var source = await LoadAsync(path);
                        var entries = source.Ast.Entries.Where(entry => !entry.Key.StartsWith('@')).ToArray();
                        if (entries.Any(entry => entry.Operator != "=" || entry.Block is null)) throw Error(path, "Повреждённое определение каталога.");
                        complete.Add((path, type, entries.Select(entry => entry.Key).ToArray()));
                    }
                }
                if (complete.Sum(item => item.Ids.Length) > 2048) throw Error("catalog", "Полный каталог превышает 2048 определений.");
                inventory = complete.ToArray();
            }
            var definitions = await ReadDefinitionsAsync(inventory, kind is "conditions-owner" or "conditions-startup" or "conditions-capital" or "conditions-rare" or "conditions-rural" or "conditions-city" or "shelter-closure", zoneConditions || kind == "shelter-closure");
            var numbers = new List<ConstructionCatalogParameter>();
            long pathBytes = 0;
            foreach (var definition in definitions)
            {
                Visit(definition, definition.Fields, "");
            }
            _coverage.Add(new("runtime", "context-required", "Conditions, scripted triggers/effects, jobs/swaps, category composition, startup/occupancy and native phases are not executed.", null));
            await VerifyAsync();
            var profileId = authorityConditions ? ConstructionAuthorityConditionInventory.Profile
                : civicConditions ? ConstructionOwnerCivicConditionInventory.Profile
                : "construction-" + (zoneConditions ? "conditions-zones" : depositConditions ? "conditions-deposits" : federationConditions ? "conditions-federation" : kind) + "-catalog-452-v1";
            return new(profileId, definitions, numbers, _files.Values, _coverage);

            void Visit(ConstructionPlacementDefinition definition, IReadOnlyList<ConstructionField> fields, string parent)
            {
                for (var index = 0; index < fields.Count; index++)
                {
                    _cancellation.ThrowIfCancellationRequested();
                    var field = fields[index];
                    var ordinal = index.ToString(CultureInfo.InvariantCulture);
                    var bytes = 2L * (_utf8.GetByteCount(parent) + _utf8.GetByteCount(ordinal) + _utf8.GetByteCount(field.Key) + 2) + _utf8.GetByteCount(definition.Id);
                    if (bytes > _pathByteLimit - pathBytes) { throw Error(definition.Origin.Source, "Catalog diagnostic path budget exceeded."); }
                    pathBytes += bytes;
                    var path = parent + "/" + ordinal + ":" + field.Key;
                    if (field.Scalar is not null && (IsNumericLiteral(field.Scalar) || field.Scalar.StartsWith('@') && !field.Scalar.StartsWith("@[")))
                    {
                        numbers.Add(new(definition.Id, path, field.Operator, Number(field.Scalar, field.Origin.Source, []), field.Origin));
                    }
                    _coverage.Add(new(definition.Id + path, field.Scalar?.StartsWith("@[") == true ? "unsupported" : "context-required", "Ordered source retained; raw numerical extraction does not evaluate this field or its enclosing conditions.", field.Origin));
                    Visit(definition, field.Children, path);
                }
            }
        }

        private async Task<List<ConstructionPlacementDefinition>> ReadDefinitionsAsync(IEnumerable<(string Path, string Kind, string[] Ids)> inventory, bool preserveTechnologyParameter = false, bool preserveZoneParameter = false)
        {
            var definitions = new List<ConstructionPlacementDefinition>();
            foreach (var item in inventory)
            {
                var source = await LoadAsync(item.Path);
                foreach (var id in item.Ids)
                {
                    var matches = source.Ast.Named(id).ToArray();
                    if (matches.Length != 1 || matches[0].Operator != "=" || matches[0].Block is null) { throw Error(item.Path, "Missing or ambiguous selected placement definition."); }
                    var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (preserveTechnologyParameter && item.Kind == "ownerCondition" && id == "has_tech_or_functional_civic") { parameters.Add("TECH", "$TECH$"); }
                    if (preserveZoneParameter && item.Kind == "planetCondition" && id == "has_zone") { parameters.Add("zone", "$zone$"); }
                    var fields = await ExpandAsync(matches[0].Block!.Entries, parameters, [], [], 1);
                    definitions.Add(new ConstructionPlacementDefinition(id, item.Kind, Origin(matches[0]), fields));
                }
            }
            return definitions;
        }

        private async Task PrepareAsync(IEnumerable<string>? extraVariableFiles = null)
        {
            await CheckVersionAsync(await ReadAsync(METADATA));
            foreach (var path in _variableFiles.Concat(extraVariableFiles ?? Array.Empty<string>()))
            {
                var source = await LoadAsync(path);
                foreach (var variable in source.Variables)
                {
                    if (!_globals.TryAdd(variable.Key, variable.Value)) { throw Error(path, "Ambiguous global variable."); }
                }
            }
        }

        private async Task VerifyAsync()
        {
            if (_beforeVerification is not null)
            {
                await _beforeVerification(_cancellation);
            }
            _cancellation.ThrowIfCancellationRequested();
            await CheckVersionAsync(await ReadAsync(METADATA, true));
            foreach (var file in _files.Values.ToArray())
            {
                _cancellation.ThrowIfCancellationRequested();
                var current = await ReadAsync(file.RelativePath, true);
                if (current.LongLength != file.ByteLength || Hash(current) != file.Sha256)
                {
                    throw Error(file.RelativePath, "Source changed between reading and publication.");
                }
            }
            _cancellation.ThrowIfCancellationRequested();
        }

        private async Task<Source> LoadAsync(string path)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (_sources.TryGetValue(path, out var existing))
            {
                return existing;
            }
            var bytes = await ReadAsync(path);
            string text;
            try
            {
                text = _utf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                throw Error(path, "Invalid UTF-8.");
            }
            Preflight(text, path);
            ScriptBlock ast;
            try
            {
                _cancellation.ThrowIfCancellationRequested();
                ast = ParadoxParser.Parse(text, path);
                _cancellation.ThrowIfCancellationRequested();
            }
            catch (FormatException)
            {
                throw Error(path, "Invalid definition syntax.");
            }
            var variables = new Dictionary<string, ScriptEntry>(StringComparer.Ordinal);
            foreach (var variable in ast.Entries.Where(entry => entry.Key.StartsWith('@')))
            {
                _cancellation.ThrowIfCancellationRequested();
                if (variable.Operator != "=" || variable.Scalar is null || !variables.TryAdd(variable.Key, variable))
                {
                    throw Error(path, "Ambiguous or invalid local variable.");
                }
            }
            var result = new Source(ast, variables);
            _sources.Add(path, result);
            return result;
        }

        private async Task<List<ConstructionField>> ExpandAsync(IEnumerable<ScriptEntry> entries, IReadOnlyDictionary<string, string> parameters, IReadOnlyList<ConstructionOrigin> callSites, IReadOnlyList<string> inlineStack, int depth)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (depth > 64)
            {
                throw Error(callSites.LastOrDefault()?.Source ?? BUILDINGS, "Expanded tree exceeds depth 64.");
            }
            var result = new List<ConstructionField>();
            foreach (var entry in entries)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (entry.Operator == "" && (entry.Key.StartsWith("[[", StringComparison.Ordinal) || entry.Key == "]")) { throw Error(entry.Source, "Selected conditional templates are unsupported."); }
                if (InlineTriggerFragment.TryExpand(entry, parameters, out var fragment))
                {
                    result.AddRange(await ExpandAsync([fragment], parameters, [.. callSites, Origin(entry)], inlineStack, depth + 1));
                    continue;
                }
                var key = Substitute(entry.Key, parameters, entry.Source);
                if (Content.PlanetEconomy.PlanetEconomyImporter.IsRareIndustrialComposite(key))
                {
                    if (entry.Operator != "=" || entry.Scalar is null || entry.Block is not null) { throw Error(entry.Source, "Invalid rare industrial composite."); }
                    var source = await LoadAsync(Content.PlanetEconomy.PlanetEconomyImporter.RareCompositeSource);
                    var matches = source.Ast.Named(key).ToArray();
                    if (matches is not [var composite] || composite.Operator != "=" || composite.Block is null) { throw Error(entry.Source, "Missing/ambiguous rare composite."); }
                    var leaves = composite.Block.Entries.Where(child => child.Key != "custom_tooltip").ToArray();
                    var resource = key["industrial_jobs_".Length..^"_produces_add".Length];
                    if (leaves.Length != 2 || leaves.Select(child => child.Key).Distinct(StringComparer.Ordinal).Count() != 2
                        || leaves.Any(child => child.Operator != "=" || child.Scalar is null || child.Block is not null
                            || child.Key != "planet_metallurgists_" + resource + "_produces_add" && child.Key != "planet_artisans_" + resource + "_produces_add"))
                    { throw Error(entry.Source, "Unsupported rare composite shape."); }
                    var scale = Number(Substitute(entry.Scalar, parameters, entry.Source), entry.Source, []);
                    var expanded = await ExpandAsync(leaves, parameters, [.. callSites, Origin(entry)], inlineStack, depth + 1);
                    foreach (var leaf in expanded)
                    {
                        var value = checked(scale * Number(leaf.Scalar!, leaf.Origin.Source, []));
                        var text = value.ToString(CultureInfo.InvariantCulture);
                        if (_utf8.GetByteCount(text) > TOTAL_LIMIT - _expandedBytes) { throw Error(entry.Source, "Expanded text exceeds 8 MiB."); }
                        _expandedBytes += _utf8.GetByteCount(text);
                        result.Add(new ConstructionField(leaf.Key, leaf.Operator, text, [], leaf.Origin, leaf.InlineCallSites));
                    }
                    continue;
                }
                if (key == "inline_script")
                {
                    if (entry.Operator != "=" || entry.Block is null && entry.Scalar is null || inlineStack.Count >= 16)
                    {
                        throw Error(entry.Source, "Invalid inline or inline depth exceeds 16.");
                    }
                    var scriptEntries = entry.Block?.Named("script").ToArray() ?? [];
                    if (entry.Block is not null && (scriptEntries.Length != 1 || scriptEntries[0].Scalar is null || scriptEntries[0].Operator != "="))
                    {
                        throw Error(entry.Source, "Inline requires one scalar script.");
                    }
                    var id = Substitute(entry.Scalar ?? scriptEntries[0].Scalar!, parameters, entry.Source);
                    ValidatePath(id, entry.Source);
                    var path = "common/inline_scripts/" + id + ".txt";
                    if (inlineStack.Contains(path, StringComparer.Ordinal))
                    {
                        throw Error(entry.Source, "Inline dependency cycle.");
                    }
                    var args = new InlineScriptParameters();
                    foreach (var argument in entry.Block?.Entries.Where(item => item.Key != "script") ?? [])
                    {
                        if (argument.Operator != "=" || argument.Scalar is null || argument.Key.Length == 0 || argument.Key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
                        {
                            throw Error(entry.Source, "Invalid scalar inline argument.");
                        }
                        var value = Substitute(argument.Scalar, parameters, entry.Source);
                        if (value.StartsWith('@'))
                        {
                            value = Number(value, entry.Source, []).ToString(CultureInfo.InvariantCulture);
                        }
                        if (!args.TryAdd(argument.Key, value))
                        {
                            throw Error(entry.Source, "Duplicate inline argument.");
                        }
                        args.Origins.Add(argument.Key, argument);
                    }
                    var inline = await LoadAsync(path);
                    result.AddRange(await ExpandAsync(inline.Ast.Entries.Where(item => !item.Key.StartsWith('@')), args, [.. callSites, Origin(entry)], [.. inlineStack, path], depth));
                    continue;
                }
                if (++_expandedNodes > _nodeLimit)
                {
                    throw Error(entry.Source, "Expanded tree exceeds node budget.");
                }
                var scalar = entry.Scalar is null ? null : Substitute(entry.Scalar, parameters, entry.Source);
                var bytes = (long)_utf8.GetByteCount(key) + _utf8.GetByteCount(entry.Operator) + (scalar is null ? 0 : _utf8.GetByteCount(scalar));
                if (bytes > TOTAL_LIMIT - _expandedBytes)
                {
                    throw Error(entry.Source, "Expanded text exceeds 8 MiB.");
                }
                _expandedBytes += (int)bytes;
                var children = entry.Block is null ? [] : await ExpandAsync(entry.Block.Entries, parameters, callSites, inlineStack, depth + 1);
                result.Add(new ConstructionField(key, entry.Operator, scalar, children, Origin(entry), callSites));
            }
            return result;
        }

        private string Substitute(string text, IReadOnlyDictionary<string, string> parameters, string source)
        {
            if (!text.Contains('$'))
            {
                return text;
            }
            var position = 0;
            long bytes = 0;
            while (position < text.Length)
            {
                _cancellation.ThrowIfCancellationRequested();
                var start = text.IndexOf('$', position);
                if (start < 0)
                {
                    Count(text.AsSpan(position));
                    break;
                }
                Count(text.AsSpan(position, start - position));
                var end = text.IndexOf('$', start + 1);
                if (end < 0 || !parameters.TryGetValue(text[(start + 1)..end], out var replacement))
                {
                    throw Error(source, "Missing inline parameter.");
                }
                Count(replacement.AsSpan());
                position = end + 1;
            }
            _substitutionBytes += (int)bytes;
            var output = new StringBuilder();
            position = 0;
            while (position < text.Length)
            {
                _cancellation.ThrowIfCancellationRequested();
                var start = text.IndexOf('$', position);
                if (start < 0)
                {
                    output.Append(text.AsSpan(position));
                    break;
                }
                output.Append(text.AsSpan(position, start - position));
                var end = text.IndexOf('$', start + 1);
                output.Append(parameters[text[(start + 1)..end]]);
                position = end + 1;
            }
            return output.ToString();

            void Count(ReadOnlySpan<char> value)
            {
                bytes += _utf8.GetByteCount(value);
                if (bytes > TOTAL_LIMIT - _expandedBytes || bytes > TOTAL_LIMIT - _substitutionBytes)
                {
                    throw Error(source, "Inline substitution exceeds expanded or generated text budget.");
                }
            }
        }

        private decimal Number(string text, string source, IReadOnlyList<string> stack)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var literal))
            {
                return literal;
            }
            var identity = source + "\n" + text;
            if (!text.StartsWith('@') || text.StartsWith("@[") || stack.Count >= 32 || stack.Contains(identity, StringComparer.Ordinal))
            {
                throw Error(source, "Unsupported number, variable cycle or variable depth exceeds 32.");
            }
            var local = _sources.TryGetValue(source, out var parsed) ? parsed.Variables.GetValueOrDefault(text) : null;
            var variable = local ?? _globals.GetValueOrDefault(text);
            if (variable?.Scalar is null)
            {
                throw Error(source, "Missing required numeric variable.");
            }
            return Number(variable.Scalar, variable.Source, [.. stack, identity]);
        }

        private static bool IsNumericLiteral(string text)
        {
            var digit = false;
            var dot = false;
            var start = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
            for (var index = start; index < text.Length; index++)
            {
                if (char.IsAsciiDigit(text[index])) { digit = true; }
                else if (text[index] == '.' && !dot) { dot = true; }
                else { return false; }
            }
            return digit;
        }

        private ConstructionDefinition Compile(ScriptEntry selected, IReadOnlyList<ConstructionField> fields)
        {
            var time = Required(fields, "base_buildtime");
            var category = Required(fields, "category");
            var resources = Required(fields, "resources");
            RequireBlock(resources);
            var economic = Required(resources.Children, "category");
            var costs = new List<ConstructionCost>();
            foreach (var cost in resources.Children.Where(field => field.Key == "cost"))
            {
                RequireBlock(cost);
                var trigger = Optional(cost.Children, "trigger");
                if (trigger is not null)
                {
                    RequireBlock(trigger);
                }
                var mult = Optional(cost.Children, "mult");
                costs.Add(new ConstructionCost(Amounts(cost.Children.Where(field => field.Key is not "trigger" and not "mult")), trigger, mult is null ? null : Numeric(mult)));
            }
            if (costs.Count == 0)
            {
                throw Error(resources.Origin.Source, "Missing cost block.");
            }
            var upkeep = Required(resources.Children, "upkeep");
            RequireBlock(upkeep);
            foreach (var key in new[] { "potential", "destroy_trigger", "allow", "abort_trigger", "abort" })
            {
                var condition = Optional(fields, key);
                if (condition is not null)
                {
                    RequireBlock(condition);
                }
                else
                {
                    _coverage.Add(new ConstructionCoverage(key, "context-required", "Block absent; this does not satisfy general construction rules.", Origin(selected)));
                }
            }
            return new ConstructionDefinition("building_hydroponics_farm", Origin(selected), Numeric(time), Scalar(category), Scalar(economic), List(Required(fields, "building_sets")), List(Required(fields, "prerequisites")), Amounts(upkeep.Children), costs, fields);
        }

        private IReadOnlyList<ConstructionAmount> Amounts(IEnumerable<ConstructionField> entries)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<ConstructionAmount>();
            foreach (var entry in entries)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (!seen.Add(entry.Key))
                {
                    throw Error(entry.Origin.Source, "Ambiguous resource amount.");
                }
                result.Add(new ConstructionAmount(entry.Key, Numeric(entry), entry.Origin));
            }
            return result;
        }

        private decimal Numeric(ConstructionField field)
        {
            return Number(Scalar(field), field.Origin.Source, []);
        }

        private static string Scalar(ConstructionField field)
        {
            if (field.Operator != "=" || field.Scalar is null)
            {
                throw Error(field.Origin.Source, "Expected one scalar with assignment operator.");
            }
            return field.Scalar;
        }

        private static IReadOnlyList<string> List(ConstructionField field)
        {
            RequireBlock(field);
            if (field.Children.Any(item => item.Operator != "" || item.Scalar is null))
            {
                throw Error(field.Origin.Source, "Expected a plain symbol list.");
            }
            return field.Children.Select(item => item.Scalar!).ToArray();
        }

        private static void RequireBlock(ConstructionField field)
        {
            if (field.Operator != "=" || field.Scalar is not null)
            {
                throw Error(field.Origin.Source, "Expected a block with assignment operator.");
            }
        }

        private static ConstructionField Required(IReadOnlyList<ConstructionField> fields, string key)
        {
            return Optional(fields, key) ?? throw Error(BUILDINGS, "Missing required construction profile field.");
        }

        private static ConstructionField? Optional(IReadOnlyList<ConstructionField> fields, string key)
        {
            var matches = fields.Where(field => field.Key == key).ToArray();
            if (matches.Length > 1)
            {
                throw Error(matches[0].Origin.Source, "Ambiguous single field.");
            }
            return matches.SingleOrDefault();
        }

        private void Cover(ConstructionField field, string path)
        {
            _cancellation.ThrowIfCancellationRequested();
            var context = field.Key is "potential" or "destroy_trigger" or "trigger" or "owner" or "from?" or "OR" or "AND" or "NOT" or "exists" or "num_buildings" or "allow" or "abort_trigger";
            var extracted = field.Key is "base_buildtime" or "category" or "resources" or "cost" or "mult" or "upkeep" or "building_sets" or "prerequisites" || field.Operator == "";
            var status = field.Scalar?.StartsWith("@[") == true ? "unsupported" : field.Key.StartsWith("ai_", StringComparison.Ordinal) ? "excluded" : context ? "context-required" : extracted ? "extracted" : "unsupported";
            _coverage.Add(new ConstructionCoverage(path, status, status switch
            {
                "extracted" => "Extracted source data, not a native evaluation result.",
                "context-required" => "Runtime scope/state and a separate evaluator are required; condition not evaluated.",
                "excluded" => "AI behavior is excluded from the numerical projection.",
                _ => "Field retained in expanded tree; semantics not executed."
            }, field.Origin));
            foreach (var child in field.Children)
            {
                Cover(child, path + "/" + child.Key);
            }
        }

        private async Task<byte[]> ReadAsync(string relativePath, bool verification = false)
        {
            _cancellation.ThrowIfCancellationRequested();
            var path = ResolveFile(relativePath);
            var limit = relativePath == METADATA ? 1024 * 1024 : FILE_LIMIT;
            if (!verification && (_files.ContainsKey(relativePath) || _files.Count >= _fileCountLimit))
            {
                throw Error(relativePath, "Repeated read or source file budget exceeded.");
            }
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var length = stream.Length;
                if (length > limit || !verification && length > TOTAL_LIMIT - _totalBytes)
                {
                    throw Error(relativePath, "File limit or total 8 MiB budget exceeded.");
                }
                using var output = new MemoryStream((int)length);
                var buffer = new byte[16384];
                int read;
                while ((read = await stream.ReadAsync(buffer, _cancellation)) != 0)
                {
                    if (output.Length + read > limit || !verification && output.Length + read > TOTAL_LIMIT - _totalBytes)
                    {
                        throw Error(relativePath, "Source exceeded budget during reading.");
                    }
                    output.Write(buffer, 0, read);
                }
                if (output.Length != length || stream.Length != length)
                {
                    throw Error(relativePath, "Source changed during reading.");
                }
                var bytes = output.ToArray();
                if (!verification)
                {
                    _totalBytes += bytes.Length;
                    _files.Add(relativePath, new ConstructionFile(relativePath, bytes.Length, Hash(bytes)));
                }
                return bytes;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Error(relativePath, "File unavailable for reading.");
            }
        }

        private Task CheckVersionAsync(byte[] bytes)
        {
            _cancellation.ThrowIfCancellationRequested();
            try
            {
                // Strict UTF-8 is also required for JSON metadata.
                _utf8.GetString(bytes);
                using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count(field => field.Name == "rawVersion") != 1 || !root.TryGetProperty("rawVersion", out var value) || value.ValueKind != JsonValueKind.String || value.GetString() != "v4.5.2")
                {
                    throw Error(METADATA, "Exact rawVersion v4.5.2 is required.");
                }
            }
            catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
            {
                throw Error(METADATA, "Invalid version metadata.");
            }
            return Task.CompletedTask;
        }

        private string ResolveFile(string relativePath)
        {
            ValidatePath(relativePath, relativePath);
            ResolveRoot(_root);
            var path = _root;
            try
            {
                var segments = relativePath.Split('/');
                for (var index = 0; index < segments.Length; index++)
                {
                    _cancellation.ThrowIfCancellationRequested();
                    path = Path.Combine(path, segments[index]);
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 || (index < segments.Length - 1) != ((attributes & FileAttributes.Directory) != 0))
                    {
                        throw Error(relativePath, "Invalid dependency type or reparse point.");
                    }
                }
                return path;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw Error(relativePath, "Dependency missing or unavailable.");
            }
        }

        private static string ResolveRoot(string root)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    throw new ConstructionImportException("Installation root is missing.");
                }
                var full = Path.GetFullPath(root);
                for (var directory = new DirectoryInfo(full); directory is not null; directory = directory.Parent)
                {
                    if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new ConstructionImportException("Installation root is missing or contains a reparse point.");
                    }
                }
                return full;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new ConstructionImportException("Cannot validate installation root.");
            }
        }

        private static void ValidatePath(string path, string safeSource)
        {
            if (path.Length == 0 || Path.IsPathRooted(path) || path.Any(character => char.IsControl(character) || character is ':' or '\\') || path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            {
                throw Error(safeSource.StartsWith("common/", StringComparison.Ordinal) ? safeSource : BUILDINGS, "Invalid relative dependency path.");
            }
        }

        private void Preflight(string text, string source)
        {
            var depth = 0;
            var math = 0;
            var conditionalDepth = 0;
            var conditionalCount = 0;
            var quoted = false;
            var comment = false;
            for (var index = 0; index < text.Length; index++)
            {
                if ((index & 4095) == 0)
                {
                    _cancellation.ThrowIfCancellationRequested();
                }
                var character = text[index];
                if (comment)
                {
                    comment = character is not '\r' and not '\n';
                    continue;
                }
                if (quoted)
                {
                    if (character == '\\')
                    {
                        index++;
                    }
                    else if (character == '"')
                    {
                        quoted = false;
                    }
                    continue;
                }
                if (math > 0)
                {
                    if (character == '[' && ++math > 64)
                    {
                        throw Error(source, "Inline math exceeds depth 64.");
                    }
                    if (character == ']')
                    {
                        math--;
                    }
                    continue;
                }
                if (character == '#')
                {
                    comment = true;
                }
                else if (character == '"')
                {
                    quoted = true;
                }
                else if (character == '@' && index + 1 < text.Length && text[index + 1] == '[')
                {
                    math = 1;
                    index++;
                }
                else if (character == '[' && index + 1 < text.Length && text[index + 1] == '[')
                {
                    if (++conditionalDepth > 64 || ++conditionalCount > 50000) { throw Error(source, "Conditional header nesting/count budget exceeded."); }
                    var closing = text.IndexOf(']', index + 2);
                    if (closing < 0 || closing - index > 131) { throw Error(source, "Invalid or unterminated conditional header."); }
                    index = closing;
                }
                else if (character == ']' && conditionalDepth > 0)
                {
                    conditionalDepth--;
                }
                else if (character == '{' && ++depth > 64)
                {
                    throw Error(source, "Syntax nesting exceeds 64.");
                }
                else if (character == '}' && --depth < 0)
                {
                    throw Error(source, "Unexpected closing brace.");
                }
            }
            if (conditionalDepth != 0) { throw Error(source, "Unterminated conditional template body."); }
            _cancellation.ThrowIfCancellationRequested();
        }

        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        private static ConstructionOrigin Origin(ScriptEntry entry) => new(entry.Source, entry.Line, entry.Column);
        private static ConstructionImportException Error(string source, string reason) => new($"{source}: {reason}");
        private sealed record Source(ScriptBlock Ast, IReadOnlyDictionary<string, ScriptEntry> Variables);
    }
}
