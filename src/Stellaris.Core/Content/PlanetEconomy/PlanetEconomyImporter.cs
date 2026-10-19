using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.PlanetEconomy;

/// <summary>Импортирует ограниченный ordinary каталог jobs и экономики планеты из Stellaris 4.5.2.</summary>
public sealed class PlanetEconomyImporter
{
    private const string ResourceSource = "common/strategic_resources/00_strategic_resources.txt";
    private const string ConceptSource = "common/game_concepts/01_job_concepts.txt";
    public const string RareCompositeSource = "common/static_modifiers/24_static_modifiers_jobs.txt";
    private static readonly string[] RareResources = ["volatile_motes", "exotic_gases", "rare_crystals", "sr_zro", "sr_dark_matter", "sr_living_metal", "nanites"];
    // Метаданные хранения нужны потребителям классификации, но сами по себе не создают ресурс в мире.
    private static readonly string[] ClassificationResources = ["influence", "minor_artifacts"];
    public static bool IsRareIndustrialComposite(string key) => key is "industrial_jobs_volatile_motes_produces_add" or "industrial_jobs_exotic_gases_produces_add" or "industrial_jobs_rare_crystals_produces_add";
    private static readonly string[] DefinitionDirectories = ["common/buildings", "common/zones"];
    private const int FileLimit = 2 * 1024 * 1024;
    private const long TotalLimit = 16 * 1024 * 1024;
    private static readonly string[] JobPaths =
    [
        "common/pop_jobs/01_ruler_jobs.txt", "common/pop_jobs/02_specialist_jobs.txt", "common/pop_jobs/03_worker_jobs.txt"
    ];
    private static readonly (string Id, string Path)[] JobInventory =
    [
        ("technician", "common/pop_jobs/03_worker_jobs.txt"), ("miner", "common/pop_jobs/03_worker_jobs.txt"),
        ("farmer", "common/pop_jobs/03_worker_jobs.txt"), ("clerk", "common/pop_jobs/03_worker_jobs.txt"),
        ("physicist", "common/pop_jobs/02_specialist_jobs.txt"), ("biologist", "common/pop_jobs/02_specialist_jobs.txt"),
        ("engineer", "common/pop_jobs/02_specialist_jobs.txt"), ("foundry", "common/pop_jobs/02_specialist_jobs.txt"),
        ("artisan", "common/pop_jobs/02_specialist_jobs.txt"), ("bureaucrat", "common/pop_jobs/02_specialist_jobs.txt"),
        ("enforcer", "common/pop_jobs/02_specialist_jobs.txt"), ("entertainer", "common/pop_jobs/02_specialist_jobs.txt"),
        ("healthcare", "common/pop_jobs/02_specialist_jobs.txt"), ("soldier", "common/pop_jobs/02_specialist_jobs.txt"),
        ("politician", "common/pop_jobs/01_ruler_jobs.txt"), ("merchant", "common/pop_jobs/01_ruler_jobs.txt")
    ];
    private static readonly (string Id, string Path)[] DistrictInventory =
    [
        ("district_generator", "common/districts/02_rural_districts.txt"),
        ("district_mining", "common/districts/02_rural_districts.txt"),
        ("district_farming", "common/districts/02_rural_districts.txt"),
        ("district_city", "common/districts/00_urban_districts.txt")
    ];
    private static readonly HashSet<string> UnlimitedResources = new(StringComparer.Ordinal)
    {
        "unity", "physics_research", "society_research", "engineering_research"
    };
    private static readonly HashSet<string> OrdinaryFalseConditionPrefixes = new(StringComparer.Ordinal)
    {
        "artisan_is_", "industrial_jobs_are_catalytic_trigger", "physicist_is_", "biologist_is_", "engineer_is_",
        "politician_is_", "bureaucrat_is_", "technician_is_", "miner_is_", "farmer_is_", "has_carrier_flag",
        "has_planet_flag", "has_valid_civic", "has_active_tradition", "is_gestalt", "is_robot_empire", "is_nomadic",
        "is_wilderness_empire", "is_fallen_empire"
    };

    private readonly Func<CancellationToken, Task>? _beforeVerification;

    public PlanetEconomyImporter() { }
    internal PlanetEconomyImporter(Func<CancellationToken, Task> beforeVerification) => _beforeVerification = beforeVerification ?? throw new ArgumentNullException(nameof(beforeVerification));

    public Task<PlanetEconomyCatalog> ImportAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        return new ImportRun(gameRoot, cancellationToken, _beforeVerification).ImportAsync();
    }

    private sealed class ImportRun
    {
        private readonly string _root;
        private readonly CancellationToken _cancellation;
        private readonly Func<CancellationToken, Task>? _beforeVerification;
        private readonly Dictionary<string, SourceFile> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptEntry> _globalVariables = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _inlineScripts = new(StringComparer.Ordinal);
        private long _totalBytes;
        private string _version = "";

        public ImportRun(string root, CancellationToken cancellation, Func<CancellationToken, Task>? beforeVerification)
        {
            _root = Path.GetFullPath(root);
            _cancellation = cancellation;
            _beforeVerification = beforeVerification;
        }

        public async Task<PlanetEconomyCatalog> ImportAsync()
        {
            await LoadVersionAsync();
            var definitionPaths = DefinitionDirectories.SelectMany(EnumerateDirectoryTextFiles).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (definitionPaths.Length == 0 || definitionPaths.Length > 128) { throw new PlanetEconomyImportException("Planet definition source inventory is empty or exceeds 128 files."); }
            var jobSourcePaths = EnumerateDirectoryTextFiles("common/pop_jobs").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (jobSourcePaths.Length == 0 || jobSourcePaths.Length > 128) { throw new PlanetEconomyImportException("Population job source inventory is empty or exceeds 128 files."); }
            foreach (var path in JobPaths.Concat(jobSourcePaths).Concat(DistrictInventory.Select(item => item.Path)).Concat(definitionPaths).Append(ResourceSource).Append(ConceptSource).Distinct(StringComparer.Ordinal))
            {
                await LoadTextAsync(path);
            }
            await LoadVariablesAsync();

            var jobEntries = JobInventory.Select(item => (item.Id, Entry: FindUnique(item.Id, JobPaths), item.Path)).ToArray();
            var districtEntries = DistrictInventory.Select(item => (Kind: PlanetEconomyDefinitionKind.District, item.Id, Entry: FindUnique(item.Id, [item.Path]))).ToArray();
            var definitionEntries = DiscoverDefinitions(definitionPaths);
            var coverage = ClassifyDefinitions(definitionEntries);
            var includedDefinitionEntries = definitionEntries.Where(item => coverage.Single(result => result.Id == item.Id).Status == PlanetEconomyDefinitionCoverageStatus.Included).ToArray();
            await LoadInlineClosureAsync(jobEntries.Select(item => item.Entry).Concat(districtEntries.Select(item => item.Entry)).Concat(includedDefinitionEntries.Select(item => item.Entry)));
            if (includedDefinitionEntries.Any(item => HasRareComposite(item.Entry))) { await LoadTextAsync(RareCompositeSource); }

            var resourceAst = _files[ResourceSource].Root!;
            var definitions = districtEntries.Select(item => CompileDefinition(item.Kind, item.Id, item.Entry))
                .Concat(includedDefinitionEntries.Select(item => CompileDefinition(item.Kind, item.Id, item.Entry))).ToArray();
            var capacityTerms = definitions.SelectMany(item => item.Terms)
                .Where(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacity).ToArray();
            var baseJobIds = JobInventory.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var closureTargets = capacityTerms.Where(term => !baseJobIds.Contains(term.TargetId))
                .GroupBy(term => term.TargetId, StringComparer.Ordinal).ToArray();
            var closureJobEntries = new List<(string Id, ScriptEntry Entry, string Path)>();
            var preclassifiedClosure = new List<PlanetEconomyJobClosure>();
            foreach (var target in closureTargets)
            {
                var source = FindOptionalUnique(target.Key, jobSourcePaths);
                var capacity = target.OrderBy(term => term.Provenance.RelativePath, StringComparer.Ordinal).ThenBy(term => term.Provenance.Line).First();
                if (source is null)
                {
                    preclassifiedClosure.Add(new(target.Key, PlanetEconomyJobClosureStatus.Missing, "source_job_definition_not_found", capacity.Provenance));
                    continue;
                }
                if (source.Source is not ("common/pop_jobs/00_other_jobs.txt" or "common/pop_jobs/01_ruler_jobs.txt"
                    or "common/pop_jobs/02_specialist_jobs.txt" or "common/pop_jobs/03_worker_jobs.txt"))
                {
                    preclassifiedClosure.Add(new(target.Key, PlanetEconomyJobClosureStatus.Gated,
                        "special_job_source_file:" + Path.GetFileName(source.Source), Provenance(source)));
                    continue;
                }
                if (source.AsBlock().Optional("category") is null && !source.AsBlock().Named("inline_script").Any())
                {
                    preclassifiedClosure.Add(new(target.Key, PlanetEconomyJobClosureStatus.Gated, "source_job_category_missing", Provenance(source)));
                    continue;
                }
                closureJobEntries.Add((target.Key, source, source.Source));
            }
            await LoadInlineClosureAsync(closureJobEntries.Select(item => item.Entry));
            var jobs = jobEntries.Select(item => CompileJob(item.Id, item.Entry))
                .Concat(closureJobEntries.Select(item => CompileJob(item.Id, item.Entry))).ToArray();
            var jobsById = jobs.ToDictionary(item => item.Id, StringComparer.Ordinal);
            var jobClosure = capacityTerms.GroupBy(term => term.TargetId, StringComparer.Ordinal).Select(group =>
            {
                var first = group.OrderBy(term => term.Provenance.RelativePath, StringComparer.Ordinal).ThenBy(term => term.Provenance.Line).First();
                if (!jobsById.TryGetValue(group.Key, out var job))
                    return preclassifiedClosure.Single(item => item.JobId == group.Key);
                var capacityActive = group.Any(term => term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext);
                var gated = job.AvailabilityDisposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext;
                return new PlanetEconomyJobClosure(job.Id, gated ? PlanetEconomyJobClosureStatus.Gated : PlanetEconomyJobClosureStatus.Covered,
                    gated ? "source_job_availability:" + job.AvailabilityDisposition
                        : !capacityActive ? "recipe_imported;capacity_reference_inactive_in_ordinary_context"
                        : job.AvailabilityDisposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext ? "recipe_imported;availability_requires_runtime_context" : null,
                    job.Provenance);
            }).ToArray();
            var terms = jobs.SelectMany(job => job.Terms).Concat(definitions.SelectMany(definition => definition.Terms)).ToArray();
            var resourceIds = terms.Where(term => term.Kind is PlanetEconomyTermKind.ResourceProduction or PlanetEconomyTermKind.ResourceUpkeep
                    or PlanetEconomyTermKind.BuildingUpkeep or PlanetEconomyTermKind.DistrictUpkeep or PlanetEconomyTermKind.ZoneUpkeep or PlanetEconomyTermKind.ConstructionCost)
                .Select(term => term.TargetId).Concat(RareResources).Concat(ClassificationResources)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var resources = resourceIds.Select(id => CompileResource(id, resourceAst)).ToArray();

            if (_beforeVerification is not null) { await _beforeVerification(_cancellation); }
            await VerifyStableAsync();
            return new PlanetEconomyCatalog(_version, resources, jobs, definitions,
                _files.Values.Select(file => new PlanetEconomySourceFile(file.Path, file.Bytes.LongLength, file.Sha256)), coverage, jobClosure);
        }

        private IEnumerable<string> EnumerateDirectoryTextFiles(string relativeDirectory)
        {
            var directory = Resolve(relativeDirectory);
            if (!Directory.Exists(directory)) { throw new PlanetEconomyImportException("Required planet definition directory is missing: " + relativeDirectory); }
            foreach (var path in EnumerateSourceFiles(directory).Where(path => Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)))
            {
                var relative = Path.GetRelativePath(_root, path).Replace('\\', '/');
                if (!relative.StartsWith(relativeDirectory + "/", StringComparison.Ordinal)) { throw new PlanetEconomyImportException("Planet definition path escaped its directory."); }
                yield return relative;
            }
        }

        private (PlanetEconomyDefinitionKind Kind, string Id, string Path, ScriptEntry Entry)[] DiscoverDefinitions(IEnumerable<string> paths)
        {
            var found = new List<(PlanetEconomyDefinitionKind Kind, string Id, string Path, ScriptEntry Entry)>();
            foreach (var path in paths)
            {
                var source = _files[path];
                foreach (var entry in source.Root!.Entries)
                {
                    if (entry.Block is null) { continue; }
                    var kind = path.StartsWith("common/buildings/", StringComparison.Ordinal) ? PlanetEconomyDefinitionKind.Building
                        : path.StartsWith("common/zones/", StringComparison.Ordinal) ? PlanetEconomyDefinitionKind.Zone : (PlanetEconomyDefinitionKind?)null;
                    if (kind is null) { continue; }
                    RequireAssignment(entry);
                    _ = entry.AsBlock();
                    found.Add((kind.Value, entry.Key, path, entry));
                }
            }
            var duplicate = found.GroupBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null) { throw new PlanetEconomyImportException("Duplicate planet definition ID: " + duplicate.Key); }
            return found.OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Entry.Line).ToArray();
        }

        private PlanetEconomyDefinitionCoverage[] ClassifyDefinitions((PlanetEconomyDefinitionKind Kind, string Id, string Path, ScriptEntry Entry)[] definitions)
        {
            var coverage = definitions.Select(item => ClassifyDefinition(item.Kind, item.Id, item.Path, item.Entry)).ToDictionary(item => item.Id, StringComparer.Ordinal);
            var upgrades = definitions.ToDictionary(item => item.Id,
                item => item.Entry.AsBlock().Optional("upgrades") is { } field ? FlattenScalars(field).ToArray() : Array.Empty<string>(), StringComparer.Ordinal);
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var definition in definitions)
                {
                    if (!upgrades.TryGetValue(definition.Id, out var targets)) continue;
                    foreach (var target in targets)
                    {
                        if (!coverage.TryGetValue(target, out var child) || child.Status != PlanetEconomyDefinitionCoverageStatus.Included) continue;
                        var parents = definitions.Where(candidate => upgrades[candidate.Id].Contains(target, StringComparer.Ordinal)).Select(candidate => coverage[candidate.Id]).ToArray();
                        if (parents.Length == 0 || parents.Any(parent => parent.Status == PlanetEconomyDefinitionCoverageStatus.Included)) continue;
                        var unresolvedParent = parents.FirstOrDefault(parent => parent.Status == PlanetEconomyDefinitionCoverageStatus.Unresolved);
                        coverage[target] = child with
                        {
                            Status = unresolvedParent is null ? PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific : PlanetEconomyDefinitionCoverageStatus.Unresolved,
                            Reason = unresolvedParent is null ? "upgrade_chain_from_special_definition:" + definition.Id : "upgrade_chain_parent_unresolved:" + unresolvedParent.Id
                        };
                        changed = true;
                    }
                }
            }
            return definitions.Select(item => coverage[item.Id]).ToArray();
        }

        private PlanetEconomyDefinitionCoverage ClassifyDefinition(PlanetEconomyDefinitionKind kind, string id, string path, ScriptEntry entry)
        {
            // File families and explicit root eligibility gates identify source-special definitions.
            var reason = path switch
            {
                "common/buildings/12_event_buildings.txt" => "event_definition_file",
                "common/buildings/13_fallen_empire_buildings.txt" => "fallen_empire_definition_file",
                "common/buildings/14_branch_office_buildings.txt" => "branch_office_definition_file",
                "common/buildings/15_overlord_holdings.txt" => "overlord_holding_definition_file",
                "common/buildings/10_deposit_buildings.txt" => "planetary_deposit_building_file",
                "common/buildings/11_primitive_buildings.txt" => "primitive_civilization_building_file",
                "common/buildings/16_first_contact_buildings.txt" => "first_contact_origin_definition_file",
                "common/buildings/18_astral_planes_buildings.txt" => "astral_scenario_definition_file",
                "common/buildings/21_wilderness_buildings.txt" => "wilderness_definition_file",
                "common/buildings/22_biogenesis_buildings.txt" => "biogenesis_origin_definition_file",
                "common/buildings/22_extreme_frontiers_buildings.txt" => "extreme_frontiers_origin_definition_file",
                "common/buildings/23_shroud_buildings.txt" => "shroud_scenario_definition_file",
                "common/buildings/24_nomads_buildings.txt" => "nomad_definition_file",
                "common/zones/02_special_zones.txt" => "unique_scenario_zone_file",
                "common/zones/01_habitat_zones.txt" => "habitat_definition_file",
                "common/zones/03_wilderness_zones.txt" => "wilderness_definition_file",
                "common/zones/04_secondary_zones.txt" => "special_planet_class_or_gestalt_zone_file",
                "common/zones/05_arkship_zones.txt" => "arkship_definition_file",
                _ => (string?)null
            };
            if (reason is null)
            {
                var specialIdFamily = kind == PlanetEconomyDefinitionKind.Building
                    ? new[] { "building_hive_", "building_machine_", "building_primitive_", "building_lathe_", "building_hab_", "building_resort_", "building_nomad_", "building_wilderness_" }
                    : new[] { "zone_habitat_", "zone_wilderness_", "zone_ark_", "zone_lathe_" };
                var specialIdMarker = specialIdFamily.FirstOrDefault(prefix => id.StartsWith(prefix, StringComparison.Ordinal));
                if (specialIdMarker is not null) { reason = "source_id_special_family:" + specialIdMarker.TrimEnd('_'); }
                if (reason is null && kind == PlanetEconomyDefinitionKind.Building
                    && (id.Contains("_hive_", StringComparison.Ordinal) || id.Contains("_machine_", StringComparison.Ordinal) || id.Contains("_primitive_", StringComparison.Ordinal)))
                { reason = "source_id_special_family:gestalt_or_primitive"; }
                if (reason is null && kind == PlanetEconomyDefinitionKind.Building
                    && (id.EndsWith("_hive", StringComparison.Ordinal) || id.EndsWith("_machine", StringComparison.Ordinal)))
                { reason = "source_id_special_family:gestalt"; }
                if (reason is null && kind == PlanetEconomyDefinitionKind.Zone
                    && (id.Contains("_hive", StringComparison.Ordinal) || id.Contains("_ring_world", StringComparison.Ordinal)
                        || id.Contains("_arcology", StringComparison.Ordinal) || id.Contains("_nexus", StringComparison.Ordinal)
                        || id.Contains("_bio_trophy", StringComparison.Ordinal)))
                { reason = "source_id_special_family:planet_class_or_gestalt"; }
            }
            var block = entry.AsBlock();
            if (reason is null && kind == PlanetEconomyDefinitionKind.Building
                && id is "building_slave_capital" or "building_slave_major_capital")
            { reason = "source_id_thrall_world_capital"; }
            if (reason is null && id.StartsWith("holding_", StringComparison.Ordinal)) { reason = "overlord_or_branch_office_holding_definition"; }
            if (reason is null)
            {
                var rootEligibility = block.Entries.Where(item => item.Key is "potential" or "possible" or "allow" or "potential_and").ToArray();
                var mandatoryRoot = rootEligibility.SelectMany(item => MandatoryConditions(item.AsBlock()))
                    .Where(item => !item.UnderOr).Select(item => item.Entry).ToArray();
                var specialGate = mandatoryRoot.FirstOrDefault(item => item.Scalar == "yes" && item.Key is ("is_hive_empire" or "is_machine_empire" or "is_robot_empire" or "is_gestalt"
                        or "is_nomadic" or "is_wilderness_empire" or "is_branch_office" or "is_fallen_empire" or "is_subterranean_empire"
                        or "is_memory_vault_empire" or "is_toxic_baths_empire"));
                if (specialGate is not null)
                { reason = "root_eligibility:" + specialGate.Key + "=yes"; }
                if (reason is null && mandatoryRoot.Any(item => item.Key == "has_symbiosis_integrator_job" && item.Scalar == "yes"))
                { reason = "root_eligibility:special_symbiosis_job"; }
                if (reason is null && mandatoryRoot.Any(item => item.Key == "has_origin" && item.Scalar is not null && item.Scalar != "origin_default"))
                { reason = "root_eligibility:specific_origin"; }
                if (reason is null && mandatoryRoot.Any(item => item.Key == "has_modifier" && item.Scalar == "resort_colony"))
                { reason = "root_eligibility:special_resort_colony_modifier"; }
                if (reason is null && mandatoryRoot.Any(item => item.Key == "has_deposit"))
                { reason = "root_eligibility:planetary_deposit_requirement"; }
                if (reason is null && mandatoryRoot.Any(item => item.Key == "has_country_flag"))
                { reason = "root_eligibility:scenario_country_flag"; }
            }
            if (reason is null && (id.Contains("bio_trophy", StringComparison.Ordinal) || id.Contains("spawning", StringComparison.Ordinal)
                || id.Contains("cosmogenesis", StringComparison.Ordinal)))
            { reason = "definition_id:special_gestalt_or_scenario"; }
            if (reason is null && path == "common/buildings/17_paragon_buildings.txt" && id == "building_the_beholder")
            { reason = "source_event_target:paragon_story_building"; }
            var status = reason is null ? PlanetEconomyDefinitionCoverageStatus.Included : PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific;
            return new(kind, id, status, reason, Provenance(entry));
        }

        private PlanetEconomyJob CompileJob(string id, ScriptEntry definition)
        {
            RequireAssignment(definition);
            var block = definition.AsBlock();
            var jobStrata = new HashSet<string>(StringComparer.Ordinal);
            CollectJobStrata(block, new Dictionary<string, string>(StringComparer.Ordinal), jobStrata, new HashSet<string>(StringComparer.Ordinal));
            if (jobStrata.Count != 1) { throw Error(definition, "ожидалась одна исходная job category, найдено " + jobStrata.Count); }
            var jobStratum = jobStrata.Single();
            var sourceJobTags = block.Optional("job_tags")?.AsBlock().Entries.Select(entry => entry.AsScalar()
                ?? throw Error(entry, "job_tags содержит не scalar tag")).ToArray() ?? Array.Empty<string>();
            var categories = new HashSet<string>(StringComparer.Ordinal);
            CollectJobCategories(block, categories, new HashSet<string>(StringComparer.Ordinal));
            if (categories.Count > 1) { throw Error(definition, $"неоднозначные экономические категории job: {string.Join(",", categories.Order(StringComparer.Ordinal))}"); }
            var category = categories.SingleOrDefault();
            var terms = new List<PlanetEconomyTerm>();
            var omitted = new List<PlanetEconomyProvenance>();
            ExtractJobTerms(block, terms, omitted, null, new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
            var availability = Merge(block.Optional("possible_pre_triggers") is { } preTriggers ? Condition(preTriggers) : null,
                block.Optional("possible") is { } possible ? Condition(possible) : null);
            var availabilityDisposition = Disposition(availability);
            var possiblePrecalculation = OptionalScalar(block, "possible_precalc");
            if (possiblePrecalculation is not null && availabilityDisposition != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext)
            {
                availabilityDisposition = PlanetEconomyConditionDisposition.RequiresRuntimeContext;
            }
            var defaultData = block.Optional("swappable_data")?.AsBlock().Optional("default")?.AsBlock()
                ?? FindInlineJobDefaultData(block, new HashSet<string>(StringComparer.Ordinal));
            var icon = OptionalScalar(defaultData, "icon");
            if (id == "merchant" && icon is null) { icon = "merchant"; }
            var buildingIcon = OptionalScalar(defaultData, "building_icon");
            var conceptId = "concept_" + id;
            var conceptFile = _files[ConceptSource].Root!;
            var concept = conceptFile.Optional(conceptId);
            string? alias = null;
            string? iconPath = null;
            if (concept?.Block is not null)
            {
                var conceptBlock = concept.AsBlock();
                iconPath = OptionalScalar(conceptBlock, "icon");
                alias = conceptBlock.Optional("alias")?.AsBlock().Entries.FirstOrDefault()?.Scalar;
            }
            if (string.IsNullOrWhiteSpace(iconPath) && !string.IsNullOrWhiteSpace(icon))
            {
                iconPath = $"gfx/interface/icons/jobs/job_{icon}.dds";
                alias = "local-job-icon:" + icon;
            }
            var provenance = Provenance(definition);
            return new PlanetEconomyJob(id, jobStratum, sourceJobTags, category, icon, buildingIcon, concept?.Key, alias, iconPath, terms, omitted,
                availability, availabilityDisposition, possiblePrecalculation, provenance, OptionalScalar(defaultData, "name"), OptionalScalar(defaultData, "desc"));
        }

        private void ExtractJobTerms(ScriptBlock block, List<PlanetEconomyTerm> target, List<PlanetEconomyProvenance> omitted,
            PlanetEconomyCondition? inherited, IReadOnlyDictionary<string, string> parameters, HashSet<string> stack)
        {
            foreach (var entry in block.Entries)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (entry.Key == "resources" && entry.Block is not null)
                {
                    ExtractJobTerms(entry.AsBlock(), target, omitted, inherited, parameters, stack);
                }
                else if (entry.Key is "produces" or "upkeep")
                {
                    RequireAssignment(entry);
                    var amountBlock = entry.AsBlock();
                    var trigger = amountBlock.Optional("trigger");
                    var condition = Merge(inherited, trigger is null ? null : Condition(trigger, parameters));
                    var disposition = Disposition(condition);
                    if (disposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext) { omitted.Add(Provenance(entry)); }
                    var resourceAmounts = amountBlock.Entries.Where(item => item.Key is not "trigger" and not "inline_script" and not "mult" and not "multiplier").ToArray();
                    foreach (var amount in resourceAmounts)
                    {
                        RequireAssignment(amount);
                    var value = NumberOrDynamicExpression(amount, parameters, out var expression);
                        var targetId = ResolveToken(amount.Key, parameters);
                        target.Add(new(entry.Key == "produces" ? PlanetEconomyTermKind.ResourceProduction : PlanetEconomyTermKind.ResourceUpkeep,
                            targetId, value, amount.Key, expression, disposition, condition, Provenance(amount)));
                    }
                    foreach (var multiplier in amountBlock.Named("mult"))
                    {
                        RequireAssignment(multiplier);
                        foreach (var amount in resourceAmounts)
                        {
                            target.Add(new(PlanetEconomyTermKind.ResourceMultiplier, ResolveToken(amount.Key, parameters), null, multiplier.Key,
                                multiplier.AsScalar(), disposition, condition, Provenance(multiplier)));
                        }
                    }
                    foreach (var multiplier in amountBlock.Named("multiplier"))
                    {
                        RequireAssignment(multiplier);
                        foreach (var amount in resourceAmounts)
                        {
                            var value = NumberOrDynamicExpression(multiplier, parameters, out var expression);
                            target.Add(new(PlanetEconomyTermKind.ResourceMultiplier, ResolveToken(amount.Key, parameters), value, multiplier.Key,
                                expression, disposition, condition, Provenance(multiplier)));
                        }
                    }
                    foreach (var inline in amountBlock.Named("inline_script")) { ExtractInlineResourceTerms(inline, target, omitted, condition, parameters, stack); }
                }
                else if (entry.Key == "inline_script")
                {
                    ExtractInlineResourceTerms(entry, target, omitted, inherited, parameters, stack);
                }
                else if (entry.Key == "planet_modifier" && entry.Block is not null)
                {
                    AddModifierTerms(entry.AsBlock(), PlanetEconomyDefinitionKind.Building, target, inherited, parameters, PlanetEconomyApplicationScope.PerDefinitionInstance);
                }
                else if (entry.Key == "triggered_planet_modifier" && entry.Block is not null)
                {
                    var branch = entry.AsBlock();
                    var potential = branch.Optional("potential") ?? branch.Optional("trigger");
                    var condition = Merge(inherited, potential is null ? null : Condition(potential, parameters));
                    if (Disposition(condition) == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext) { omitted.Add(Provenance(entry)); }
                    foreach (var nested in branch.Entries.Where(item => item.Key is not "potential" and not "trigger"))
                    {
                        if (nested.Key == "inline_script")
                        {
                            ExtractInlineResourceTerms(nested, target, omitted, condition, parameters, stack);
                        }
                        else if (nested.Block is not null)
                        {
                            AddModifierTerms(nested.AsBlock(), PlanetEconomyDefinitionKind.Building, target, condition, parameters, PlanetEconomyApplicationScope.PerDefinitionInstance);
                        }
                    }
                    var direct = branch.Entries.Where(item => item.Key is not "potential" and not "trigger" and not "inline_script" && item.Block is null);
                    AddModifierTerms(new ScriptBlock(direct, SourceOf(branch), 1, 1), PlanetEconomyDefinitionKind.Building, target, condition, parameters, PlanetEconomyApplicationScope.PerDefinitionInstance);
                }
            }
        }

        private void ExtractInlineResourceTerms(ScriptEntry inlineEntry, List<PlanetEconomyTerm> target, List<PlanetEconomyProvenance> omitted,
            PlanetEconomyCondition? inherited, IReadOnlyDictionary<string, string> parameters, HashSet<string> stack)
        {
            var (name, args) = InlineCall(inlineEntry, parameters);
            if (!stack.Add(name)) { throw Error(inlineEntry, "цикл inline script: " + name); }
            try
            {
                var expanded = MergeParameters(parameters, args);
                ExtractJobTerms(_inlineScripts[name], target, omitted, inherited, expanded, stack);
            }
            finally { stack.Remove(name); }
        }

        private PlanetEconomyDefinition CompileDefinition(PlanetEconomyDefinitionKind kind, string id, ScriptEntry definition)
        {
            RequireAssignment(definition);
            var block = definition.AsBlock();
            var category = OptionalScalar(block.Optional("resources")?.AsBlock(), "category");
            var terms = new List<PlanetEconomyTerm>();
            ExtractConstructionTerms(block, terms);
            WalkDefinition(block, kind, terms, null, new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
            var properties = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var fieldKeys = new[] { "name", "shared_capacity_modifier", "max_buildings", "max_districts", "max_for_deposits_on_planet", "min_for_deposits_on_planet", "planet_limit", "empire_limit", "capital_tier", "max_level", "category", "is_uncapped", "can_build", "can_demolish", "can_be_ruined", "can_be_disabled", "is_essential", "position_priority", "max_buildings_planet_class", "swap_type", "swap_type_weight", "cost", "time", "build_time", "base_buildtime", "potential", "possible", "possible_precalc", "allow", "unlock", "prerequisites", "upgrades", "icon", "show_tech_unlock_if", "destroy_if", "convert_to", "planet_class", "zone_sets", "building_sets", "included_building_sets", "excluded_building_sets", "include", "exclude", "tech_required" };
            var sourcePropertyKeys = fieldKeys.Concat(block.Entries.Where(item => item.Block is null).Select(item => item.Key)).Distinct(StringComparer.Ordinal);
            foreach (var key in sourcePropertyKeys)
            {
                var values = block.Named(key).SelectMany(FlattenScalars).ToArray();
                if (values.Length > 0) { properties.Add(key, Array.AsReadOnly(values)); }
            }
            var sourceFields = fieldKeys.ToDictionary(key => key,
                key => (IReadOnlyList<PlanetEconomyCondition>)Array.AsReadOnly(block.Named(key).Select(SourceTree).ToArray()), StringComparer.Ordinal);
            foreach (var key in new[] { "country_modifier", "triggered_country_modifier" })
                sourceFields[key] = Array.AsReadOnly(block.Named(key).Select(SourceTree).ToArray());
            var resourceCostFields = block.Optional("resources")?.AsBlock().Named("cost").Select(SourceTree).ToArray() ?? Array.Empty<PlanetEconomyCondition>();
            if (resourceCostFields.Length > 0) { sourceFields["resource_cost"] = Array.AsReadOnly(resourceCostFields); }
            var resourceProducesFields = block.Optional("resources")?.AsBlock().Named("produces").Select(SourceTree).ToArray() ?? Array.Empty<PlanetEconomyCondition>();
            if (resourceProducesFields.Length > 0) { sourceFields["resource_produces"] = Array.AsReadOnly(resourceProducesFields); }
            var availability = Merge(block.Optional("potential") is { } potential ? Condition(potential) : null,
                Merge(block.Optional("allow") is { } allow ? Condition(allow) : null,
                    block.Optional("possible") is { } possible ? Condition(possible) : null));
            var availabilityDisposition = DefinitionDisposition(availability);
            return new(kind, id, category,
                SetValues(block, "building_sets"), SetValues(block, "zone_sets"), SetValues(block, "included_building_sets"),
                SetValues(block, "excluded_building_sets"), SetValues(block, "include"), SetValues(block, "exclude"), terms,
                properties, sourceFields, availability, availabilityDisposition, OptionalScalar(block, "name"), OptionalScalar(block, "icon"), Provenance(definition));
        }

        private void ExtractConstructionTerms(ScriptBlock block, List<PlanetEconomyTerm> target)
        {
            foreach (var cost in block.Named("cost"))
            {
                RequireAssignment(cost);
                if (cost.Block is null) { target.Add(new(PlanetEconomyTermKind.ConstructionCost, "source_scalar", null, cost.Key, cost.AsScalar(), PlanetEconomyConditionDisposition.RequiresRuntimeContext, null, Provenance(cost))); }
            }
            foreach (var key in new[] { "time", "build_time", "base_buildtime" })
            {
                foreach (var time in block.Named(key))
                {
                    var value = NumberOrDynamicExpression(time, new Dictionary<string, string>(StringComparer.Ordinal), out var expression);
                    target.Add(new(PlanetEconomyTermKind.ConstructionTime, "days", value, key, expression, PlanetEconomyConditionDisposition.Unconditional, null, Provenance(time)));
                }
            }
        }

        private PlanetEconomyCondition SourceTree(ScriptEntry entry)
        {
            var children = entry.Block is null ? Array.Empty<PlanetEconomyCondition>() : entry.AsBlock().Entries.Select(SourceTree).ToArray();
            return new PlanetEconomyCondition(entry.Key, entry.Operator, entry.Scalar, children, Provenance(entry));
        }

        private PlanetEconomyConditionDisposition DefinitionDisposition(PlanetEconomyCondition? condition)
        {
            if (condition is null) { return PlanetEconomyConditionDisposition.Unconditional; }
            if (Descendants(condition).Any(item => item.Key is "has_valid_civic" or "has_ethic" or "has_technology" or "has_technology_researched"
                or "has_tradition" or "has_ascension_perk" or "has_planet_class"))
            { return PlanetEconomyConditionDisposition.RequiresRuntimeContext; }
            return Disposition(condition);
        }

        private void WalkDefinition(ScriptBlock block, PlanetEconomyDefinitionKind kind, List<PlanetEconomyTerm> target,
            PlanetEconomyCondition? inherited, IReadOnlyDictionary<string, string> parameters, HashSet<string> stack,
            PlanetEconomyApplicationScope applicationScope = PlanetEconomyApplicationScope.PerDefinitionInstance)
        {
            foreach (var entry in block.Entries)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (entry.Key == "inline_script")
                {
                    var (name, args) = InlineCall(entry, parameters);
                    if (!stack.Add(name)) { throw Error(entry, "цикл inline script: " + name); }
                    try { WalkDefinition(_inlineScripts[name], kind, target, inherited, MergeParameters(parameters, args), stack, applicationScope); }
                    finally { stack.Remove(name); }
                    continue;
                }
                if (entry.Key == "resources" && entry.Block is not null)
                {
                    WalkResourceDefinition(entry.AsBlock(), kind, target, inherited, parameters, stack, applicationScope);
                    continue;
                }
                if (entry.Key == "cost" && entry.Block is not null)
                {
                    WalkResourceDefinition(new ScriptBlock([entry], entry.Source, entry.Line, entry.Column), kind, target, inherited, parameters, stack, applicationScope);
                    continue;
                }
                if (entry.Key == "planet_modifier" && entry.Block is not null)
                {
                    AddModifierTerms(entry.AsBlock(), kind, target, inherited, parameters, applicationScope);
                    continue;
                }
                if ((entry.Key is "triggered_planet_modifier" or "triggered_district_planet_modifier") && entry.Block is not null)
                {
                    var branch = entry.AsBlock();
                    var nestedScope = kind == PlanetEconomyDefinitionKind.Zone && entry.Key == "triggered_district_planet_modifier"
                        ? PlanetEconomyApplicationScope.PerAttachedDistrictLevel : applicationScope;
                    var potential = branch.Optional("potential") ?? branch.Optional("trigger");
                    var condition = Merge(inherited, potential is null ? null : Condition(potential, parameters));
                    var modifiers = branch.Optional("modifier");
                    if (modifiers?.Block is not null) { AddModifierTerms(modifiers.AsBlock(), kind, target, condition, parameters, nestedScope); }
                    foreach (var nested in branch.Entries.Where(item => item.Key is not "potential" and not "trigger" and not "modifier"))
                    {
                        if (nested.Block is not null) { WalkDefinition(nested.AsBlock(), kind, target, condition, parameters, stack, nestedScope); }
                    }
                    var direct = branch.Entries.Where(item => item.Key is not "potential" and not "trigger" and not "modifier" && item.Block is null);
                    AddModifierTerms(new ScriptBlock(direct, SourceOf(branch), 1, 1), kind, target, condition, parameters, nestedScope);
                    continue;
                }
                if (TryJobCapacity(entry, parameters, inherited, applicationScope, out var capacity))
                {
                    target.Add(capacity);
                    continue;
                }
                if (entry.Block is not null && entry.Key is "trigger" or "if" or "else" or "limit")
                {
                    if (entry.Key == "if")
                    {
                        var branch = entry.AsBlock();
                        var limit = branch.Optional("limit");
                        var condition = Merge(inherited, limit is null ? null : Condition(limit, parameters));
                        foreach (var nested in branch.Entries.Where(item => item.Key != "limit"))
                        {
                            if (nested.Block is not null) { WalkDefinition(nested.AsBlock(), kind, target, condition, parameters, stack, applicationScope); }
                        }
                        var direct = branch.Entries.Where(item => item.Key != "limit" && item.Block is null);
                        AddModifierTerms(new ScriptBlock(direct, SourceOf(branch), 1, 1), kind, target, condition, parameters, applicationScope);
                    }
                    else
                    {
                        var condition = entry.Key == "trigger" ? Merge(inherited, Condition(entry, parameters)) : inherited;
                        WalkDefinition(entry.AsBlock(), kind, target, condition, parameters, stack, applicationScope);
                    }
                }
            }
        }

        private void WalkResourceDefinition(ScriptBlock block, PlanetEconomyDefinitionKind kind, List<PlanetEconomyTerm> target,
            PlanetEconomyCondition? inherited, IReadOnlyDictionary<string, string> parameters, HashSet<string> stack,
            PlanetEconomyApplicationScope applicationScope)
        {
            foreach (var entry in block.Entries)
            {
                if (entry.Key is "cost" or "upkeep" or "produces")
                {
                    RequireAssignment(entry);
                    var table = entry.AsBlock();
                    var trigger = table.Optional("trigger");
                    var condition = Merge(inherited, trigger is null ? null : Condition(trigger, parameters));
                    var termKind = entry.Key switch
                    {
                        "produces" => PlanetEconomyTermKind.ResourceProduction,
                        "cost" => PlanetEconomyTermKind.ConstructionCost,
                        _ => kind switch
                        {
                            PlanetEconomyDefinitionKind.Building => PlanetEconomyTermKind.BuildingUpkeep,
                            PlanetEconomyDefinitionKind.District => PlanetEconomyTermKind.DistrictUpkeep,
                            _ => PlanetEconomyTermKind.ZoneUpkeep
                        }
                    };
                    var resourceAmounts = table.Entries.Where(item => item.Key is not "trigger" and not "inline_script" and not "mult" and not "multiplier").ToArray();
                    foreach (var amount in resourceAmounts)
                    {
                        RequireAssignment(amount);
                        var value = NumberOrDynamicExpression(amount, parameters, out var expression);
                        target.Add(new(termKind, ResolveToken(amount.Key, parameters), value, amount.Key, expression, Disposition(condition), condition, Provenance(amount), applicationScope));
                    }
                    foreach (var multiplier in table.Named("mult"))
                    {
                        RequireAssignment(multiplier);
                        foreach (var amount in resourceAmounts)
                        {
                            target.Add(new(termKind == PlanetEconomyTermKind.ConstructionCost ? PlanetEconomyTermKind.ConstructionCostMultiplier : PlanetEconomyTermKind.ResourceMultiplier, ResolveToken(amount.Key, parameters), null, multiplier.Key,
                                multiplier.AsScalar(), Disposition(condition), condition, Provenance(multiplier), applicationScope));
                        }
                    }
                    foreach (var multiplier in table.Named("multiplier"))
                    {
                        RequireAssignment(multiplier);
                        foreach (var amount in resourceAmounts)
                        {
                            var value = NumberOrDynamicExpression(multiplier, parameters, out var expression);
                            target.Add(new(termKind == PlanetEconomyTermKind.ConstructionCost ? PlanetEconomyTermKind.ConstructionCostMultiplier : PlanetEconomyTermKind.ResourceMultiplier, ResolveToken(amount.Key, parameters), value, multiplier.Key,
                                expression, Disposition(condition), condition, Provenance(multiplier), applicationScope));
                        }
                    }
                    foreach (var inline in table.Named("inline_script"))
                    {
                        var (name, args) = InlineCall(inline, parameters);
                        if (!stack.Add(name)) { throw Error(inline, "цикл inline script: " + name); }
                        try { WalkDefinition(_inlineScripts[name], kind, target, condition, MergeParameters(parameters, args), stack, applicationScope); }
                        finally { stack.Remove(name); }
                    }
                }
                else if (entry.Key == "inline_script")
                {
                    var (name, args) = InlineCall(entry, parameters);
                    if (!stack.Add(name)) { throw Error(entry, "цикл inline script: " + name); }
                    try { WalkDefinition(_inlineScripts[name], kind, target, inherited, MergeParameters(parameters, args), stack, applicationScope); }
                    finally { stack.Remove(name); }
                }
            }
        }

        private void AddModifierTerms(ScriptBlock block, PlanetEconomyDefinitionKind kind, List<PlanetEconomyTerm> target,
            PlanetEconomyCondition? condition, IReadOnlyDictionary<string, string> parameters, PlanetEconomyApplicationScope applicationScope)
        {
            foreach (var entry in block.Entries)
            {
                if (entry.Block is not null) { continue; }
                AddModifierEntry(entry, target, condition, parameters, applicationScope);
            }
            var modifiers = block.Entries.Where(entry => entry.Key.StartsWith("planet_", StringComparison.Ordinal) && entry.Key != "mult").ToArray();
            foreach (var multiplier in block.Named("mult"))
            {
                RequireAssignment(multiplier);
                foreach (var modifier in modifiers)
                {
                    target.Add(new(PlanetEconomyTermKind.PlanetModifierMultiplier, ResolveToken(modifier.Key, parameters), null, multiplier.Key,
                        multiplier.AsScalar(), Disposition(condition), condition, Provenance(multiplier), applicationScope));
                }
            }
            var capacityFields = block.Entries.Where(entry => entry.Key.StartsWith("job_", StringComparison.Ordinal)
                && (entry.Key.EndsWith("_add", StringComparison.Ordinal) || entry.Key.EndsWith("_mult", StringComparison.Ordinal))
                && entry.Block is null).ToArray();
            foreach (var multiplier in block.Named("mult"))
            {
                RequireAssignment(multiplier);
                var multiplierValue = IsNumberToken(multiplier.AsScalar()) ? Number(multiplier, parameters) : (decimal?)null;
                foreach (var capacityField in capacityFields)
                {
                    var suffixLength = capacityField.Key.EndsWith("_add", StringComparison.Ordinal) ? 4 : 5;
                    var boundKey = ResolveToken(capacityField.Key, parameters);
                    var capacityId = boundKey[4..^suffixLength];
                    target.Add(new(PlanetEconomyTermKind.WorkforceCapacityMultiplier, capacityId, multiplierValue, multiplier.Key,
                        multiplierValue is null ? multiplier.AsScalar() : null, Disposition(condition), condition, Provenance(multiplier), applicationScope));
                }
            }
        }

        private void AddModifierEntry(ScriptEntry entry, List<PlanetEconomyTerm> target,
            PlanetEconomyCondition? condition, IReadOnlyDictionary<string, string> parameters, PlanetEconomyApplicationScope applicationScope)
        {
            if (entry.Operator != "=" || entry.Scalar is null) return;
            if (IsRareIndustrialComposite(entry.Key))
            {
                var composite = _files[RareCompositeSource].Root!.Named(entry.Key).ToArray();
                if (composite is not [var selected] || selected.Operator != "=" || selected.Block is null) { throw Error(entry, "Missing/ambiguous rare industrial composite."); }
                var scale = Number(entry, parameters);
                var children = selected.AsBlock().Entries.Where(child => child.Key != "custom_tooltip").ToArray();
                if (children.Length != 2 || children.Select(child => child.Key).Distinct(StringComparer.Ordinal).Count() != 2) { throw Error(entry, "Unsupported rare composite shape."); }
                var resource = entry.Key["industrial_jobs_".Length..^"_produces_add".Length];
                foreach (var child in children)
                {
                    if (child.Operator != "=" || child.Block is not null || child.Key != "planet_metallurgists_" + resource + "_produces_add" && child.Key != "planet_artisans_" + resource + "_produces_add") { throw Error(child, "Unsupported rare composite leaf."); }
                    var coefficient = Number(child, parameters);
                    // SourceExpression хранит связь с исходным composite; File snapshot содержит его closure.
                    target.Add(new(PlanetEconomyTermKind.PlanetModifier, child.Key, checked(scale * coefficient), child.Key, entry.Key, Disposition(condition), condition, Provenance(child), applicationScope));
                }
                return;
            }
            if (TryJobCapacity(entry, parameters, condition, applicationScope, out var capacity))
            {
                target.Add(capacity);
                return;
            }
            if (entry.Key == "mult") return;
            var value = IsNumberToken(entry.Scalar) ? Number(entry, parameters) : (decimal?)null;
            target.Add(new(PlanetEconomyTermKind.PlanetModifier, ResolveToken(entry.Key, parameters), value, entry.Key,
                value is null ? entry.Scalar : null, Disposition(condition), condition, Provenance(entry), applicationScope));
        }

        private static bool HasRareComposite(ScriptEntry entry) => IsRareIndustrialComposite(entry.Key)
            || entry.Block is not null && entry.Block.Entries.Any(HasRareComposite);

        private static string SourceOf(ScriptBlock block) => block.Entries.FirstOrDefault()?.Source ?? "<empty>";

        private bool TryJobCapacity(ScriptEntry entry, IReadOnlyDictionary<string, string> parameters,
            PlanetEconomyCondition? condition, PlanetEconomyApplicationScope applicationScope, out PlanetEconomyTerm term)
        {
            term = null!;
            if (!entry.Key.StartsWith("job_", StringComparison.Ordinal) || entry.Operator != "=") { return false; }
            var suffix = entry.Key.EndsWith("_add", StringComparison.Ordinal) ? 4
                : entry.Key.EndsWith("_mult", StringComparison.Ordinal) ? 5 : 0;
            if (suffix == 0 || entry.Block is not null) { return false; }
            var boundKey = ResolveToken(entry.Key, parameters);
            var id = boundKey[4..^suffix];
            var kind = suffix == 4 ? PlanetEconomyTermKind.WorkforceCapacity : PlanetEconomyTermKind.WorkforceCapacityMultiplier;
            term = new(kind, id, Number(entry, parameters), entry.Key, null, Disposition(condition), condition, Provenance(entry), applicationScope);
            return true;
        }

        private PlanetEconomyResource CompileResource(string id, ScriptBlock resourceRoot)
        {
            var entries = resourceRoot.Named(id).ToArray();
            if (entries.Length != 1) { throw new PlanetEconomyImportException($"Ожидалось одно определение ресурса {id}, найдено {entries.Length}."); }
            var entry = entries[0];
            RequireAssignment(entry);
            var block = entry.AsBlock();
            var category = RequiredScalar(block, "category");
            var max = block.Optional("max");
            decimal? maximum = null;
            var policy = PlanetEconomyStoragePolicy.Unspecified;
            if (max is not null)
            {
                RequireAssignment(max);
                maximum = Number(max, new Dictionary<string, string>(StringComparer.Ordinal));
                if (maximum < 0) { throw Error(max, "negative resource maximum"); }
                policy = PlanetEconomyStoragePolicy.SourceCapped;
            }
            else if (UnlimitedResources.Contains(id)) { policy = PlanetEconomyStoragePolicy.UnlimitedReserve; }
            var fixedMaximum = ReadMaximumFlag("fixed_max_amount");
            var specialMaximum = ReadMaximumFlag("special_max_amount");
            if (fixedMaximum && specialMaximum) { throw Error(entry, "fixed and special resource maximum flags are mutually exclusive"); }
            var maximumKind = fixedMaximum ? PlanetEconomyMaximumKind.Fixed
                : specialMaximum ? PlanetEconomyMaximumKind.Special : PlanetEconomyMaximumKind.Generic;
            var dynamicCapacity = block.Optional("dynamic_capacity");
            if (dynamicCapacity is not null) { RequireAssignment(dynamicCapacity); }
            return new PlanetEconomyResource(id, category, policy, maximum, Provenance(entry), maximumKind,
                dynamicCapacity is null ? null : SourceTree(dynamicCapacity));

            bool ReadMaximumFlag(string key)
            {
                var flag = block.Optional(key);
                if (flag is null) { return false; }
                if (flag.Operator != "=" || flag.Block is not null || flag.Scalar is not ("yes" or "no"))
                { throw Error(flag, "resource maximum flag must be one scalar yes/no"); }
                return flag.Scalar == "yes";
            }
        }

        private void CollectJobCategories(ScriptBlock block, HashSet<string> categories, HashSet<string> inlineStack)
        {
            foreach (var entry in block.Entries)
            {
                if (entry.Key == "resources" && entry.Block is not null)
                {
                    categories.Add(RequiredScalar(entry.AsBlock(), "category"));
                }
                else if (entry.Key == "inline_script")
                {
                    var (name, _) = InlineCall(entry, new Dictionary<string, string>(StringComparer.Ordinal));
                    if (!inlineStack.Add(name)) throw Error(entry, "цикл inline script при поиске job category: " + name);
                    try { CollectJobCategories(_inlineScripts[name], categories, inlineStack); }
                    finally { inlineStack.Remove(name); }
                }
                else if (entry.Block is not null && entry.Key is not "overlord_resources")
                {
                    CollectJobCategories(entry.AsBlock(), categories, inlineStack);
                }
            }
        }

        private void CollectJobStrata(ScriptBlock block, IReadOnlyDictionary<string, string> parameters, HashSet<string> strata, HashSet<string> inlineStack)
        {
            foreach (var entry in block.Entries)
            {
                if (entry.Key == "category" && entry.Block is null)
                {
                    strata.Add(ResolveToken(entry.AsScalar(), parameters));
                }
                else if (entry.Key == "inline_script")
                {
                    var (name, args) = InlineCall(entry, parameters);
                    if (!inlineStack.Add(name)) { throw Error(entry, "цикл inline script при поиске job stratum: " + name); }
                    try { CollectJobStrata(_inlineScripts[name], MergeParameters(parameters, args), strata, inlineStack); }
                    finally { inlineStack.Remove(name); }
                }
            }
        }

        private ScriptBlock? FindInlineJobDefaultData(ScriptBlock block, HashSet<string> inlineStack)
        {
            foreach (var entry in block.Entries.Where(item => item.Key == "inline_script"))
            {
                var (name, _) = InlineCall(entry, new Dictionary<string, string>(StringComparer.Ordinal));
                if (!inlineStack.Add(name)) { throw Error(entry, "цикл inline script при поиске job icon metadata: " + name); }
                try
                {
                    var source = _inlineScripts[name];
                    var defaultData = source.Optional("swappable_data")?.AsBlock().Optional("default")?.AsBlock();
                    if (defaultData is not null) { return defaultData; }
                    var nested = FindInlineJobDefaultData(source, inlineStack);
                    if (nested is not null) { return nested; }
                }
                finally { inlineStack.Remove(name); }
            }
            return null;
        }

        private async Task LoadVariablesAsync()
        {
            var directory = Resolve("common/scripted_variables");
            if (!Directory.Exists(directory)) { throw new PlanetEconomyImportException("Required scripted variable directory is missing."); }
            var files = EnumerateSourceFiles(directory).Where(path => Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) { throw new PlanetEconomyImportException("Required scripted variable sources are empty."); }
            foreach (var full in files)
            {
                _cancellation.ThrowIfCancellationRequested();
                var source = await LoadTextAsync(Path.GetRelativePath(_root, full).Replace('\\', '/'));
                if (!source.Path.StartsWith("common/scripted_variables/", StringComparison.Ordinal)) { continue; }
                foreach (var variable in source.Root!.Entries.Where(entry => entry.Key.StartsWith('@')))
                {
                    if (variable.Operator != "=" || variable.Scalar is null || !_globalVariables.TryAdd(variable.Key, variable))
                    {
                        throw Error(variable, "malformed or ambiguous global scripted variable");
                    }
                }
            }
        }

        private async Task LoadInlineClosureAsync(IEnumerable<ScriptEntry> roots)
        {
            var queue = new Queue<string>();
            foreach (var root in roots) { EnqueueInlineNames(root.AsBlock(), queue); }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (queue.TryDequeue(out var name))
            {
                _cancellation.ThrowIfCancellationRequested();
                if (!visited.Add(name)) { continue; }
                if (_inlineScripts.ContainsKey(name)) { continue; }
                var path = InlinePath(name);
                var source = await LoadTextAsync(path);
                _inlineScripts.Add(name, source.Root!);
                EnqueueInlineNames(source.Root!, queue);
            }
        }

        private async Task LoadVersionAsync()
        {
            var relative = "launcher-settings.json";
            var bytes = await ReadBytesAsync(relative);
            try
            {
                using var json = JsonDocument.Parse(bytes);
                var root = json.RootElement;
                var matches = root.EnumerateObject().Where(item => item.NameEquals("rawVersion")).ToArray();
                if (matches.Length != 1 || matches[0].Value.ValueKind != JsonValueKind.String || matches[0].Value.GetString() != "v4.5.2")
                {
                    throw new PlanetEconomyImportException("Exact Stellaris rawVersion v4.5.2 is required.");
                }
                _version = matches[0].Value.GetString()!;
                _files.Add(relative, new SourceFile(relative, bytes, Hash(bytes), null));
            }
            catch (JsonException error) { throw new PlanetEconomyImportException("Malformed launcher version metadata.", error); }
        }

        private async Task<SourceFile> LoadTextAsync(string relative)
        {
            relative = Normalize(relative);
            if (_files.TryGetValue(relative, out var existing)) { return existing; }
            var bytes = await ReadBytesAsync(relative);
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException error) { throw new PlanetEconomyImportException($"Source is not UTF-8: {relative}.", error); }
            ScriptBlock ast;
            try { ast = ParadoxParser.Parse(text, relative); }
            catch (FormatException error) { throw new PlanetEconomyImportException($"Malformed required source {relative}: {error.Message}", error); }
            var file = new SourceFile(relative, bytes, Hash(bytes), ast);
            _files.Add(relative, file);
            return file;
        }

        private async Task<byte[]> ReadBytesAsync(string relative)
        {
            var path = Resolve(relative);
            if (!File.Exists(path)) { throw new PlanetEconomyImportException("Required source is missing: " + relative); }
            ValidateNoReparsePath(path, relative);
            var bytes = await ReadBoundedAsync(path, relative);
            _totalBytes = checked(_totalBytes + bytes.LongLength);
            if (_totalBytes > TotalLimit) { throw new PlanetEconomyImportException("Economy source closure exceeds total byte budget."); }
            return bytes;
        }

        private async Task<byte[]> ReadBoundedAsync(string fullPath, string relative)
        {
            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length < 0 || stream.Length > FileLimit) throw new PlanetEconomyImportException("Source exceeds per-file budget: " + relative);
            using var buffer = new MemoryStream((int)stream.Length);
            var chunk = new byte[64 * 1024];
            while (true)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(), _cancellation);
                if (read == 0) break;
                if (buffer.Length + read > FileLimit) throw new PlanetEconomyImportException("Source exceeds per-file budget: " + relative);
                await buffer.WriteAsync(chunk.AsMemory(0, read), _cancellation);
            }
            return buffer.ToArray();
        }

        private async Task VerifyStableAsync()
        {
            foreach (var file in _files.Values)
            {
                _cancellation.ThrowIfCancellationRequested();
                byte[] current;
                try
                {
                    var path = Resolve(file.Path);
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Source reparse points are unsupported.");
                    current = await ReadBoundedAsync(path, file.Path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { throw new PlanetEconomyImportException("Required source disappeared during import: " + file.Path, error); }
                if (current.LongLength != file.Bytes.LongLength || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), Convert.FromHexString(file.Sha256)))
                {
                    throw new PlanetEconomyImportException("Source changed during import: " + file.Path);
                }
            }
        }

        private ScriptEntry FindUnique(string id, IEnumerable<string> paths)
        {
            var entries = paths.Distinct(StringComparer.Ordinal).SelectMany(path => _files[path].Root!.Named(id)).ToArray();
            if (entries.Length != 1) { throw new PlanetEconomyImportException($"Expected one source definition '{id}', found {entries.Length}."); }
            RequireAssignment(entries[0]);
            _ = entries[0].AsBlock();
            return entries[0];
        }

        private ScriptEntry? FindOptionalUnique(string id, IEnumerable<string> paths)
        {
            var entries = paths.Distinct(StringComparer.Ordinal).SelectMany(path => _files[path].Root!.Named(id)).ToArray();
            if (entries.Length > 1) { throw new PlanetEconomyImportException($"Ambiguous source job definition '{id}', found {entries.Length}."); }
            if (entries.Length == 0) { return null; }
            RequireAssignment(entries[0]);
            _ = entries[0].AsBlock();
            return entries[0];
        }

        private PlanetEconomyCondition? Condition(ScriptEntry trigger, IReadOnlyDictionary<string, string>? parameters = null)
        {
            RequireAssignment(trigger);
            return new PlanetEconomyCondition("AND", "=", null, trigger.AsBlock().Entries.Select(entry => ConvertCondition(entry, parameters)), Provenance(trigger));
        }

        private PlanetEconomyCondition ConvertCondition(ScriptEntry entry, IReadOnlyDictionary<string, string>? parameters = null)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (parameters is not null && InlineTriggerFragment.TryExpand(entry, parameters, out var fragment))
            {
                // Wrapper хранит native placeholder origin, дочернее условие —
                // actual аргумент вызывающего definition, без synthetic line1.
                return new PlanetEconomyCondition("AND", "=", null, [ConvertCondition(fragment, parameters)], Provenance(entry));
            }
            if (entry.Block is not null)
            {
                RequireAssignment(entry);
                return new PlanetEconomyCondition(entry.Key, entry.Operator, null, entry.AsBlock().Entries.Select(child => ConvertCondition(child, parameters)), Provenance(entry));
            }
            return new PlanetEconomyCondition(entry.Key, entry.Operator, entry.Scalar, Array.Empty<PlanetEconomyCondition>(), Provenance(entry));
        }

        private PlanetEconomyCondition? Merge(PlanetEconomyCondition? parent, PlanetEconomyCondition? child)
        {
            if (parent is null) { return child; }
            if (child is null) { return parent; }
            return new PlanetEconomyCondition("AND", "=", null, [parent, child], parent.Provenance);
        }

        private PlanetEconomyConditionDisposition Disposition(PlanetEconomyCondition? condition)
        {
            if (condition is null) { return PlanetEconomyConditionDisposition.Unconditional; }
            var result = EvaluateCondition(condition);
            return result switch
            {
                true => PlanetEconomyConditionDisposition.ActiveInOrdinaryContext,
                false => PlanetEconomyConditionDisposition.InactiveInOrdinaryContext,
                null => PlanetEconomyConditionDisposition.RequiresRuntimeContext
            };
        }

        private bool? EvaluateCondition(PlanetEconomyCondition condition)
        {
            if (condition.Key == "AND") return All(condition.Children);
            if (condition.Key == "OR") return Any(condition.Children);
            if (condition.Key is "NOT" or "NOR")
            {
                var value = condition.Key == "NOT" ? All(condition.Children) : Any(condition.Children);
                return value is null ? null : !value.Value;
            }
            if (condition.Key is "owner" or "owner?" or "planet" or "planet?" or "colony" or "colony?" or "hidden_trigger") return All(condition.Children);
            if (condition.Key == "exists") return condition.Value is "owner" or "planet" or "colony";
            if (condition.Operator == "=" && condition.Value is not ("yes" or "no"))
            {
                if (condition.Key is "has_country_flag" or "has_planet_flag" or "has_modifier" or "has_valid_civic" or "has_active_tradition") return false;
                if (condition.Key == "has_origin") return condition.Value == "origin_default";
            }
            if (condition.Operator != "=" || condition.Value is not ("yes" or "no")) return null;
            bool? actual = condition.Key switch
            {
                "always" => true,
                "is_regular_empire" => true,
                "is_gestalt" or "is_robot_empire" or "is_nomadic" or "is_wilderness_empire" or "is_fallen_empire" => false,
                "planet_is_habitat_equivalent" or "planet_is_ecu_equivalent" or "planet_is_ring_world_equivalent" => false,
                "has_owner" => true,
                _ when OrdinaryFalseConditionPrefixes.Any(prefix => condition.Key.StartsWith(prefix, StringComparison.Ordinal)) => false,
                _ => null
            };
            return actual is null ? null : condition.Value == "yes" ? actual : !actual.Value;
        }

        private bool? All(IEnumerable<PlanetEconomyCondition> children)
        {
            var unknown = false;
            foreach (var child in children)
            {
                var value = EvaluateCondition(child);
                if (value == false) return false;
                if (value is null) unknown = true;
            }
            return unknown ? null : true;
        }

        private bool? Any(IEnumerable<PlanetEconomyCondition> children)
        {
            var unknown = false;
            foreach (var child in children)
            {
                var value = EvaluateCondition(child);
                if (value == true) return true;
                if (value is null) unknown = true;
            }
            return unknown ? null : false;
        }

        private decimal Number(ScriptEntry entry, IReadOnlyDictionary<string, string> parameters)
        {
            RequireAssignment(entry);
            return ResolveNumber(entry.AsScalar(), entry.Source, parameters, new HashSet<string>(StringComparer.Ordinal));
        }

        private decimal? NumberOrDynamicExpression(ScriptEntry entry, IReadOnlyDictionary<string, string> parameters, out string? expression)
        {
            var raw = entry.AsScalar();
            if (raw.StartsWith("value:", StringComparison.Ordinal) || raw.StartsWith("owner.", StringComparison.Ordinal)
                || raw.StartsWith("planet.", StringComparison.Ordinal) || raw.StartsWith("colony.value:", StringComparison.Ordinal)
                || raw.Contains("|", StringComparison.Ordinal))
            {
                expression = raw;
                return null;
            }
            expression = null;
            return Number(entry, parameters);
        }

        private decimal ResolveNumber(string raw, string source, IReadOnlyDictionary<string, string> parameters, HashSet<string> stack)
        {
            if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) return value;
            if (raw.StartsWith("-$", StringComparison.Ordinal) && raw.EndsWith('$') && parameters.TryGetValue(raw[2..^1], out var negative))
            {
                return checked(-ResolveNumber(negative, source, parameters, stack));
            }
            if (raw.StartsWith('$') && raw.EndsWith('$') && parameters.TryGetValue(raw[1..^1], out var argument))
            {
                return ResolveNumber(argument, source, parameters, stack);
            }
            if (!raw.StartsWith('@') || !stack.Add(raw)) { throw new PlanetEconomyImportException($"Unsupported numeric source value '{raw}' at {source}."); }
            try
            {
                var local = _files[source].Root!.Optional(raw);
                var variable = local ?? (_globalVariables.TryGetValue(raw, out var global) ? global : null);
                if (variable is null) { throw new PlanetEconomyImportException($"Missing source variable {raw} at {source}."); }
                RequireAssignment(variable);
                return ResolveNumber(variable.AsScalar(), variable.Source, parameters, stack);
            }
            finally { stack.Remove(raw); }
        }

        private (string Name, Dictionary<string, string> Args) InlineCall(ScriptEntry inline, IReadOnlyDictionary<string, string> parent)
        {
            RequireAssignment(inline);
            string name;
            var args = new InlineScriptParameters();
            if (inline.Block is null)
            {
                name = ResolveToken(inline.AsScalar(), parent);
            }
            else
            {
                var block = inline.AsBlock();
                var script = block.Required("script");
                RequireAssignment(script);
                name = ResolveToken(script.AsScalar(), parent);
                foreach (var parameter in block.Entries.Where(item => item.Key != "script"))
                {
                    RequireAssignment(parameter);
                    args.Add(parameter.Key, ResolveToken(parameter.AsScalar(), parent));
                    args.Origins.Add(parameter.Key, parameter);
                }
            }
            if (string.IsNullOrWhiteSpace(name) || name.Contains("..", StringComparison.Ordinal) || name.Contains('\\') || name.StartsWith('/'))
            {
                throw Error(inline, "unsafe inline script name");
            }
            if (!_inlineScripts.ContainsKey(name)) { throw Error(inline, "missing inline dependency " + name); }
            return (name, args);
        }

        private string ResolveToken(string value, IReadOnlyDictionary<string, string> parameters)
        {
            if (value.StartsWith('$') && value.EndsWith('$') && parameters.TryGetValue(value[1..^1], out var exact)) return exact;
            return Regex.Replace(value, @"\$([A-Za-z0-9_]+)\$", match =>
                parameters.TryGetValue(match.Groups[1].Value, out var resolved) ? resolved : match.Value);
        }

        private Dictionary<string, string> MergeParameters(IReadOnlyDictionary<string, string> parent, IReadOnlyDictionary<string, string> child)
        {
            var result = new InlineScriptParameters(parent);
            foreach (var item in child)
            {
                result[item.Key] = item.Value;
                result.Origins.Remove(item.Key);
                if (child is InlineScriptParameters sourced && sourced.Origins.TryGetValue(item.Key, out var origin))
                    result.Origins.Add(item.Key, origin);
            }
            return result;
        }

        private void EnqueueInlineNames(ScriptBlock block, Queue<string> queue)
        {
            foreach (var entry in Descendants(block).Where(entry => entry.Key == "inline_script"))
            {
                string name;
                if (entry.Block is null) { name = entry.AsScalar(); }
                else
                {
                    var script = entry.AsBlock().Optional("script") ?? throw Error(entry, "inline_script lacks script field");
                    name = script.AsScalar();
                }
                if (name.StartsWith('$') || name.StartsWith('@')) { throw Error(entry, "dynamic inline script name is unsupported"); }
                if (name.Contains("..", StringComparison.Ordinal) || name.Contains('\\') || name.StartsWith('/')) { throw Error(entry, "unsafe inline script name"); }
                queue.Enqueue(name);
            }
        }

        private IEnumerable<string> FlattenScalars(ScriptEntry entry)
        {
            if (entry.Block is null) { yield return entry.Scalar ?? entry.Key; yield break; }
            foreach (var nested in entry.AsBlock().Entries)
            {
                foreach (var value in FlattenScalars(nested)) { yield return value; }
            }
        }

        private static string[] SetValues(ScriptBlock block, string key) => block.Named(key)
            .SelectMany(entry => entry.Block?.Entries.Select(item => item.Scalar ?? item.Key) ?? (entry.Scalar is null ? Array.Empty<string>() : [entry.Scalar]))
            .ToArray();

        private static string RequiredScalar(ScriptBlock block, string key)
        {
            var entry = block.Required(key);
            RequireAssignment(entry);
            return entry.AsScalar();
        }

        private static string? OptionalScalar(ScriptBlock? block, string key)
        {
            var entry = block?.Optional(key);
            if (entry is null) return null;
            RequireAssignment(entry);
            return entry.AsScalar();
        }

        private PlanetEconomyProvenance Provenance(ScriptEntry entry)
        {
            if (!_files.TryGetValue(entry.Source, out var source)) { throw Error(entry, "source provenance is unavailable"); }
            return new PlanetEconomyProvenance(entry.Source, source.Sha256, entry.Line, entry.Column);
        }

        private string Resolve(string relative)
        {
            var normalized = Normalize(relative);
            var full = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new PlanetEconomyImportException("Source path escaped game root.");
            }
            ValidateNoReparsePath(full, relative);
            return full;
        }

        private IEnumerable<string> EnumerateSourceFiles(string directory)
        {
            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.TryPop(out var current))
            {
                ValidateNoReparsePath(current, Path.GetRelativePath(_root, current));
                IEnumerable<string> entries;
                try { entries = Directory.EnumerateFileSystemEntries(current).ToArray(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    throw new PlanetEconomyImportException("Cannot enumerate required scripted variable directory.", error);
                }
                foreach (var entry in entries)
                {
                    ValidateNoReparsePath(entry, Path.GetRelativePath(_root, entry));
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else yield return entry;
                }
            }
        }

        private void ValidateNoReparsePath(string fullPath, string relative)
        {
            var current = Path.GetPathRoot(fullPath) ?? throw new PlanetEconomyImportException("Source path has no filesystem root.");
            var rootRelative = Path.GetRelativePath(current, _root);
            if (rootRelative == ".") rootRelative = string.Empty;
            var rootSegments = rootRelative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in rootSegments)
            {
                current = Path.Combine(current, segment);
                RejectReparseIfPresent(current, relative);
            }

            var withinRoot = Path.GetRelativePath(_root, fullPath);
            if (withinRoot == ".") return;
            foreach (var segment in withinRoot.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                RejectReparseIfPresent(current, relative);
            }
        }

        private static void RejectReparseIfPresent(string path, string relative)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new PlanetEconomyImportException("Source path contains an unsupported reparse point: " + relative);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }

        private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
        private static string InlinePath(string name) => "common/inline_scripts/" + name + ".txt";
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        private static bool IsNumberToken(string value) => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _) || value.StartsWith('@') || value.StartsWith('$');
        private static IEnumerable<ScriptEntry> Descendants(ScriptBlock block)
        {
            foreach (var entry in block.Entries)
            {
                yield return entry;
                if (entry.Block is not null) foreach (var nested in Descendants(entry.AsBlock())) yield return nested;
            }
        }
        private static IEnumerable<PlanetEconomyCondition> Descendants(PlanetEconomyCondition condition)
        {
            yield return condition;
            foreach (var child in condition.Children)
            {
                foreach (var nested in Descendants(child)) { yield return nested; }
            }
        }
        private static IEnumerable<(ScriptEntry Entry, bool UnderOr)> MandatoryConditions(ScriptBlock block, bool underOr = false)
        {
            foreach (var entry in block.Entries)
            {
                var inAlternative = underOr || entry.Key is "OR" or "NOR" or "NOT";
                yield return (entry, inAlternative);
                if (entry.Block is not null)
                    foreach (var nested in MandatoryConditions(entry.AsBlock(), inAlternative)) yield return nested;
            }
        }
        private static void RequireAssignment(ScriptEntry entry)
        {
            if (entry.Operator != "=") throw Error(entry, "expected assignment operator");
        }
        private static PlanetEconomyImportException Error(ScriptEntry entry, string reason) =>
            new($"{entry.Source}:{entry.Line}:{entry.Column}: {entry.Key}: {reason}.");

        private sealed record SourceFile(string Path, byte[] Bytes, string Sha256, ScriptBlock? Root);
    }
}

public sealed class PlanetEconomyImportException(string message, Exception? innerException = null) : Exception(message, innerException);
