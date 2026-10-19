using Stellaris.Core.Construction;
using Stellaris.Core.Research;
using Stellaris.Core.Economy;
using Stellaris.Session.M3.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Scripting;
using System.Collections.Frozen;
using Stellaris.Core.Content.Galaxy;
using System.Security.Cryptography;
using System.Text;

namespace Stellaris.Session.M3;

/// <summary>Source helpers и принятая пользователем политика весов: только месторождения реализованных колоний.</summary>
public sealed class M3RareDepositResearchPolicy
{
    public const string PolicyId = "human-resource-tech-colony-deposits-only-2026-10-14-v1";
    private const string TriggerSource = "common/scripted_triggers/01_scripted_triggers_buildings.txt";
    private const string ClassSource = "common/planet_classes/00_planet_classes.txt";
    private const string TechnologySource = "common/technology/00_strategic_resources_tech.txt";
    private readonly IReadOnlyDictionary<string, string[]> _helperDeposits;
    private readonly IReadOnlyDictionary<string, string> _districtSets;
    private readonly IReadOnlyDictionary<string, string> _weightQueries;
    public ContentManifest Manifest { get; }

    private M3RareDepositResearchPolicy(ContentManifest manifest, Dictionary<string, string[]> helpers,
        Dictionary<string, string> districtSets, Dictionary<string, string> queries)
    {
        Manifest = manifest;
        _helperDeposits = helpers.ToFrozenDictionary(StringComparer.Ordinal);
        _districtSets = districtSets.ToFrozenDictionary(StringComparer.Ordinal);
        _weightQueries = queries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public static M3RareDepositResearchPolicy Import(string gameRoot)
    {
        var source = new ContentSession(gameRoot);
        if (source.Version.TrimStart('v') != "4.5.2") { throw new InvalidDataException("Rare deposit policy требует 4.5.2."); }
        foreach (var path in new[] { TriggerSource, ClassSource, TechnologySource })
        {
            if (new FileInfo(Path.Combine(gameRoot, path)).Length > 2 * 1024 * 1024) { throw new InvalidDataException("Rare deposit source превышает бюджет."); }
        }
        var triggers = source.LoadFile(TriggerSource).Root;
        var habitat = Definition(triggers, "planet_is_habitat_equivalent");
        RequireScalars(habitat, ("uses_district_set", "habitat"));
        var helpers = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var resource in new[] { "volatile_motes", "exotic_gases", "rare_crystals" })
        {
            var helper = Definition(triggers, "has_" + resource + "_deposit");
            RequireKeys(helper, "if", "else");
            var conditional = Definition(helper, "if");
            RequireKeys(conditional, "limit", "solar_system");
            RequireScalars(Definition(conditional, "limit"), ("planet_is_habitat_equivalent", "yes"));
            var system = Definition(conditional, "solar_system"); RequireKeys(system, "any_system_planet");
            var planet = Definition(system, "any_system_planet"); RequireKeys(planet, "has_deposit_for", "has_resource");
            if (planet.Optional("has_deposit_for") is not { Operator: "=", Scalar: "shipclass_mining_station" }) { throw Shape(); }
            var amount = Definition(planet, "has_resource"); RequireKeys(amount, "type", "amount");
            if (amount.Optional("type") is not { Operator: "=", Scalar: var type } || type != resource
                || amount.Optional("amount") is not { Operator: ">", Scalar: "0" }) { throw Shape(); }
            var ordinary = Definition(helper, "else"); RequireKeys(ordinary, "OR");
            var deposits = Definition(ordinary, "OR").Entries;
            if (deposits.Count is < 1 or > 32 || deposits.Any(entry => entry is not { Key: "has_deposit", Operator: "=", Scalar: not null }
                || !System.Text.RegularExpressions.Regex.IsMatch(entry.Scalar!, "^d_[a-z0-9_]{1,120}$"))) { throw Shape(); }
            var ids = deposits.Select(entry => entry.Scalar!).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) { throw Shape(); }
            helpers.Add("has_" + resource + "_deposit", ids);
        }
        // Planet classes содержат tagged hsv blocks; используем существующий source AST reader, не меняя gameplay parser.
        var classPath = Path.Combine(gameRoot, ClassSource);
        var classBytes = File.ReadAllBytes(classPath);
        var classHash = Convert.ToHexString(SHA256.HashData(classBytes));
        var classes = GalaxyScriptReader.Parse(new UTF8Encoding(false, true).GetString(classBytes), ClassSource);
        var sets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in classes.Entries.Where(entry => entry.Key?.StartsWith("pc_", StringComparison.Ordinal) == true && entry.Operator == "=" && entry.Value.Kind == GalaxySyntaxKind.Block))
        {
            if (entry.Value.Entries.Where(field => field.Key == "district_set").ToArray() is [var districtSet]
                && districtSet.Operator == "=" && districtSet.Value.Kind == GalaxySyntaxKind.Scalar && districtSet.Value.Text is { } set)
            { sets.Add(entry.Key!, set); }
        }
        var technologies = source.LoadFile(TechnologySource).Root;
        var queries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in new[] { "volatile_motes", "exotic_gases", "rare_crystals" })
        {
            var definition = Definition(technologies, "tech_" + resource);
            var weight = Definition(definition, "weight_modifier");
            var within = FindWithinBorder(weight).ToArray();
            if (within is not [var query] || !IsDepositQuery(query, resource)) { throw Shape(); }
            queries.Add(Serialize(query), resource);
        }
        var manifest = source.Manifest(PolicyId, ["Prototype: weight resource-tech учитывает только месторождения реализованных колоний; орбитальные и особые источники не моделируются.",
            "Неподдержанные habitat helpers, скрытые matching features и неизвестные формы остаются unknown."]);
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(classPath))) != classHash) { throw new IOException("Planet class source изменился во время импорта rare research policy."); }
        manifest = manifest with { Files = Array.AsReadOnly(manifest.Files.Append(new ManifestFile(ClassSource, classHash, classBytes.LongLength)).OrderBy(file => file.Path, StringComparer.Ordinal).ToArray()) };
        return new(manifest, helpers, sets, queries);
    }

    public bool? EvaluateHelper(string helper, M3StartupColony colony)
    {
        if (!_helperDeposits.TryGetValue(helper, out var deposits) || colony.Homeworld.PlanetClass != colony.Plan.Context.PlanetClass
            || !_districtSets.TryGetValue(colony.Homeworld.PlanetClass, out var districtSet) || districtSet == "habitat") { return null; }
        return Any(deposits.Select(id => HasDeposit(colony, id)));
    }

    public bool? EvaluateWeightQuery(ScriptBlock query, IReadOnlyList<M3StartupColony> colonies)
        => IsSupportedWeightQuery(query) ? Any(colonies.Select(colony => Evaluate(query, colony))) : null;

    public bool IsSupportedWeightQuery(ScriptBlock query) => _weightQueries.TryGetValue(Serialize(query), out var resource) && IsDepositQuery(query, resource);

    public static IEnumerable<ScriptBlock> FindWithinBorder(ScriptBlock? block)
    {
        if (block is null) { yield break; }
        foreach (var entry in block.Entries.Where(entry => entry.Block is not null))
        {
            if (entry.Key == "any_planet_within_border") { yield return entry.AsBlock(); }
            foreach (var child in FindWithinBorder(entry.AsBlock())) { yield return child; }
        }
    }

    public static string Serialize(ScriptBlock block) => string.Join(";", block.Entries.Select(entry => entry.Key + entry.Operator + (entry.Scalar ?? "{" + Serialize(entry.AsBlock()) + "}")));
    private static bool? HasDeposit(M3StartupColony colony, string id) => colony.Features.Any(feature => feature.DefinitionId == id && !feature.IsHidden) ? true
        : colony.Features.Any(feature => feature.DefinitionId == id) ? null : false;
    private static bool? Any(IEnumerable<bool?> values) { var items = values.ToArray(); return items.Any(value => value == true) ? true : items.Any(value => value is null) ? null : false; }
    private static bool? All(IEnumerable<bool?> values) { var items = values.ToArray(); return items.Any(value => value == false) ? false : items.Any(value => value is null) ? null : true; }
    private static bool? Evaluate(ScriptBlock block, M3StartupColony colony) => All(block.Entries.Select(entry =>
    {
        if (entry.Key == "has_deposit") { return HasDeposit(colony, entry.Scalar!); }
        var value = entry.Key is "AND" or "NOT" ? Evaluate(entry.AsBlock(), colony) : Any(entry.AsBlock().Entries.Select(child => Evaluate(new ScriptBlock([child]), colony)));
        return entry.Key is "NOT" or "NOR" ? value is null ? null : !value.Value : value;
    }));
    private static bool IsDepositQuery(ScriptBlock block, string resource, int depth = 0) => depth < 8 && block.Entries.Count is > 0 and <= 32
        && block.Entries.All(entry => entry.Operator == "=" && (entry.Key == "has_deposit" && entry.Scalar is { } id
            && Enumerable.Range(1, 5).Any(index => id == "d_" + resource + "_" + index)
            || entry.Key is "AND" or "OR" or "NOT" or "NOR" && entry.Block is not null && IsDepositQuery(entry.AsBlock(), resource, depth + 1)));
    private static ScriptBlock Definition(ScriptBlock block, string key) => block.Named(key).ToArray() is [var entry] && entry.Operator == "=" && entry.Block is not null ? entry.AsBlock() : throw Shape();
    private static void RequireKeys(ScriptBlock block, params string[] keys) { if (block.Entries.Count != keys.Length || keys.Any(key => block.Named(key).Count() != 1)) { throw Shape(); } }
    private static void RequireScalars(ScriptBlock block, params (string Key, string Value)[] values)
    { RequireKeys(block, values.Select(value => value.Key).ToArray()); if (values.Any(value => block.Optional(value.Key) is not { Operator: "=", Scalar: var scalar } || scalar != value.Value)) { throw Shape(); } }
    private static InvalidDataException Shape() => new("Неподдержанная source AST форма rare deposit research policy.");
}

public sealed partial class M3GameSession
{
    private ResearchEffectProjection ResearchEffectsFor(Participant player, ResearchCountry? country = null, EconomyWorld? world = null)
        => ResearchEffects.Project(country ?? player.Research!, _sourceEconomy!.Catalog, _sourceEconomy.RuntimeJobs,
            player.Template!.Ethics.Concat(player.Template.Civics).Append(player.Template.OriginId).ToHashSet(StringComparer.Ordinal),
            player.Planet is { } planet ? (world ?? _world).GetEffectiveJobResourceClosure(planet) : null);

    private EntityId EconomyPlanetFor(ColonyDefinition colony)
    {
        var participant = _participants.Single(player => player.Id == colony.OwnerParticipantId);
        // Fixture хранит исторические display IDs 1/4; authoritative entity — выданный owner при Join.
        // Дополнительные колонии требуют отдельного authoritative binding, его пока нет.
        if (participant.Planet is not { } planet)
        { throw new InvalidOperationException("Colony economy entity mapping is unavailable."); }
        return planet;
    }

    private EntityId[] ResearchPlanets(Participant player, SpaceWorld space)
    {
        var colonies = space.Colonies.Where(colony => colony.OwnerParticipantId == player.Id).ToArray();
        if (colonies.Length != 1) { throw new InvalidOperationException("Multiple colony economy entity bindings are unavailable."); }
        return colonies.Select(EconomyPlanetFor).Distinct().ToArray();
    }

    private M3ConstructionEvaluation ConstructionEvaluationFor(Participant player, M3StartupColony startup,
        ConstructionLiveRuntime runtime, ResearchCountry? research = null,
        IReadOnlyDictionary<string, ConstructionLiveRuntime>? systemRuntimes = null)
    {
        var country = research ?? player.Research;
        var modifiers = country is not null && _sourceEconomy is not null ? ResearchEffectsFor(player, country).ConstructionModifiers : [];
        decimal Bonus(string key) => modifiers.Where(modifier => modifier.SourceKey == key).Sum(modifier => modifier.Value);
        var building = _constructionContent!.PresetModifiers(player.Template!, ConstructionKind.Building);
        var district = _constructionContent.PresetModifiers(player.Template!, ConstructionKind.District);
        var binding = M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic);
        var context = ConstructionBaseContextFor(player, startup, runtime, country);
        if (_galaxyCandidate is not null && _space is not null)
        {
            var contexts = new Dictionary<string, ConstructionContext>(StringComparer.Ordinal) { [startup.ColonyId] = context };
            foreach (var colony in _space.Colonies.Where(colony => colony.Id != startup.ColonyId))
            {
                var owner = _participants.SingleOrDefault(participant => participant.Id == colony.OwnerParticipantId);
                if (owner?.Template is not null && _startupColonies.TryGetValue(colony.Id, out var otherStartup)
                    && (systemRuntimes ?? _constructionRuntimes).TryGetValue(colony.Id, out var otherRuntime))
                    contexts.Add(colony.Id, ConstructionBaseContextFor(owner, otherStartup, otherRuntime, owner.Research));
            }
            if (M3ConstructionSystemGraph.Bind(_galaxyCandidate.Generated, _space, startup.ColonyId, contexts) is { } graph)
                context = context.WithSystemGraph(graph, graph.RootBodyId);
        }
        return new(context,
            M3ConstructionContent.EconomyFacts(startup), new("m3-source-research-effects-v1",
                checked(building.Cost + Bonus("planet_structures_cost_mult")),
                checked(building.Speed + Bonus("planet_building_build_speed_mult")),
                checked(district.Speed + Bonus("planet_district_build_speed_mult"))), binding);
    }

    private ConstructionContext ConstructionBaseContextFor(Participant player, M3StartupColony startup,
        ConstructionLiveRuntime runtime, ResearchCountry? country)
    {
        var binding = M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic);
        // Native trigger: положительные расходы CG достаточны для true; прочие ветви не угадываем.
        bool? usesConsumerGoods = _space is not null && ResearchPlanets(player, _space)
            .Any(planet => _world.PreviewPlanetReport(planet).Upkeep.GetValueOrDefault("consumer_goods") > 0) ? true : null;
        return _constructionContent!.Context(startup, player.Template!, runtime, binding,
                country?.Researched ?? _researchedTechnologies?.Invoke(player.Id) ?? player.Template!.StartingTechnologies, _sourceEconomy?.InitialPopulation, usesConsumerGoods,
                ownerSpecies: player.Country is { } owner ? _world.GetOwnedSpeciesSnapshot(owner) : null,
                ownerFounderSpeciesId: player.Country is { } founderOwner ? _world.GetFounderSpecies(founderOwner)?.Id : null,
                ownerCountryEntityId: player.Country,
                foreignOwnership: player.Planet is { } ownershipPlanet ? _world.GetPlanetForeignOwnership(ownershipPlanet) : null,
                planetEntityId: player.Planet);
    }

    private ResearchCountry CreateResearch(ConstructionOrdinaryTemplate template, ResearchFacts? facts = null)
        => ResearchCountry.Create(_researchCatalog!, (ulong)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue), template.StartingTechnologies, facts ?? ResearchFactsFor(template));

    private ResearchFacts ResearchFactsFor(ConstructionOrdinaryTemplate template, int? ownerId = null, int elapsedDay = 0,
        IReadOnlyDictionary<string, M3StartupColony>? startups = null, IReadOnlyDictionary<string, ConstructionLiveRuntime>? construction = null)
    {
        var values = new Dictionary<string, bool?>(StringComparer.Ordinal);
        void Fact(string predicate, string argument, bool value) => values[ResearchFacts.Key("country", predicate, argument)] = value;
        foreach (var (predicate, value) in new Dictionary<string, bool>
        {
            ["is_ai"] = false, ["is_gestalt"] = false, ["is_machine_empire"] = false,
            ["is_hive_empire"] = false, ["is_robot_empire"] = false, ["is_wilderness_empire"] = false,
            ["is_nomadic_empire"] = false, ["is_nomadic"] = false, ["is_regular_empire"] = true, ["is_fallen_empire"] = false,
            ["country_uses_food"] = true, ["is_low_tech_start"] = false,
            ["country_uses_bio_ships"] = false,
            ["is_individual_machine"] = false, ["is_subject"] = false,
            ["is_mechanical_empire"] = false, ["is_lithoid_empire"] = false, ["is_infernal_empire"] = false,
            ["is_guided_sapience_empire"] = false,
            // 08_scripted_triggers_shroud:214: два source civic.
            ["is_tankbound_empire"] = template.Civics.Contains("civic_tankbound") || template.Civics.Contains("civic_tankbound_corporate"),
            ["is_catalytic_empire"] = template.Civics.Any(id => id is "civic_catalytic_processing" or "civic_corporate_catalytic_processing" or "civic_hive_catalytic_processing" or "civic_machine_catalytic_processing"),
            ["has_federation"] = false, ["is_galactic_community_member"] = M3RuntimeOwnerFacts.OwnerGalacticCommunityMember,
            ["is_beastmasters_empire"] = template.Civics.Contains("civic_beastmasters"),
            ["is_materialist"] = template.Ethics.Any(id => id.Contains("materialist", StringComparison.Ordinal)),
            ["is_militarist"] = template.Ethics.Any(id => id.Contains("militarist", StringComparison.Ordinal)),
            ["is_pacifist"] = template.Ethics.Any(id => id.Contains("pacifist", StringComparison.Ordinal)),
            ["is_spiritualist"] = template.Ethics.Any(id => id.Contains("spiritualist", StringComparison.Ordinal))
        })
        { Fact(predicate, "yes", value); Fact(predicate, "no", !value); }
        // Полные выбранные множества подтверждают также отсутствие другого civic/ethic/origin.
        foreach (var technology in _researchCatalog!.Technologies.Values)
        {
            Visit(technology.Potential); Visit(technology.WeightRules); Visit(technology.StartingPotential); Visit(technology.CostRules);
            foreach (var trigger in technology.SwapTriggers) { Visit(trigger); }
        }
        foreach (var tier in _researchCatalog.Tiers.Values) { Visit(tier.WeightRules); }
        void Visit(Core.Scripting.ScriptBlock? block)
        {
            if (block is null) { return; }
            foreach (var entry in block.Entries)
            {
                if (entry.Scalar is { } argument)
                {
                    bool? value = entry.Key switch
                    {
                        "has_ethic" => template.Ethics.Contains(argument),
                        "has_valid_civic" or "has_civic" => template.Civics.Contains(argument),
                        "has_origin" => template.OriginId == argument,
                        "has_authority" => template.AuthorityId == argument,
                        "has_ascension_perk" => M3RuntimeOwnerFacts.AscensionPerks.Contains(argument),
                        "has_tradition" => M3RuntimeOwnerFacts.HeldTraditions.Contains(argument),
                        "has_country_flag" or "has_relic" or "is_active_resolution" => false,
                        _ => null
                    };
                    if (value is { } known) { Fact(entry.Key, argument, known); }
                }
                if (entry.Block is not null && entry.Key is "has_trait_in_council" or "is_specialist_subject_type" or "any_owned_leader")
                { Fact(entry.Key, Serialize(entry.Block), false); }
                Visit(entry.Block);
            }
        }
        var owned = (startups ?? _startupColonies).Values.Where(colony => colony.OwnerParticipantId == ownerId).ToArray();
        var numbers = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [ResearchFacts.NumericKey("country", "years_passed")] = elapsedDay / 360m,
            [ResearchFacts.NumericKey("country", "num_owned_planets")] = owned.Length
        };
        var planets = owned.Select(colony =>
        {
            var facts = new Dictionary<string, bool?>(values, StringComparer.Ordinal);
            var installed = (construction ?? _constructionRuntimes).GetValueOrDefault(colony.ColonyId)?.Installed.Values;
            void PlanetFact(string predicate, bool value)
            { facts[ResearchFacts.Key("planet", predicate, "yes")] = value; facts[ResearchFacts.Key("planet", predicate, "no")] = !value; }
            // Исходные helpers: 00_scripted_triggers:4354 и 01_scripted_triggers_buildings:746–798.
            if (installed is not null) { PlanetFact("has_any_planet_farming_district", installed.Any(item => item.Kind == ConstructionKind.District && item.DefinitionId is "district_farming" or "district_rw_farming" or "district_orchard_forests")); }
            PlanetFact("has_any_agriculture_zone", colony.Zones.Any(zone => zone.ZoneId is "zone_food" or "zone_food_wilderness" or "zone_food_ring_world" or "zone_food_hive" or "zone_ark_food"
                or "zone_exotic_gases" or "zone_exotic_gases_nexus" or "zone_exotic_gases_hive" or "zone_habitat_exotic_gases" or "zone_anglers" or "zone_agrarian_urban" or "zone_agrarian_anglers" or "zone_habitat_hydroponics"));
            if (_rareDepositResearchPolicy is not null)
            {
                foreach (var resource in new[] { "volatile_motes", "exotic_gases", "rare_crystals" })
                {
                    var helper = "has_" + resource + "_deposit";
                    var truth = _rareDepositResearchPolicy.EvaluateHelper(helper, colony);
                    facts[ResearchFacts.Key("planet", helper, "yes")] = truth;
                    facts[ResearchFacts.Key("planet", helper, "no")] = truth is null ? null : !truth.Value;
                }
            }
            foreach (var technology in _researchCatalog.Technologies.Values)
            { PlanetVisit(technology.Potential); PlanetVisit(technology.WeightRules); PlanetVisit(technology.CostRules); }
            void PlanetVisit(Core.Scripting.ScriptBlock? block)
            {
                if (block is null) { return; }
                foreach (var entry in block.Entries)
                {
                    if (entry.Scalar is { } argument)
                    {
                        bool? value = entry.Key switch
                        {
                            "is_planet_class" => colony.Homeworld.PlanetClass == argument,
                            "has_building" when installed is not null => installed.Any(item => item.Kind == ConstructionKind.Building && item.DefinitionId == argument),
                            "has_deposit" => colony.Features.Any(feature => feature.DefinitionId == argument && !feature.IsHidden) ? true
                                : colony.Features.Any(feature => feature.DefinitionId == argument) ? null : false,
                            "has_planet_flag" => false,
                            _ => null
                        };
                        if (value is { } known) { facts[ResearchFacts.Key("planet", entry.Key, argument)] = known; }
                    }
                    PlanetVisit(entry.Block);
                }
            }
            var planetNumbers = new Dictionary<string, decimal>(numbers, StringComparer.Ordinal);
            if (_sourceEconomy is not null) { planetNumbers[ResearchFacts.NumericKey("planet", "pop_amount")] = _sourceEconomy.InitialPopulation; }
            if (_constructionContent is not null && installed is not null)
            {
                foreach (var id in _constructionContent.Content.Economy.Buildings.Keys)
                { planetNumbers[ResearchFacts.NumericKey("planet", "num_buildings", id)] = installed.Count(item => item.Kind == ConstructionKind.Building && item.DefinitionId == id); }
                foreach (var id in _constructionContent.Content.Economy.Districts.Keys)
                { planetNumbers[ResearchFacts.NumericKey("planet", "num_districts", id)] = installed.Count(item => item.Kind == ConstructionKind.District && item.DefinitionId == id); }
            }
            var founder = new Dictionary<string, bool?>(facts, StringComparer.Ordinal);
            foreach (var (predicate, value) in new Dictionary<string, bool>
            { ["is_sapient"] = true, ["is_livestock"] = false, ["has_disconnected_drone_citizenship_type"] = false })
            {
                founder[ResearchFacts.Key("pop_group", predicate, "yes")] = value;
                founder[ResearchFacts.Key("pop_group", predicate, "no")] = !value;
            }
            return new ResearchFacts(facts, planetNumbers, new Dictionary<string, IReadOnlyList<ResearchFacts>>(StringComparer.Ordinal)
            { [ResearchFacts.NumericKey("planet", "any_owned_pop_group")] = Array.AsReadOnly(new[] { new ResearchFacts(founder) }) });
        }).ToArray();
        if (_rareDepositResearchPolicy is not null)
        {
            // Fallback key общий для ResearchRules: не публикуем weight-only факт, если тот же запрос используется вне weights.
            var outsideWeightQueries = _researchCatalog.Technologies.Values.SelectMany(technology =>
                new[] { technology.Potential, technology.StartingPotential, technology.CostRules }.Concat(technology.SwapTriggers))
                .SelectMany(M3RareDepositResearchPolicy.FindWithinBorder).Select(Serialize).ToHashSet(StringComparer.Ordinal);
            foreach (var query in _researchCatalog.Technologies.Values.SelectMany(technology => M3RareDepositResearchPolicy.FindWithinBorder(technology.WeightRules)))
            {
                var key = Serialize(query);
                if (!_rareDepositResearchPolicy.IsSupportedWeightQuery(query) || outsideWeightQueries.Contains(key)) { continue; }
                values[ResearchFacts.Key("country", "any_planet_within_border", key)] = _rareDepositResearchPolicy.EvaluateWeightQuery(query, owned);
            }
        }
        return new(values, numbers, new Dictionary<string, IReadOnlyList<ResearchFacts>>(StringComparer.Ordinal)
        { [ResearchFacts.NumericKey("country", "any_owned_planet")] = Array.AsReadOnly(planets), [ResearchFacts.NumericKey("country", "every_owned_planet")] = Array.AsReadOnly(planets) });
    }

    private static string Serialize(Core.Scripting.ScriptBlock block) => string.Join(";", block.Entries.Select(entry => entry.Key + entry.Operator + (entry.Scalar ?? "{" + Serialize(entry.AsBlock()) + "}")));

    private void SelectResearch(Participant participant, M3ClientCommand command)
    {
        RequirePlayer(participant);
        RequireRunning();
        if (participant.Research is null) { throw Reject("OUT_OF_SCOPE", "Исследования недоступны."); }
        if (command.ExpectedResearchRevision < 0 || new[] { command.Area, command.TechnologyId }.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160 || id.Any(char.IsControl)))
        { throw Reject("INVALID_COMMAND", "Неверное направление, технология или revision."); }
        try { participant.Research = participant.Research.WithFacts(ResearchFactsFor(participant.Template!, participant.Id, _space!.ElapsedDay), fillEmptyChoices: false).Select(command.Area!, command.TechnologyId!, command.ExpectedResearchRevision!.Value); }
        catch (InvalidOperationException exception) when (exception.Message is "STALE_REVISION" or "RESEARCH_UNAVAILABLE")
        { throw Reject(exception.Message, "Выбор исследования отклонён."); }
        catch (ArgumentException) { throw Reject("INVALID_RESEARCH_SELECTION", "Технология отсутствует в предложениях направления."); }
    }

    private M3ResearchView? ProjectResearch(IEnumerable<Participant> visible)
    {
        if (_researchCatalog is null) { return null; }
        string? Key(string candidate) => _technologyKeys.Contains(candidate) ? candidate : null;
        var empires = visible.Where(player => player.Research is not null).Select(player =>
        {
            var country = player.Research!;
            var identities = player.Template!.Ethics.Concat(player.Template.Civics).Append(player.Template.OriginId).ToHashSet(StringComparer.Ordinal);
            var speed = _sourceEconomy is null ? ResearchCountry.AreaIds.ToDictionary(area => area, _ => 1 + country.Catalog.NoLeaderPenalty, StringComparer.Ordinal)
                : ResearchEffectsFor(player).AreaResearchMultipliers;
            M3TechnologyView Technology(string id)
            {
                var definition = country.Catalog.Technologies[id];
                var applied = _sourceEconomy is null ? new HashSet<string>(StringComparer.Ordinal)
                    : ResearchEffects.AppliedKeys(country, id, _sourceEconomy.Catalog, _sourceEconomy.RuntimeJobs, identities,
                        player.Planet is { } planet ? _world.GetEffectiveJobResourceClosure(planet) : null);
                var effects = ResearchEffects.EffectDetails(country, id).Select(effect =>
                {
                    var format = effect.Key.EndsWith("_mult", StringComparison.Ordinal) || effect.Key is "all_technology_research_speed"
                        || ResearchCountry.AreaIds.Any(area => effect.Key == "country_" + area + "_tech_research_speed") ? "percent" : "number";
                    return new M3TechnologyEffectView(effect.Diagnostic is null ? effect.Key : "Значение эффекта не разрешено: " + effect.Key,
                        applied.Contains(effect.Key) && effect.Value is not null ? "active" : "deferred", Key("mod_" + effect.Key),
                        effect.Value, format, SourceKey: effect.Key, DiagnosticCode: effect.Diagnostic);
                }).ToList();
                effects.AddRange(definition.Effects.Where(effect => !effect.StartsWith("modifier:", StringComparison.Ordinal)).Select(effect =>
                {
                    var key = effect[(effect.IndexOf(':') + 1)..];
                    return new M3TechnologyEffectView("Специальный игровой эффект: " + key, "deferred", Key("feature_" + key) ?? Key(key), SourceKey: key);
                }));
                if (_constructionContent is not null)
                {
                    foreach (var recipe in _constructionContent.Content.Catalogs.Values.Distinct().SelectMany(catalog => catalog.Definitions.Values)
                        .Where(recipe => recipe.Kind == "building" && recipe.Fields.Where(field => field.Key == "prerequisites").SelectMany(field => field.Children).Any(field => field.Scalar == id)))
                    { effects.Add(new("После изучения открывает здание: " + recipe.Id, "active", Key(recipe.Id), TargetDefinitionId: recipe.Id, SourceKey: "unlock_building")); }
                }
                var category = definition.Categories.FirstOrDefault() ?? "";
                return new(id, id, definition.Area, category, country.CurrentCost(id),
                    definition.Tier, definition.Rare, Array.AsReadOnly(effects.ToArray()), country.Assess(id).Weight, country.Levels.GetValueOrDefault(id), definition.Icon,
                    Key(id), Key(id + "_desc"), Key(category));
            }
            var reports = _space is null ? [] : ResearchPlanets(player, _space).Select(_world.PreviewPlanetReport).ToArray();
            var areas = ResearchCountry.AreaIds.Select(area =>
            {
                var state = country.Areas[area];
                return new M3ResearchAreaView(area, state.ActiveTechnologyId, state.ActiveTechnologyId is { } active ? state.Progress.GetValueOrDefault(active) : 0,
                    reports.Sum(report => report.Net.GetValueOrDefault(area + "_research")) * (state.ActiveTechnologyId is null ? 1m : speed[area]),
                    _world.GetBalance(player.Country!.Value, area + "_research"), Array.AsReadOnly(state.Choices.Select(Technology).ToArray()), state.LastCompletedTechnologyId);
            }).ToArray();
            var assessments = country.Catalog.Technologies.Keys.Select(id => country.Assess(id)).ToArray();
            var unavailable = assessments.Where(result => !result.Available && result.Gates.Any(gate => gate.Contains("unknown", StringComparison.Ordinal) || gate.Contains("unsupported", StringComparison.Ordinal) || gate == "conditional-or-missing-cost"))
                .Select(result => new M3ResearchUnavailableView(result.TechnologyId, result.TechnologyId, result.Gates, Key(result.TechnologyId))).ToArray();
            return new M3ResearchEmpireView(player.Id, country.Revision, Array.AsReadOnly(areas), Array.AsReadOnly(country.Researched.Order(StringComparer.Ordinal).Select(Technology).ToArray()),
                Array.AsReadOnly(unavailable), country.Catalog.Technologies.Count, assessments.Count(result => result.Available));
        }).ToArray();
        return new(Array.AsReadOnly(empires));
    }
}
