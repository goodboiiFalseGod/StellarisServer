using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Construction.Startup;

/// <summary>Импортирует ограниченную последовательность стартовых building effects Stellaris 4.5.2.</summary>
public sealed class StartupBuildingImporter
{
    private const string START = "common/scripted_effects/01_start_of_game_effects.txt";
    private const int FILE_LIMIT = 2 * 1024 * 1024;
    private const int TOTAL_LIMIT = 8 * 1024 * 1024;
    private static readonly string[] ETHICS = ["spiritualist", "materialist", "pacifist", "militarist", "egalitarian", "authoritarian", "xenophile", "xenophobe"];
    private readonly Func<CancellationToken, Task>? _beforeVerification;
    private readonly Func<string, byte[]>? _sourceReader;

    public StartupBuildingImporter() { }
    internal StartupBuildingImporter(Func<CancellationToken, Task> beforeVerification, Func<string, byte[]>? sourceReader = null)
    {
        _beforeVerification = beforeVerification ?? throw new ArgumentNullException(nameof(beforeVerification));
        _sourceReader = sourceReader;
    }

    public Task<StartupBuildingPlan> GenerateAsync(string gameRoot, StartupOwnerContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        ArgumentNullException.ThrowIfNull(context);
        return new Run(gameRoot, context, cancellationToken, _beforeVerification, _sourceReader).GenerateAsync();
    }

    private sealed class Run
    {
        private readonly string _root;
        private readonly StartupOwnerContext _context;
        private readonly CancellationToken _cancellation;
        private readonly Func<CancellationToken, Task>? _beforeVerification;
        private readonly Func<string, byte[]>? _sourceReader;
        private readonly Dictionary<string, SourceFile> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, OpaqueSourceFile> _opaqueFiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _districts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptEntry> _districtEntries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _slots = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _effects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _inline = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _deposits = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _buildingDefinitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScriptBlock> _zoneDefinitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _scriptedNumbers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _variables = new(StringComparer.Ordinal);
        private readonly List<StartupDistrictResult> _districtResults = [];
        private readonly List<StartupSlotResult> _slotResults = [];
        private readonly List<StartupZoneResult> _zoneResults = [];
        private readonly List<StartupBuildingResult> _buildingResults = [];
        private readonly List<string> _activeDeposits = [];
        private readonly List<StartupFeatureResult> _featureResults = [];
        private readonly List<StartupZoneRecurringEconomyProof> _zoneRecurringEconomyProofs = [];
        private readonly Dictionary<string, int> _effectiveDistrictCaps = new(StringComparer.Ordinal);
        private readonly List<PendingBuilding> _pendingBuildings = [];
        private readonly List<StartupGate> _gates = [];
        private long _totalBytes;
        private int _nextBuildingOrder;
        private int _planetSize;
        private int _nextFeatureOrder;
        private bool _sourceWorldSupported;
        private int _effectiveGeneralCapacityModifier;
        private string? _sourcePlanetClimate;

        public Run(string root, StartupOwnerContext context, CancellationToken cancellation, Func<CancellationToken, Task>? beforeVerification, Func<string, byte[]>? sourceReader)
        {
            _root = Path.GetFullPath(root);
            _context = context;
            _planetSize = context.PlanetSize;
            _cancellation = cancellation;
            _beforeVerification = beforeVerification;
            _sourceReader = sourceReader;
        }

        public async Task<StartupBuildingPlan> GenerateAsync()
        {
            _cancellation.ThrowIfCancellationRequested();
            var start = await LoadAsync(START);
            var dispatcher = Unique(start, "generate_start_buildings_and_districts");
            if (dispatcher.Operator != "=" || dispatcher.Block is null) { throw Error(START, dispatcher, "Expected startup effect block."); }
            var helper = Unique(start, "add_district_and_planet_size_if_needed_effect_amount");
            var addIfAbsent = Unique(start, "add_building_if_not_present");
            ValidateDistrictHelper(helper);
            _effects.Add("add_district_and_planet_size_if_needed_effect_amount", helper.Block!);
            _effects.Add("add_building_if_not_present", addIfAbsent.Block ?? throw Error(START, addIfAbsent, "Expected helper block."));

            await LoadDistrictsAsync();
            await LoadZoneSlotsAsync();
            await LoadInlineDependenciesAsync();
            await LoadWorldDefinitionsAsync();
            _sourceWorldSupported = _context.Authority == StartupAuthority.Regular && _context.Origin == "origin_default" && _context.PlanetClass == "pc_continental" && !_context.IsNomadic && !_context.IsWilderness;
            if (_sourceWorldSupported)
            {
                await ProveDefaultZoneNoRecurringEconomyAsync();
                InitializeDefaultStartingDistrict();
                await GenerateStartingFeaturesAsync();
            }

            foreach (var entry in dispatcher.Block!.Entries)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (entry.Key != "inline_script") { continue; }
                var reference = entry.AsScalar();
                if (!reference.StartsWith("game_start/", StringComparison.Ordinal)) { continue; }
                var name = reference["game_start/".Length..];
                if (name is "authority_regular" or "authority_hive" or "authority_machine")
                {
                    if (name == AuthorityScript(_context.Authority)) { await ProcessModuleAsync(name + ".txt", new Dictionary<string, string>(StringComparer.Ordinal)); }
                    continue;
                }
                if (name.StartsWith("authority_", StringComparison.Ordinal)) { continue; }
                if (name == "origin_default")
                {
                    if (_context.Origin == "origin_default") { await ProcessModuleAsync("origin_default.txt", new Dictionary<string, string>(StringComparer.Ordinal)); }
                    continue;
                }
                if (name.StartsWith("origin_", StringComparison.Ordinal))
                {
                    if (_context.Origin == name["origin_".Length..])
                    {
                        var path = "common/inline_scripts/game_start/" + name + ".txt";
                        _gates.Add(new("startup-origin-unresolved", path, "Выбранный origin имеет source effect sequence вне поддержанного профиля."));
                    }
                    continue;
                }
                if (name.StartsWith("ethic_", StringComparison.Ordinal) && ETHICS.Contains(name[6..], StringComparer.Ordinal))
                {
                    await ProcessModuleAsync(name + ".txt", new Dictionary<string, string>(StringComparer.Ordinal));
                    continue;
                }
                if (name == "civics")
                {
                    await ProcessCivicsAsync();
                    continue;
                }
                if (name is "rural_districts_initializer" or "rural_districts_placement")
                {
                    var path = "common/inline_scripts/game_start/" + name + ".txt";
                    _ = await LoadModuleAsync(path);
                    if (!_sourceWorldSupported)
                    {
                        _gates.Add(new("startup-effect-unresolved", path, "Rural start для этого контекста omitted: source-world атом поддерживает только ordinary origin_default pc_continental."));
                        continue;
                    }
                    await ProcessModuleAsync(name + ".txt", new Dictionary<string, string>(StringComparer.Ordinal));
                    continue;
                }
                if (name is "species_lithoids" or "species_infernals" or "species_subspecies" or "shipsets" or "civic_tankbound") { continue; }
                if (name == "species_individual_machine")
                {
                    if (_context.IsIndividualMachine) { await ProcessModuleAsync(name + ".txt", new Dictionary<string, string>(StringComparer.Ordinal)); }
                    continue;
                }
                if (name is "authority_regular_nomad" or "authority_hive_nomad" or "authority_machine_nomad" or "origin_wilderness" or "volcanic_world_setup") { continue; }
                throw Error(START, entry, "Reached startup inline script is outside this basic profile.");
            }

            if (_districtResults.Count == 0 || _slotResults.Count == 0) { throw new StartupBuildingImportException("Startup source did not produce a supported urban district graph."); }
            ResolvePendingBuildings(START, null);
            if (_pendingBuildings.Count != 0) { throw new StartupBuildingImportException("A reached building add has no unique compatible zone occurrence."); }
            if (_beforeVerification is not null) { await _beforeVerification(_cancellation); }
            await VerifyStableAsync();
            return Plan();
        }

        private StartupBuildingPlan Plan() => new(_context, _planetSize, _districtResults, _slotResults, _zoneResults, _buildingResults,
            _files.Values.Select(file => new StartupSourceFile(file.Path, file.Bytes.LongLength, file.Hash)).Concat(_opaqueFiles.Values.Select(file => new StartupSourceFile(file.Path, file.Bytes.LongLength, file.Hash))),
            _gates, _featureResults, _effectiveDistrictCaps.Select(item => new StartupDistrictCapResult(item.Key, item.Value)), _zoneRecurringEconomyProofs);

        private static string AuthorityScript(StartupAuthority authority) => authority switch { StartupAuthority.Regular => "authority_regular", StartupAuthority.Hive => "authority_hive", StartupAuthority.Machine => "authority_machine", _ => throw new ArgumentOutOfRangeException(nameof(authority)) };

        private async Task LoadInlineDependenciesAsync()
        {
            _ = await LoadAsync(START);
            var paths = new List<string> { "common/inline_scripts/game_start/" + AuthorityScript(_context.Authority) + ".txt", "common/inline_scripts/game_start/species_individual_machine.txt", "common/inline_scripts/game_start/origin_default.txt", "common/inline_scripts/game_start/civics.txt" };
            paths.AddRange(ETHICS.Select(id => "common/inline_scripts/game_start/ethic_" + id + ".txt"));
            foreach (var path in paths)
            {
                await LoadModuleAsync(path);
            }
            foreach (var civic in _context.Civics)
            {
                var civicPath = "common/inline_scripts/game_start/civic_" + civic["civic_".Length..] + ".txt";
                var full = Path.Combine(_root, civicPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full)) { await LoadModuleAsync(civicPath); }
            }
        }

        private async Task LoadDistrictsAsync()
        {
            var directory = Path.Combine(_root, "common", "districts");
            if (!Directory.Exists(directory)) { throw new StartupBuildingImportException("Required source directory is missing: common/districts."); }
            var paths = Directory.EnumerateFiles(directory, "*.txt", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).ToArray();
            if (paths.Length > 64) { throw new StartupBuildingImportException("District source inventory exceeds 64 files."); }
            foreach (var fullPath in paths)
            {
                var relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
                var source = await LoadAsync(relative);
                foreach (var entry in source.Entries.Where(entry => entry.Block is not null && entry.Operator == "="))
                {
                    if (!_districts.TryAdd(entry.Key, entry.Block!) || !_districtEntries.TryAdd(entry.Key, entry)) { throw Error(relative, entry, "Ambiguous district definition."); }
                }
            }
        }

        private void InitializeDefaultStartingDistrict()
        {
            var defaults = _districtEntries.Values.Where(entry =>
            {
                var flag = entry.Block?.Optional("default_starting_district");
                if (flag is null) { return false; }
                if (flag.Operator != "=" || flag.Block is not null || flag.Scalar is not ("yes" or "no")) { throw Error(entry.Source, entry, "Malformed default_starting_district flag."); }
                return flag.Scalar == "yes";
            }).ToArray();
            if (defaults.Length != 1) { throw new StartupBuildingImportException("Expected one source default_starting_district=yes definition for ordinary continental startup."); }
            var district = defaults[0];
            if (district.Operator != "=" || district.Block is null || !_districts.ContainsKey(district.Key)) { throw Error(district.Source, district, "Malformed source default starting district definition."); }
            // Engine rule supplied by the user: the source-selected default district starts at level one before ordered startup effects.
            AddDistrict(district.Source, district, district.Key);
        }

        private async Task LoadZoneSlotsAsync()
        {
            const string path = "common/zone_slots/00_zone_slots.txt";
            var source = await LoadAsync(path);
            foreach (var entry in source.Entries.Where(entry => entry.Block is not null && entry.Operator == "="))
            {
                if (!_slots.TryAdd(entry.Key, entry.Block!)) { throw Error(path, entry, "Ambiguous zone-slot definition."); }
            }
        }

        private async Task LoadWorldDefinitionsAsync()
        {
            await LoadDefinitionDirectoryAsync("common/deposits", _deposits, "deposit");
            await LoadDefinitionDirectoryAsync("common/buildings", _buildingDefinitions, "building");
            await LoadDefinitionDirectoryAsync("common/zones", _zoneDefinitions, "zone");
            const string planetClasses = "common/planet_classes/00_planet_classes.txt";
            var planetSource = await LoadOpaqueAsync(planetClasses);
            var climateMatches = Regex.Matches(planetSource.Text, "(?ms)^pc_continental\\s*=\\s*\\{(?:(?!^\\S+\\s*=\\s*\\{).)*?^\\s*climate\\s*=\\s*\"(?<value>[^\"]+)\"");
            if (climateMatches.Count != 1 || climateMatches[0].Groups["value"].Value != "wet") { throw new StartupBuildingImportException(planetClasses + ": source continental climate is missing, ambiguous or changed."); }
            _sourcePlanetClimate = climateMatches[0].Groups["value"].Value;
            var variableDirectory = Path.Combine(_root, "common", "scripted_variables");
            if (!Directory.Exists(variableDirectory)) { throw new StartupBuildingImportException("Required source directory is missing: common/scripted_variables."); }
            foreach (var fullPath in Directory.EnumerateFiles(variableDirectory, "*.txt", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
                var source = await LoadAsync(relative);
                foreach (var entry in source.Entries.Where(entry => entry.Key.StartsWith('@') && entry.Operator == "=" && entry.Scalar is not null))
                {
                    if (!double.TryParse(entry.Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { throw Error(relative, entry, "Invalid scripted numeric variable."); }
                    if (!_scriptedNumbers.TryAdd(entry.Key, value)) { throw Error(relative, entry, "Ambiguous scripted numeric variable."); }
                }
            }
        }

        private async Task ProveDefaultZoneNoRecurringEconomyAsync()
        {
            const string definitionPath = "common/zones/00_zones.txt";
            const string zoneId = "zone_default";
            const string scriptId = "buildings/nomadic_cost_switcher";
            const string inlinePath = "common/inline_scripts/buildings/nomadic_cost_switcher.txt";
            var source = await LoadAsync(definitionPath);
            var zone = Unique(source, zoneId).AsBlock();
            var resources = Unique(zone, "resources").AsBlock();
            var resourceKeys = resources.Entries.Select(entry => entry.Key).Order(StringComparer.Ordinal).ToArray();
            if (!resourceKeys.SequenceEqual(new[] { "category", "inline_script" }, StringComparer.Ordinal)
                || resources.Optional("category")?.AsScalar() != "planet_zones")
            {
                throw new StartupBuildingImportException(definitionPath + ": zone_default recurring resource shape changed.");
            }
            var inlineCall = Unique(resources, "inline_script").AsBlock();
            if (Unique(inlineCall, "script").AsScalar() != scriptId
                || Unique(inlineCall, "REGULAR_RESOURCE").AsScalar() != "minerals"
                || Unique(inlineCall, "NOMADIC_RESOURCE").AsScalar() != "alloys"
                || Unique(inlineCall, "COST").AsScalar() != "@zone_cost"
                || Unique(inlineCall, "NOMADIC_COST_MULT").AsScalar() != "0.5")
            {
                throw new StartupBuildingImportException(definitionPath + ": zone_default cost inline parameters changed.");
            }
            var inline = await LoadAsync(inlinePath);
            if (inline.Entries.Count != 2 || inline.Entries.Any(entry => entry.Key != "cost" || entry.Operator != "=" || entry.Block is null))
            {
                throw new StartupBuildingImportException(inlinePath + ": nomadic cost inline shape changed; recurring zone flows are unresolved.");
            }
            foreach (var cost in inline.Named("cost"))
            {
                if (cost.Block!.Entries.Any(entry => entry.Key is "produces" or "upkeep" or "planet_modifier" or "triggered_planet_modifier"
                    || entry.Key.StartsWith("job_", StringComparison.Ordinal)))
                {
                    throw Error(inlinePath, cost, "Cost inline acquired a recurring economy term.");
                }
            }
            _zoneRecurringEconomyProofs.Add(new(zoneId, definitionPath, _files[definitionPath].Hash, inlinePath, _files[inlinePath].Hash));
        }

        private async Task LoadDefinitionDirectoryAsync(string relativeDirectory, Dictionary<string, ScriptBlock> target, string kind)
        {
            var directory = Path.Combine(_root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory)) { throw new StartupBuildingImportException("Required source directory is missing: " + relativeDirectory + "."); }
            foreach (var fullPath in Directory.EnumerateFiles(directory, "*.txt", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
                var source = await LoadAsync(relative);
                foreach (var entry in source.Entries.Where(entry => entry.Operator == "=" && entry.Block is not null))
                {
                    if (!target.TryAdd(entry.Key, entry.Block!)) { throw Error(relative, entry, "Ambiguous " + kind + " definition."); }
                }
            }
        }

        private async Task GenerateStartingFeaturesAsync()
        {
            const string generatorId = "generate_start_deposits_and_blockers";
            var source = await LoadAsync(START);
            var generator = Unique(source, generatorId);
            if (generator.Operator != "=" || generator.Block is null || generator.Block.Entries.Count < 2 || generator.Block.Entries[0].Key != "optimize_memory" || generator.Block.Entries[1].Key != "clear_deposits")
            {
                throw Error(START, generator, "Required start-deposit source shape changed.");
            }
            await ProcessDepositEntriesAsync(START, generator.Block.Entries);
            ResolveBlockerSwaps(START, generator);
            RefreshEffectiveDistrictCaps(START);
        }

        private async Task ProcessDepositEntriesAsync(string path, IReadOnlyList<ScriptEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                _cancellation.ThrowIfCancellationRequested();
                var entry = entries[i];
                if (entry.Key == "if")
                {
                    var branchEnd = i;
                    while (branchEnd + 1 < entries.Count && entries[branchEnd + 1].Key == "else_if") { branchEnd++; }
                    var hasElse = branchEnd + 1 < entries.Count && entries[branchEnd + 1].Key == "else";
                    var chainEnd = branchEnd + (hasElse ? 1 : 0);
                    var selected = false;
                    for (var branchIndex = i; branchIndex <= branchEnd; branchIndex++)
                    {
                        var branch = entries[branchIndex];
                        var limit = branch.Block?.Optional("limit") ?? throw Error(path, branch, "Deposit condition has no limit block.");
                        bool matches;
                        try { matches = Evaluate(path, limit.AsBlock(), new Dictionary<string, string>(StringComparer.Ordinal)); }
                        catch (UnresolvedStartupConditionException unresolved) { throw Error(path, branch, "Start-deposit source needs unsupported condition " + unresolved.Condition + "."); }
                        if (!matches) { continue; }
                        await ProcessDepositEntriesAsync(path, branch.Block!.Entries.Where(item => item.Key != "limit").ToArray());
                        selected = true;
                        break;
                    }
                    if (!selected && hasElse) { await ProcessDepositEntriesAsync(path, entries[chainEnd].AsBlock().Entries); }
                    i = chainEnd;
                    continue;
                }
                if (entry.Key == "else_if") { throw Error(path, entry, "else_if without a matching if in start-deposit source."); }
                if (entry.Key == "else") { await ProcessDepositEntriesAsync(path, entry.AsBlock().Entries); continue; }
                if (entry.Key == "clear_deposits")
                {
                    if (entry.Operator != "=" || entry.Scalar != "yes" || entry.Block is not null) { throw Error(path, entry, "clear_deposits operand changed."); }
                    _activeDeposits.Clear(); _featureResults.Clear(); _nextFeatureOrder = 0;
                    RefreshEffectiveDistrictCaps(path);
                    continue;
                }
                if (entry.Key == "add_deposit")
                {
                    if (entry.Operator != "=" || entry.Block is not null) { throw Error(path, entry, "Malformed add_deposit."); }
                    AddFeature(entry.AsScalar(), isBlocker: false, path, entry);
                    continue;
                }
                if (entry.Key == "remove_deposit") { throw Error(path, entry, "Reached remove_deposit has no source-verified duplicate removal semantics in this profile."); }
                if (entry.Key is "optimize_memory") { continue; }
                if (entry.Key is "while" or "add_random_natural_blocker_and_give_tech" or "create_life_seeded_planet")
                {
                    throw Error(path, entry, "Reached start-deposit operator is outside ordinary continental source coverage: " + entry.Key + ".");
                }
                if (entry.Key.StartsWith("#", StringComparison.Ordinal)) { continue; }
                throw Error(path, entry, "Unsupported reached start-deposit effect/operator.");
            }
        }

        private void AddFeature(string definitionId, bool isBlocker, string path, ScriptEntry source)
        {
            if (!_deposits.TryGetValue(definitionId, out var definition)) { throw Error(path, source, "Missing reached deposit definition " + definitionId + "."); }
            var instanceId = "feature:" + _nextFeatureOrder.ToString(CultureInfo.InvariantCulture);
            isBlocker |= definition.Optional("category")?.AsScalar() == "deposit_cat_blockers";
            _featureResults.Add(new(instanceId, definitionId, _nextFeatureOrder, isBlocker, false, null, null));
            _nextFeatureOrder++;
            _activeDeposits.Add(instanceId);
            RefreshEffectiveDistrictCaps(path);
        }

        private void ResolveBlockerSwaps(string path, ScriptEntry generator)
        {
            var blockerIndices = _activeDeposits.Select((id, index) => (id, index))
                .Where(item => !_featureResults.Single(feature => feature.InstanceId == item.id).IsHidden && _deposits[_featureResults.Single(feature => feature.InstanceId == item.id).DefinitionId].Optional("category")?.AsScalar() == "deposit_cat_blockers")
                .Select(item => item.id).ToArray();
            // A task-local seed drives a source-weighted choice without claiming native PRNG identity.
            var random = new Random(_context.BlockerChoiceSeed);
            foreach (var blockerInstanceId in blockerIndices)
            {
                var blockerIndex = _featureResults.FindIndex(feature => feature.InstanceId == blockerInstanceId);
                var blocker = _featureResults[blockerIndex];
                var definition = _deposits[blocker.DefinitionId];
                var allowedBlock = definition.Optional("blocker_swap_types")?.AsBlock();
                if (allowedBlock is null)
                {
                    if (definition.Optional("all_blocker_swap_types")?.AsScalar() == "yes") { throw new StartupBuildingImportException(path + ": blocker " + blocker.DefinitionId + " uses unsupported all_blocker_swap_types."); }
                    continue;
                }
                if (definition.Optional("all_blocker_swap_types")?.AsScalar() == "yes")
                {
                    throw new StartupBuildingImportException(path + ": blocker " + blocker.DefinitionId + " uses unsupported all_blocker_swap_types.");
                }
                var allowed = allowedBlock.Entries.Select(item => item.AsScalar()).ToArray();
                var candidates = _activeDeposits.Where(instanceId =>
                {
                    var feature = _featureResults.Single(item => item.InstanceId == instanceId);
                    return !feature.IsHidden && !feature.IsBlocker && allowed.Contains(feature.DefinitionId, StringComparer.Ordinal) && IsDepositPotentialTrue(path, feature.DefinitionId);
                }).Select(instanceId => _featureResults.Single(item => item.InstanceId == instanceId)).ToArray();
                if (candidates.Length == 0) { throw new StartupBuildingImportException(path + ": blocker " + blocker.DefinitionId + " has no active suitable blocker_swap_types target."); }
                var useWeights = definition.Optional("use_weights_for_blocker_swap_types")?.AsScalar() switch
                {
                    "yes" => true,
                    "no" or null => false,
                    _ => throw new StartupBuildingImportException(path + ": invalid use_weights_for_blocker_swap_types for " + blocker.DefinitionId + ".")
                };
                var weighted = candidates.Select(candidate => (Feature: candidate, Weight: useWeights ? DepositDropWeight(path, candidate.DefinitionId) : 1d)).Where(item => item.Weight > 0d).ToArray();
                var totalWeight = weighted.Sum(item => item.Weight);
                if (!(totalWeight > 0d) || !double.IsFinite(totalWeight)) { throw new StartupBuildingImportException(path + ": blocker " + blocker.DefinitionId + " has no positive weighted target."); }
                var draw = random.NextDouble() * totalWeight;
                var selected = weighted[^1].Feature;
                foreach (var item in weighted)
                {
                    draw -= item.Weight;
                    if (draw < 0d) { selected = item.Feature; break; }
                }
                var hiddenIndex = _featureResults.FindIndex(feature => feature.InstanceId == selected.InstanceId);
                _featureResults[hiddenIndex] = _featureResults[hiddenIndex] with { IsHidden = true, HiddenByBlockerInstanceId = blocker.InstanceId };
                _featureResults[blockerIndex] = blocker with { IsBlocker = true, HiddenFeatureInstanceId = selected.InstanceId };
                RefreshEffectiveDistrictCaps(path);
            }
        }

        private bool IsDepositPotentialTrue(string path, string definitionId)
        {
            var potential = _deposits[definitionId].Optional("potential");
            return potential is null || Evaluate(path, potential.AsBlock(), new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private double DepositDropWeight(string path, string definitionId)
        {
            var entry = _deposits[definitionId].Optional("drop_weight");
            if (entry is null) { return 1d; } // The 4.5.2 deposit README documents the default.
            var block = entry.AsBlock();
            var baseWeight = ReadScriptNumber(path, block.Required("weight").AsScalar());
            foreach (var modifier in block.Named("modifier"))
            {
                var modifierBlock = modifier.AsBlock();
                var predicates = modifierBlock.Entries.Where(item => item.Key != "factor").ToArray();
                if (Evaluate(path, new ScriptBlock(predicates, path, modifier.Line, modifier.Column), new Dictionary<string, string>(StringComparer.Ordinal)))
                {
                    baseWeight *= ReadScriptNumber(path, modifierBlock.Required("factor").AsScalar());
                }
            }
            if (baseWeight < 0d || !double.IsFinite(baseWeight)) { throw new StartupBuildingImportException(path + ": invalid source drop_weight for " + definitionId + "."); }
            return baseWeight;
        }

        private double ReadScriptNumber(string path, string raw)
        {
            if (raw.StartsWith('@'))
            {
                if (!_scriptedNumbers.TryGetValue(raw, out var value)) { throw new StartupBuildingImportException(path + ": missing scripted numeric variable " + raw + "."); }
                return value;
            }
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed)) { throw new StartupBuildingImportException(path + ": malformed source numeric value " + raw + "."); }
            return parsed;
        }

        private void RefreshEffectiveDistrictCaps(string path)
        {
            _effectiveGeneralCapacityModifier = 0;
            _effectiveDistrictCaps.Clear();
            foreach (var feature in _featureResults.Where(item => !item.IsHidden))
            {
                var definition = _deposits[feature.DefinitionId];
                var general = definition.Optional("planet_modifier")?.Block?.Optional("planet_max_districts_add");
                if (feature.IsBlocker && general is null) { throw new StartupBuildingImportException(path + ": source blocker " + feature.DefinitionId + " no longer declares planet_max_districts_add = -1."); }
                if (general is not null)
                {
                    var value = ReadScriptNumber(path, general.AsScalar());
                    if (feature.IsBlocker && value != -1d) { throw new StartupBuildingImportException(path + ": source blocker " + feature.DefinitionId + " no longer has planet_max_districts_add = -1."); }
                    if (value != Math.Truncate(value) || value is < int.MinValue or > int.MaxValue) { throw new StartupBuildingImportException(path + ": unsupported non-integral general district capacity modifier."); }
                    _effectiveGeneralCapacityModifier = checked(_effectiveGeneralCapacityModifier + (int)value);
                }
                var modifierBlock = definition.Optional("planet_modifier")?.Block;
                if (modifierBlock is null) { continue; }
                foreach (var modifier in modifierBlock.Entries.Where(item => item.Key.StartsWith("district_", StringComparison.Ordinal) && item.Key.EndsWith("_max_add", StringComparison.Ordinal)))
                {
                    var value = ReadScriptNumber(path, modifier.AsScalar());
                    if (value != Math.Truncate(value) || value is < int.MinValue or > int.MaxValue) { throw new StartupBuildingImportException(path + ": unsupported non-integral district-type cap " + modifier.Key + "."); }
                    _effectiveDistrictCaps[modifier.Key] = checked(_effectiveDistrictCaps.GetValueOrDefault(modifier.Key) + (int)value);
                }
            }
            _effectiveDistrictCaps["planet_max_districts_add"] = _effectiveGeneralCapacityModifier;
            foreach (var cap in new[] { "district_generator_max_add", "district_mining_max_add", "district_farming_max_add" }) { _effectiveDistrictCaps.TryAdd(cap, 0); }
        }

        private async Task ProcessCivicsAsync()
        {
            const string path = "common/inline_scripts/game_start/civics.txt";
            var source = await LoadAsync(path);
            foreach (var entry in source.Entries.Where(item => item.Key == "inline_script"))
            {
                var reference = entry.AsScalar();
                if (!reference.StartsWith("game_start/civic_", StringComparison.Ordinal)) { throw Error(path, entry, "Unsupported civic dispatcher path."); }
                var id = reference["game_start/".Length..];
                var civicId = "civic_" + id["civic_".Length..];
                if (!_context.Civics.Contains(civicId)) { continue; }
                await ProcessModuleAsync(id + ".txt", new Dictionary<string, string>(StringComparer.Ordinal));
            }
        }

        private async Task ProcessModuleAsync(string fileName, Dictionary<string, string> locals)
        {
            var path = "common/inline_scripts/game_start/" + fileName;
            var block = await LoadModuleAsync(path);
            await ProcessEntriesAsync(path, block.Entries, locals);
        }

        private async Task<ScriptBlock> LoadModuleAsync(string path)
        {
            if (_inline.TryGetValue(path, out var existing)) { return existing; }
            var source = await LoadAsync(path);
            _inline.Add(path, source);
            return source;
        }

        private async Task ProcessEntriesAsync(string path, IReadOnlyList<ScriptEntry> entries, Dictionary<string, string> locals)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                _cancellation.ThrowIfCancellationRequested();
                var entry = entries[i];
                switch (entry.Key)
                {
                    case "if":
                        var branchEnd = i;
                        while (branchEnd + 1 < entries.Count && entries[branchEnd + 1].Key == "else_if") { branchEnd++; }
                        var hasElse = branchEnd + 1 < entries.Count && entries[branchEnd + 1].Key == "else";
                        var chainEnd = branchEnd + (hasElse ? 1 : 0);
                        var selected = false;
                        for (var branchIndex = i; branchIndex <= branchEnd; branchIndex++)
                        {
                            var conditional = entries[branchIndex];
                            var limit = conditional.Block?.Optional("limit") ?? throw Error(path, conditional, "Conditional has no limit block.");
                            bool matches;
                            try { matches = Evaluate(path, limit.AsBlock(), locals); }
                            catch (UnresolvedStartupConditionException unresolved)
                            {
                                if (_sourceWorldSupported) { throw Error(path, conditional, $"Достигнутое условие {unresolved.Condition} не имеет source-факта; план не может считаться authoritative."); }
                                _gates.Add(new("startup-condition-unresolved", unresolved.SourcePath, $"Условие {unresolved.Condition} не вычислено: {unresolved.Detail}"));
                                selected = true;
                                break;
                            }
                            if (matches)
                            {
                                await ProcessEntriesAsync(path, conditional.Block!.Entries.Where(item => item.Key != "limit").ToArray(), locals);
                                selected = true;
                                break;
                            }
                        }
                        if (!selected && hasElse) { await ProcessEntriesAsync(path, entries[chainEnd].AsBlock().Entries, locals); }
                        i = chainEnd;
                        break;
                    case "else_if":
                        throw Error(path, entry, "else_if without a matching if.");
                    case "else":
                        await ProcessEntriesAsync(path, entry.AsBlock().Entries, locals);
                        break;
                    case "inline_script":
                        var inlineId = entry.Block is null ? entry.AsScalar() : entry.Block.Required("script").AsScalar();
                        if (!inlineId.StartsWith("game_start/", StringComparison.Ordinal)) { throw Error(path, entry, "Unsupported reached inline script."); }
                        var inlinePath = "common/inline_scripts/" + inlineId + ".txt";
                        await ProcessEntriesAsync(inlinePath, (await LoadModuleAsync(inlinePath)).Entries, locals);
                        break;
                    case "add_district_and_planet_size_if_needed_effect_amount":
                        ExecuteDistrictHelper(path, entry);
                        break;
                    case "add_building_if_not_present":
                        var building = entry.Block?.Required("BUILDING").AsScalar() ?? throw Error(path, entry, "Expected BUILDING argument.");
                        await ProcessEntriesAsync("common/scripted_effects/01_start_of_game_effects.txt", _effects["add_building_if_not_present"].Entries, new Dictionary<string, string>(locals, StringComparer.Ordinal) { ["BUILDING"] = Substitute(building, locals) });
                        break;
                    case "add_zone":
                        AddZone(path, entry);
                        break;
                    case "add_deposit":
                    case "remove_deposit":
                    case "clear_deposits":
                        throw Error(path, entry, "Достигнутое изменение deposits вне импортированного source generator не поддерживается; authoritative план не публикуется.");
                    case "add_building":
                        AddBuilding(path, entry, locals);
                        break;
                    case "remove_building":
                        RemoveBuilding(path, entry, locals);
                        break;
                    case "add_district":
                        var districtId = Substitute(entry.AsScalar(), locals);
                        AddDistrict(path, entry, districtId);
                        break;
                    case "set_variable":
                    case "change_variable":
                        ApplyVariableMutation(path, entry);
                        break;
                    case "clear_variable":
                        _variables.Remove(entry.AsScalar());
                        break;
                    case "export_modifier_to_variable":
                        ExportModifierToVariable(path, entry);
                        break;
                    case "while":
                        await ExecuteSourceWhile(path, entry, locals);
                        break;
                    case "optimize_memory":
                    case "set_timed_carrier_flag":
                    case "give_starting_resources_effect":
                    case "validate_planet_buildings_and_districts":
                    case "set_planet_flag":
                    case "remove_planet_flag":
                    case "remove_carrier_flag":
                        break;
                    case "add_modifier":
                        _gates.Add(new("startup-effect-unresolved", path, "Reached add_modifier may affect startup capacity/state and has no imported evaluator."));
                        break;
                    case "limit":
                        throw Error(path, entry, "Unexpected standalone condition block.");
                    default:
                        if (entry.Key.StartsWith("#", StringComparison.Ordinal)) { break; }
                        throw Error(path, entry, "Unsupported reached startup effect/operator.");
                }
            }
        }

        private bool Evaluate(string path, ScriptBlock block, IReadOnlyDictionary<string, string> locals)
        {
            foreach (var condition in block.Entries)
            {
                if (condition.Key is "owner" or "owner?" or "from" or "planet")
                {
                    if (condition.Block is null || !Evaluate(path, condition.Block, locals)) { return false; }
                    continue;
                }
                if (condition.Key == "has_building") { ResolvePendingBuildings(path, condition); }
                if (condition.Key is "check_variable" or "check_modifier_value")
                {
                    RefreshEffectiveDistrictCaps(path);
                    if (!EvaluateNumericCheck(path, condition)) { return false; }
                    continue;
                }
                if (condition.Key is "NOT" or "OR" or "AND" or "NOR")
                {
                    var nested = condition.AsBlock().Entries.Select(item => Evaluate(path, new ScriptBlock([item], path, item.Line, item.Column), locals)).ToArray();
                    if (condition.Key == "NOT" && nested.Any(value => value) || condition.Key == "OR" && !nested.Any(value => value) || condition.Key == "AND" && nested.Any(value => !value) || condition.Key == "NOR" && nested.Any(value => value)) { return false; }
                    continue;
                }
                var scalar = condition.Scalar is null ? null : Substitute(condition.Scalar, locals);
                bool? value = condition.Key switch
                {
                    "is_regular_empire" => _context.Authority == StartupAuthority.Regular,
                    "is_hive_empire" => _context.Authority == StartupAuthority.Hive,
                    "is_machine_empire" => _context.Authority == StartupAuthority.Machine,
                    "is_gestalt" => _context.Authority is StartupAuthority.Hive or StartupAuthority.Machine,
                    "is_nomadic" => _context.IsNomadic,
                    "is_wilderness_empire" => _context.IsWilderness,
                    "is_individual_machine" => _context.IsIndividualMachine,
                    "is_planet_class" => scalar is not null && scalar == _context.PlanetClass,
                    "country_uses_food_game_start" => _context.CountryUsesFoodGameStart,
                    "is_lithoid_empire" or "is_lithoid_or_infernal_empire" or "is_lithoid_devouring_swarm" or "is_apocalyptic_empire" or "is_environmental_architects_empire" => SourcePredicateFact(condition.Key, scalar),
                    "has_carrier_flag" => SourcePredicateFact(condition.Key, scalar),
                    "is_wet" => Climate() == "wet",
                    "is_cold" => Climate() == "cold",
                    "is_dry" => Climate() == "dry",
                    "is_dry_non_volcanic" => Climate() == "dry" && _context.PlanetClass != "pc_volcanic",
                    "is_authoritarian" or "is_fanatic_authoritarian" or "is_materialist" or "is_fanatic_materialist" or "is_pacifist" or "is_fanatic_pacifist" or "is_militarist" or "is_fanatic_militarist" or "is_egalitarian" or "is_fanatic_egalitarian" or "is_spiritualist" or "is_fanatic_spiritualist" or "is_xenophile" or "is_fanatic_xenophile" or "is_xenophobe" or "is_fanatic_xenophobe" => HasEthic(condition.Key),
                    "has_valid_civic" or "has_civic" => scalar is not null && _context.Civics.Contains(scalar),
                    "has_origin" => scalar is not null && _context.Origin == scalar,
                    "has_zone" => condition.Block is not null && _zoneResults.Any(zone => zone.ZoneId == condition.Block.Optional("zone")?.AsScalar()),
                    "has_building" => scalar is not null && _buildingResults.Any(building => building.DefinitionId == scalar),
                    "exists" => true,
                    _ => SourcePredicateFact(condition.Key, scalar)
                };
                if (value is null) { throw new UnresolvedStartupConditionException(path, condition.Key, "нет явного факта в startup context"); }
                var expected = scalar switch { "yes" => true, "no" => false, _ => (bool?)null };
                if (expected is null) { if (!value.Value) { return false; } }
                else if (value.Value != expected.Value) { return false; }
            }
            return true;
        }

        private bool? SourcePredicateFact(string predicate, string? operand)
        {
            var key = predicate == "has_carrier_flag" ? predicate + ":" + (operand ?? string.Empty) : predicate;
            return _context.SourcePredicateFacts.TryGetValue(key, out var value) ? value : null;
        }

        private bool HasEthic(string predicate)
        {
            var suffix = predicate[3..];
            if (suffix.StartsWith("fanatic_", StringComparison.Ordinal)) { return _context.Ethics.Contains("ethic_" + suffix); }
            return _context.Ethics.Contains(suffix) || _context.Ethics.Contains("ethic_" + suffix) || _context.Ethics.Contains("ethic_fanatic_" + suffix);
        }

        private string? Climate() => _context.PlanetClass == "pc_continental" ? _sourcePlanetClimate : null;

        private bool EvaluateNumericCheck(string path, ScriptEntry entry)
        {
            var block = entry.AsBlock();
            var valueEntry = block.Named("value").SingleOrDefault() ?? throw Error(path, entry, "Numeric source check has no value operand.");
            var actual = entry.Key switch
            {
                "check_variable" => _variables.GetValueOrDefault(block.Required("which").AsScalar()),
                "check_modifier_value" => _effectiveDistrictCaps.GetValueOrDefault(block.Required("modifier").AsScalar()),
                _ => throw Error(path, entry, "Unsupported numeric source check.")
            };
            var expectedText = valueEntry.AsScalar();
            var expected = _variables.TryGetValue(expectedText, out var variableExpected) ? variableExpected : ReadScriptNumber(path, expectedText);
            return valueEntry.Operator switch
            {
                "=" => actual == expected,
                ">" => actual > expected,
                ">=" => actual >= expected,
                "<" => actual < expected,
                "<=" => actual <= expected,
                _ => throw Error(path, valueEntry, "Unsupported numeric comparison operator.")
            };
        }

        private void ApplyVariableMutation(string path, ScriptEntry entry)
        {
            var block = entry.AsBlock();
            var variable = block.Required("which").AsScalar();
            var value = ReadScriptNumber(path, block.Required("value").AsScalar());
            if (!double.IsFinite(value)) { throw Error(path, entry, "Non-finite startup variable value."); }
            if (entry.Key == "set_variable") { _variables[variable] = value; }
            else { _variables[variable] = _variables.GetValueOrDefault(variable) + value; }
        }

        private void ExportModifierToVariable(string path, ScriptEntry entry)
        {
            RefreshEffectiveDistrictCaps(path);
            var block = entry.AsBlock();
            var modifier = block.Required("modifier").AsScalar();
            var variable = block.Required("variable").AsScalar();
            _variables[variable] = _effectiveDistrictCaps.GetValueOrDefault(modifier);
        }

        private async Task ExecuteSourceWhile(string path, ScriptEntry entry, Dictionary<string, string> locals)
        {
            var block = entry.AsBlock();
            var countText = Substitute(block.Required("count").AsScalar(), locals);
            double countValue;
            if (_variables.TryGetValue(countText, out var variableValue)) { countValue = variableValue; }
            else if (!double.TryParse(countText, NumberStyles.Float, CultureInfo.InvariantCulture, out countValue)) { throw Error(path, entry, "Source loop count is unresolved: " + countText + "."); }
            if (countValue != Math.Truncate(countValue) || countValue is < 0 or > 100) { throw Error(path, entry, "Unsupported source loop count."); }
            var body = block.Entries.Where(item => item.Key != "count").ToArray();
            for (var i = 0; i < (int)countValue; i++) { await ProcessEntriesAsync(path, body, locals); }
        }

        private void ExecuteDistrictHelper(string path, ScriptEntry entry)
        {
            RefreshEffectiveDistrictCaps(path);
            var args = entry.AsBlock();
            var countText = args.Required("amount").AsScalar();
            var resolvedCount = _variables.TryGetValue(countText, out var variableCount) ? variableCount : double.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var numericCount) ? numericCount : double.NaN;
            if (!double.IsFinite(resolvedCount) || resolvedCount != Math.Truncate(resolvedCount) || resolvedCount is < 0 or > 100) { throw Error(path, entry, "Unsupported helper count."); }
            var count = (int)resolvedCount;
            var districtId = args.Required("district").AsScalar();
            for (var index = 0; index < count; index++)
            {
                var free = checked(_planetSize + _context.DistrictCapacityModifier + _effectiveGeneralCapacityModifier - _districtResults.Sum(item => item.Level));
                if (free < 0) { throw Error(path, entry, "Free district slot evaluator returned a negative result."); }
                if (free >= 1) { AddDistrict(path, entry, districtId); }
                else if (free == 0 && _planetSize < 25) { _planetSize++; AddDistrict(path, entry, districtId); }
            }
        }

        private void AddDistrict(string path, ScriptEntry source, string districtId)
        {
            if (!_districts.TryGetValue(districtId, out var definition)) { throw Error(path, source, "Missing reached district definition " + districtId + "."); }
            var current = _districtResults.FindIndex(item => item.DefinitionId == districtId);
            if (current < 0)
            {
                _districtResults.Add(new(districtId, 1));
                var entries = definition.Optional("zone_slots")?.AsBlock().Entries ?? throw Error(path, source, "District has no zone_slots.");
                for (var index = 0; index < entries.Count; index++)
                {
                    var slotType = entries[index].AsScalar();
                    if (!_slots.TryGetValue(slotType, out var slotDefinition)) { throw Error(path, entries[index], "Missing reached zone slot definition."); }
                    var startZone = slotDefinition.Optional("start")?.AsScalar();
                    _slotResults.Add(new(districtId, slotType, index, startZone));
                    if (startZone is not null) { _zoneResults.Add(new(districtId, slotType, index, startZone, index)); }
                }
            }
            else { _districtResults[current] = _districtResults[current] with { Level = checked(_districtResults[current].Level + 1) }; }
            ResolvePendingBuildings(path, source);
        }

        private void AddZone(string path, ScriptEntry entry)
        {
            var block = entry.AsBlock();
            var districtId = block.Required("district").AsScalar();
            var zoneId = block.Required("zone").AsScalar();
            if (!int.TryParse(block.Required("zone_slot").AsScalar(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 0) { throw Error(path, entry, "Invalid source zone slot number."); }
            var slots = _slotResults.Where(slot => slot.DistrictId == districtId && slot.Occurrence == number).ToArray();
            if (slots.Length != 1) { throw Error(path, entry, "Zone slot target is missing or ambiguous."); }
            var target = slots[0];
            _zoneResults.RemoveAll(zone => zone.DistrictId == target.DistrictId && zone.SlotType == target.SlotType && zone.Occurrence == target.Occurrence);
            _zoneResults.Add(new(target.DistrictId, target.SlotType, target.Occurrence, zoneId, number));
            ResolvePendingBuildings(path, entry);
        }

        private void AddBuilding(string path, ScriptEntry entry, IReadOnlyDictionary<string, string> locals)
        {
            var building = entry.Block is null ? Substitute(entry.AsScalar(), locals) : Substitute(entry.Block.Required("building").AsScalar(), locals);
            var requestedZone = entry.Block?.Optional("zone")?.AsScalar();
            _pendingBuildings.Add(new(building, requestedZone, _nextBuildingOrder++));
            ResolvePendingBuildings(path, entry);
        }

        private void ResolvePendingBuildings(string path, ScriptEntry? source)
        {
            foreach (var pending in _pendingBuildings.ToArray())
            {
                var compatible = pending.ZoneId is null
                    ? _zoneResults.Where(zone => IsBuildingCompatibleWithZone(path, pending.DefinitionId, zone.ZoneId)).ToArray()
                    : _zoneResults.Where(zone => zone.ZoneId == pending.ZoneId).ToArray();
                if (compatible.Length == 0) { continue; }
                if (pending.ZoneId is not null)
                {
                    if (compatible.Length != 1) { throw source is null ? new StartupBuildingImportException("Explicit building target zone is ambiguous: " + pending.DefinitionId + ".") : Error(path, source, "Explicit building target zone is ambiguous: " + pending.DefinitionId + "."); }
                    var explicitZone = compatible[0];
                    _buildingResults.Add(new(pending.DefinitionId, explicitZone.ZoneId, explicitZone.DistrictId, explicitZone.SlotNumber, pending.Order));
                }
                else if (pending.DefinitionId == "building_capital")
                {
                    if (compatible.Length != 1) { var detail = string.Join(",", compatible.Select(item => item.DistrictId + "/" + item.SlotType + "/" + item.Occurrence)); throw source is null ? new StartupBuildingImportException("Capital compatible government zone is ambiguous [" + detail + "].") : Error(path, source, "Capital compatible government zone is ambiguous [" + detail + "]."); }
                    var capitalZone = compatible[0];
                    _buildingResults.Add(new(pending.DefinitionId, capitalZone.ZoneId, capitalZone.DistrictId, capitalZone.SlotNumber, pending.Order));
                }
                else
                {
                    var keys = compatible.Select(ZoneKey).Distinct(StringComparer.Ordinal).ToArray();
                    _buildingResults.Add(new(pending.DefinitionId, null, null, null, pending.Order, Array.AsReadOnly(keys)));
                }
                _pendingBuildings.Remove(pending);
            }
        }

        private static string ZoneKey(StartupZoneResult zone) => zone.DistrictId + "/" + zone.SlotType + "/" + zone.Occurrence + "/" + zone.ZoneId;

        private bool IsBuildingCompatibleWithZone(string path, string buildingId, string zoneId)
        {
            if (!_buildingDefinitions.TryGetValue(buildingId, out var building)) { throw new StartupBuildingImportException(path + ": missing source building definition " + buildingId + "."); }
            if (!_zoneDefinitions.TryGetValue(zoneId, out var zone)) { throw new StartupBuildingImportException(path + ": missing source zone definition " + zoneId + "."); }
            var setsEntry = building.Optional("building_sets") ?? throw new StartupBuildingImportException(path + ": reached building has no source building_sets: " + buildingId + ".");
            var buildingSets = setsEntry.AsBlock().Entries.Select(item => item.AsScalar()).ToHashSet(StringComparer.Ordinal);
            var excluded = zone.Optional("excluded_building_sets")?.AsBlock().Entries.Select(item => item.AsScalar()).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            if (buildingSets.Overlaps(excluded)) { return false; }
            var included = zone.Optional("included_building_sets")?.AsBlock();
            if (included is not null) { return included.Entries.Any(item => buildingSets.Contains(item.AsScalar())); }
            var include = zone.Optional("include")?.AsBlock();
            if (include is null) { return false; }
            var allowed = include.Entries.Select(item => item.AsScalar()).ToArray();
            if (allowed.Contains("all", StringComparer.Ordinal)) { return true; }
            return allowed.Any(buildingSets.Contains);
        }

        private void RemoveBuilding(string path, ScriptEntry entry, IReadOnlyDictionary<string, string> locals)
        {
            var building = Substitute(entry.AsScalar(), locals);
            _buildingResults.RemoveAll(item => item.DefinitionId == building);
        }

        private static void ValidateDistrictHelper(ScriptEntry helper)
        {
            if (helper.Operator != "=" || helper.Block is null || !helper.Block.Entries.Select(item => item.Key).SequenceEqual(["optimize_memory", "while"])) { throw new StartupBuildingImportException("Required district helper has an unsupported source shape."); }
            var whileBlock = RequiredBlock(helper.Block, "while");
            if (!whileBlock.Entries.Select(item => item.Key).SequenceEqual(["count", "if", "else_if"])) { throw new StartupBuildingImportException("Required district helper loop/branch order changed."); }
            RequireScalar(whileBlock, "count", "=", "$amount$");
            var ifBranch = RequiredBlock(whileBlock, "if");
            if (!ifBranch.Entries.Select(item => item.Key).SequenceEqual(["limit", "add_district"])) { throw new StartupBuildingImportException("Required free-slot branch changed."); }
            var ifLimit = RequiredBlock(ifBranch, "limit");
            if (ifLimit.Entries.Count != 1) { throw new StartupBuildingImportException("Required free-slot predicate changed."); }
            RequireScalar(ifLimit.Entries[0], "free_district_slots", ">=", "1");
            ValidateDistrictAdd(ifBranch.Entries[1]);
            var elseBranch = RequiredBlock(whileBlock, "else_if");
            if (!elseBranch.Entries.Select(item => item.Key).SequenceEqual(["limit", "change_planet_size", "add_district"])) { throw new StartupBuildingImportException("Required size-growth branch changed."); }
            var elseLimit = RequiredBlock(elseBranch, "limit");
            if (elseLimit.Entries.Count != 2) { throw new StartupBuildingImportException("Required size-growth predicate changed."); }
            RequireScalar(elseLimit.Entries[0], "free_district_slots", "=", "0");
            RequireScalar(elseLimit.Entries[1], "planet_size", "<", "25");
            RequireScalar(elseBranch.Entries[1], "change_planet_size", "=", "1");
            ValidateDistrictAdd(elseBranch.Entries[2]);
        }

        private static ScriptBlock RequiredBlock(ScriptBlock block, string key)
        {
            var entry = block.Optional(key);
            return entry?.Operator == "=" && entry.Block is not null ? entry.Block : throw new StartupBuildingImportException("Required startup helper block changed: " + key + ".");
        }

        private static void RequireScalar(ScriptBlock block, string key, string op, string value) => RequireScalar(block.Required(key), key, op, value);

        private static void RequireScalar(ScriptEntry entry, string key, string op, string value)
        {
            if (entry.Key != key || entry.Operator != op || entry.Scalar != value || entry.Block is not null) { throw new StartupBuildingImportException("Required startup helper operand changed: " + key + "."); }
        }

        private static void ValidateDistrictAdd(ScriptEntry entry)
        {
            if (entry.Key != "add_district" || entry.Operator != "=" || entry.Block is null || !entry.Block.Entries.Select(item => item.Key).SequenceEqual(["district_type", "ignore_cap"])) { throw new StartupBuildingImportException("Required district add changed."); }
            RequireScalar(entry.Block, "district_type", "=", "$district$");
            RequireScalar(entry.Block, "ignore_cap", "=", "yes");
        }

        private async Task<ScriptBlock> LoadAsync(string relative)
        {
            if (_files.TryGetValue(relative, out var existing)) { return existing.Ast; }
            _cancellation.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { throw new StartupBuildingImportException("Source path escaped game root."); }
            byte[] bytes;
            try { bytes = _sourceReader is null ? await File.ReadAllBytesAsync(full, _cancellation) : _sourceReader(relative); }
            catch (IOException exception) { throw new StartupBuildingImportException($"Required startup source {relative} cannot be read: {exception.Message}"); }
            if (bytes.Length > FILE_LIMIT || bytes.Length > TOTAL_LIMIT - _totalBytes) { throw new StartupBuildingImportException("Startup source exceeds bounded import limits."); }
            _totalBytes += bytes.Length;
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { throw new StartupBuildingImportException("Startup source is not valid UTF-8: " + relative); }
            ScriptBlock ast;
            try { ast = ParadoxParser.Parse(text, relative); }
            catch (FormatException exception) { throw new StartupBuildingImportException("Malformed startup source: " + exception.Message); }
            _files.Add(relative, new(relative, bytes, Convert.ToHexString(SHA256.HashData(bytes)), ast));
            return ast;
        }

        private async Task<OpaqueSourceFile> LoadOpaqueAsync(string relative)
        {
            if (_opaqueFiles.TryGetValue(relative, out var existing)) { return existing; }
            _cancellation.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { throw new StartupBuildingImportException("Source path escaped game root."); }
            byte[] bytes;
            try { bytes = _sourceReader is null ? await File.ReadAllBytesAsync(full, _cancellation) : _sourceReader(relative); }
            catch (IOException exception) { throw new StartupBuildingImportException($"Required startup source {relative} cannot be read: {exception.Message}"); }
            if (bytes.Length > FILE_LIMIT || bytes.Length > TOTAL_LIMIT - _totalBytes) { throw new StartupBuildingImportException("Startup source exceeds bounded import limits."); }
            _totalBytes += bytes.Length;
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { throw new StartupBuildingImportException("Startup source is not valid UTF-8: " + relative); }
            var file = new OpaqueSourceFile(relative, bytes, Convert.ToHexString(SHA256.HashData(bytes)), text);
            _opaqueFiles.Add(relative, file);
            return file;
        }

        private async Task VerifyStableAsync()
        {
            foreach (var file in _files.Values)
            {
                _cancellation.ThrowIfCancellationRequested();
                var full = Path.Combine(_root, file.Path.Replace('/', Path.DirectorySeparatorChar));
                var current = _sourceReader is null ? await File.ReadAllBytesAsync(full, _cancellation) : _sourceReader(file.Path);
                if (current.LongLength != file.Bytes.LongLength || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), Convert.FromHexString(file.Hash))) { throw new StartupBuildingImportException("Startup source changed during import: " + file.Path); }
            }
            foreach (var file in _opaqueFiles.Values)
            {
                _cancellation.ThrowIfCancellationRequested();
                var full = Path.Combine(_root, file.Path.Replace('/', Path.DirectorySeparatorChar));
                var current = _sourceReader is null ? await File.ReadAllBytesAsync(full, _cancellation) : _sourceReader(file.Path);
                if (current.LongLength != file.Bytes.LongLength || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), Convert.FromHexString(file.Hash))) { throw new StartupBuildingImportException("Startup source changed during import: " + file.Path); }
            }
        }

        private static ScriptEntry Unique(ScriptBlock source, string id)
        {
            var items = source.Named(id).ToArray();
            return items.Length == 1 ? items[0] : throw new StartupBuildingImportException("Expected one source effect " + id + ".");
        }

        private static string Substitute(string value, IReadOnlyDictionary<string, string> locals)
        {
            if (value.Length >= 3 && value[0] == '$' && value[^1] == '$' && locals.TryGetValue(value[1..^1], out var result)) { return result; }
            if (value.Contains('$', StringComparison.Ordinal)) { throw new StartupBuildingImportException("Startup effect has an unresolved parameter: " + value); }
            return value;
        }

        private static StartupBuildingImportException Error(string path, ScriptEntry entry, string message) => new($"{path}:{entry.Line}:{entry.Column}: {message}");
        private sealed record PendingBuilding(string DefinitionId, string? ZoneId, int Order);
        private sealed class UnresolvedStartupConditionException(string sourcePath, string condition, string detail) : Exception
        {
            public string SourcePath { get; } = sourcePath;
            public string Condition { get; } = condition;
            public string Detail { get; } = detail;
        }
        private sealed record SourceFile(string Path, byte[] Bytes, string Hash, ScriptBlock Ast);
        private sealed record OpaqueSourceFile(string Path, byte[] Bytes, string Hash, string Text);
    }

}
