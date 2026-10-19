using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using Stellaris.Core.Assets;
using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Economy;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;

M3StartupCatalog? sourceStartup = null;
M3BaselineEconomyProfile? sourceEconomy = null;
Stellaris.Core.Content.PlanetEconomy.PlanetEconomyCatalog? sourceCatalog = null;
M3ConstructionContent? sourceConstruction = null;
TechnologyCatalog? sourceResearch = null;
M3RareDepositResearchPolicy? sourceRareDepositPolicy = null;
if (args.Length >= 2 && args[0] == "--game-root")
{
    sourceStartup = await M3StartupCatalog.ImportAsync(args[1]);
    sourceCatalog = await new Stellaris.Core.Content.PlanetEconomy.PlanetEconomyImporter().ImportAsync(args[1]);
    sourceConstruction = await M3ConstructionContent.ImportAsync(args[1], sourceCatalog);
    sourceResearch = new TechnologyImporter().Import(args[1]);
    sourceRareDepositPolicy = M3RareDepositResearchPolicy.Import(args[1]);
    var startResources = new StartupPopulationResourceImporter().Import(args[1]);
    var traitBonuses = new StartupTraitWorkforceImporter().Import(args[1]);
    sourceEconomy = new M3BaselineEconomyProfile(sourceCatalog, startResources, traitBonuses.BonusesByTemplate, sourceResearch.CategoryParents);
}

var manifestRoot = Path.Combine(Path.GetTempPath(), "Stellaris-M3SessionChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(manifestRoot);
M3AssetManifest manifest;
try
{
    File.WriteAllText(Path.Combine(manifestRoot, "launcher-settings.json"), "{\"rawVersion\":\"v4.5.2\"}");
    foreach (var relative in M3AssetManifestBuilder.ProfilePaths)
    {
        var path = Path.Combine(manifestRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "synthetic");
    }
    manifest = await new M3AssetManifestBuilder().BuildAsync(manifestRoot);
}
finally
{
    Directory.Delete(manifestRoot, true);
}
var researchOnly = args.Contains("--research-only") || args.Contains("--effects-only");
if (args.Contains("--rare-only") || args.Contains("--rare-binding-only") || args.Contains("--rare-diagnostics-only"))
{
    try
    {
        if (!args.Contains("--rare-diagnostics-only"))
        {
            RareDepositResearchFacts(); Console.WriteLine("PASS: Source rare deposit helpers/weight policy: positive/negative/hidden/habitat/unsupported/collision");
            RareResourceSourceBindings(); Console.WriteLine("PASS: Source bindings трёх industrial producers/category/effective Workforce/upkeep/rebuild и nanites zero flow");
        }
        if (!args.Contains("--rare-binding-only"))
        {
            RareResourceNaturalRuntime();
            if (!args.Contains("--rare-diagnostics-only")) Console.WriteLine("PASS: Source science/reserve → refinery → gases from zero → paid lab upgrade → recurring upkeep");
        }
        return 0;
    }
    catch (Exception exception) { Console.Error.WriteLine("FAIL: Natural rare resource runtime\n" + exception); return 1; }
}
var checks = new (string Name, Action Run)[]
{
    ("Fixture для произвольного числа игроков", PlayerFixtureChecks.Run),
    ("Таблицы исследований без потери данных", ResearchWireChecks.Run),
    ("Штатные ключи технологий без переводов и RINFIX", TechnologyKeyChecks.Run),
    ("Лобби, профиль, pending observer и связь planet 1/4", Lobby),
    ("Состав лобби, kick, host handoff и монотонные participant IDs", LobbyMembership),
    ("Reconnect, старое соединение и исходный ACK", Reconnect),
    ("Observer perspectives: privacy, authorization, replay and reconnect", ObserverPerspectives),
    ("Строгий pure DTO и безопасные отказы движения", Commands),
    ("Даты 0/29/30/359/360 и step cache", Calendar),
    ("Монотонные часы, deadline, speed, no catchup", Clock),
    ("AFK, host disconnect, приказы и права", Disconnection),
    ("Атомарная ошибка day30 с движением", AtomicFailure),
    ("Успех day30 с движением и отчётом", AtomicSuccess),
    ("Economy: traits, jobs, infrastructure отдельно", Economy),
    ("Проекция: скрытые links/body/route/target/transit", Hidden),
    ("JSON явных union всех ветвей и отсутствие приватных полей", Unions),
    ("Readonly коллекции и повторные снимки", Immutable),
    ("Отрицательные join и управление наблюдателем", JoinErrors),
    ("Лимит command cache и rejected replay", CacheLimit)
};
var failures = 0;
if (args.Contains("--condition-refresh-only"))
{
    if (sourceConstruction is null) throw new ArgumentException("--condition-refresh-only требует --game-root.");
    try { InfrastructureConditionRefresh(); Console.WriteLine("PASS: Q04 Session — explicit permission fixture, same-transaction cancellation/refund and Unknown rollback"); return 0; }
    catch (Exception exception) { Console.Error.WriteLine("FAIL: Infrastructure condition refresh\\n" + exception); return 1; }
}
if (args.Contains("--enabled-only"))
{
    if (sourceStartup is null) throw new ArgumentException("--enabled-only требует --game-root.");
    try { OrdinaryBuildingEnabled(); Console.WriteLine("PASS: B02 Session — source permissions, explicit no/Unknown, no mutation, authority/privacy/stale"); return 0; }
    catch (Exception exception) { Console.Error.WriteLine("FAIL: Enabled здания\n" + exception); return 1; }
}
if (args.Contains("--zone-only"))
{
    if (sourceStartup is null) throw new ArgumentException("--zone-only требует --game-root.");
    try { OrdinaryRuralZones(); Console.WriteLine("PASS: C02 Session — source slots/requirements, production Unknown, no mutation, authority/privacy"); return 0; }
    catch (Exception exception) { Console.Error.WriteLine("FAIL: Ресурсные зоны\n" + exception); return 1; }
}
if (args.Contains("--zone-replacement-only"))
{
    if (sourceStartup is null) throw new ArgumentException("--zone-replacement-only требует --game-root.");
    try { OrdinaryZoneReplacement(); Console.WriteLine("PASS: C03 Session — source preview/privacy/strict command/confirmation/stale, native speed Unknown без мутации"); return 0; }
    catch (Exception exception) { Console.Error.WriteLine("FAIL: Замена городской зоны\n" + exception); return 1; }
}
if (args.Contains("--rural-only"))
{
    if (sourceStartup is null) throw new ArgumentException("--rural-only требует --game-root.");
    try { OrdinaryRuralDistricts(); Console.WriteLine("PASS: Три ordinary presets/rural types — source quote, reservations, jobs/upkeep completion, месяц"); return 0; }
    catch (Exception exception) { Console.Error.WriteLine("FAIL: Ресурсные районы\n" + exception); return 1; }
}
if (args.Contains("--queue-only"))
{
    if (sourceStartup is null) throw new ArgumentException("--queue-only требует --game-root.");
    try { OrdinaryQueueReorder(); Console.WriteLine("PASS: Обычная очередь — authority, membership, active lock, stale/replay, банк/работа/дневная фаза"); return 0; }
    catch (Exception exception) { Console.Error.WriteLine("FAIL: Обычная очередь\n" + exception); return 1; }
}
foreach (var (name, run) in checks.Where(_ => !researchOnly))
{
    try
    {
        run();
        Console.WriteLine("PASS: " + name);
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine("FAIL: " + name + "\n" + exception);
    }
}
Console.WriteLine($"Наборов: {checks.Length}; ошибок: {failures}.");
if (sourceStartup is not null)
{
    if (!args.Contains("--research-diagnostics") && !args.Contains("--upgrade-only") && !args.Contains("--long-automatic-only"))
    {
        if (!args.Contains("--effects-only"))
        {
            try { ResearchIntegration(); Console.WriteLine("PASS: Исследования: транзакция месяца, три направления, приватность, retry/stale и улучшение здания"); }
            catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Research integration\n" + exception); }
        }
        try { ResearchEffectsIntegration(); Console.WriteLine("PASS: Source effects: граница месяца, чистый overlay, preview, live construction speed и atomic rollback"); }
        catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Research effects integration\n" + exception); }
    }
    if (!args.Contains("--upgrade-only") && !args.Contains("--long-automatic-only"))
    {
        try { FullResearchSmoke(); Console.WriteLine("PASS: Полный исходный пул технологий трёх ordinary presets"); }
        catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Full research smoke\n" + exception); }
    }
    if (researchOnly) { return failures == 0 ? 0 : 1; }
    if (!args.Contains("--upgrade-only") && !args.Contains("--long-automatic-only"))
    {
    try { StartupProjection(sourceStartup); Console.WriteLine("PASS: Реальный source startup дошёл до owner и observer-player projection без утечки в general view"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Реальный source startup M3 projection\n" + exception); }
    try { SourceEconomyRuntime(); Console.WriteLine("PASS: Реальный source economy start/month/WorkforceLimit/private DTO"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Реальный source economy M3 runtime\n" + exception); }
    try { LiveConstructionIntegration(); Console.WriteLine("PASS: Real startup source graph and construction share session revisions, bank, and projection"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Live construction M3 integration\n" + exception); }
    try { OrdinaryQueueReorder(); Console.WriteLine("PASS: Обычная очередь: active lock, exact membership, работа/банк, stale/replay/authority"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Перестановка обычной очереди\n" + exception); }
    try { ExtendedOrdinaryRuntime(); Console.WriteLine("PASS: Расширенное здание оплачено настоящим банком, построено и изменило production/upkeep"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Расширенное runtime здание\n" + exception); }
    }
    if (!args.Contains("--long-automatic-only"))
    {
        try { UpgradeRuntime(); Console.WriteLine("PASS: Startup capital upgrade source quote/payment/pending/cancel/recheck/atomic completion and subsequent graph refresh"); }
        catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Startup upgrade runtime\n" + exception); }
    }
    if (!args.Contains("--upgrade-only"))
    {
        try { LongAutomaticConstructionRuntime(); Console.WriteLine("PASS: 6000 automatic source days without construction receipts, then paid build and startup upgrade in the same session"); }
        catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL: Long automatic construction runtime\n" + exception); }
    }
}
return failures == 0 ? 0 : 1;

M3GameSession NewSession(decimal jobUpkeep = 0, bool useSourceEconomy = false, TechnologyCatalog? research = null, Func<int, IReadOnlySet<string>>? researchedTechnologies = null, M3ConstructionContent? constructionOverride = null)
{
    string[] jobsIds = ["technician", "miner", "farmer"];
    string[] traitsIds = ["trait_agrarian", "trait_industrious", "trait_ingenious"];
    string[] buildingIds = ["building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility"];
    var contentManifest = new ContentManifest("4.5.2", "synthetic", [], []);
    var resources = new[] { new ResourceDefinition("energy", 1000000), new ResourceDefinition("minerals", 1000000), new ResourceDefinition("food", 1000000) };
    var jobs = new[]
    {
        new JobDefinition("technician", "worker", 100, Amounts(("energy", 6)), Amounts(("energy", jobUpkeep))),
        new JobDefinition("miner", "worker", 100, Amounts(("minerals", 4)), Amounts()),
        new JobDefinition("farmer", "worker", 100, Amounts(("food", 6)), Amounts())
    };
    var buildings = buildingIds.Select((id, index) => new BuildingDefinition(id, Amounts(("energy", 2)), new[] { new Modifier("worker", resources[index].Id, "produces", 0.5m, 0) })).ToArray();
    var projection = new EconomyProjection(contentManifest, resources, jobs, [new CategoryDefinition("worker", null)], buildings, [], 100);
    var traitJobs = new[] { "farmer", "miner", "technician" };
    var traits = traitsIds.Select((id, index) => new TraitWorkforceProjection(contentManifest, [id], jobsIds.ToDictionary(job => job, job => job == traitJobs[index] ? 0.15m : 0), [])).ToDictionary(item => item.Traits.Single());
    return new(projection, traits, manifest, constructionImportAttempted: true, startup: sourceStartup,
        sourceEconomy: useSourceEconomy ? sourceEconomy : null, constructionContent: useSourceEconomy ? constructionOverride ?? sourceConstruction : null, researchCatalog: research, researchedTechnologies: researchedTechnologies,
        rareDepositResearchPolicy: useSourceEconomy ? sourceRareDepositPolicy : null);
}

void RareDepositResearchFacts()
{
    Check(sourceRareDepositPolicy is not null && sourceStartup is not null && sourceResearch is not null, "Нужен --game-root.");
    var policy = sourceRareDepositPolicy!;
    Check(policy.Manifest.Profile == M3RareDepositResearchPolicy.PolicyId && policy.Manifest.Limitations.Count > 0, "Prototype policy не раскрыта.");
    var selected = sourceStartup!.Select("economic", 451);
    var colony = M3StartupColony.Create("rare-policy", 1, selected.Homeworld, selected.Plan) with { Features = [] };
    M3StartupFeature Feature(string id, bool hidden = false) => new("policy/" + id, id, false, hidden, hidden ? "policy/blocker" : null, null, 0);
    foreach (var (helper, deposit) in new[] { ("has_volatile_motes_deposit", "d_dust_caverns"), ("has_exotic_gases_deposit", "d_bubbling_swamp"), ("has_rare_crystals_deposit", "d_crystal_forest") })
    {
        Check(policy.EvaluateHelper(helper, colony) == false, "Подтверждённое отсутствие source deposit не определено: " + helper);
        Check(policy.EvaluateHelper(helper, colony with { Features = [Feature(deposit)] }) == true, "Source positive helper потерян: " + helper);
        Check(policy.EvaluateHelper(helper, colony with { Features = [Feature(deposit, true)] }) is null, "Hidden deposit подменён False: " + helper);
    }
    var habitatContext = new StartupOwnerContext(StartupAuthority.Regular, selected.Plan.Context.Ethics, selected.Plan.Context.Civics,
        selected.Plan.Context.Origin, selected.Plan.Context.PlanetSize, planetClass: "pc_habitat");
    var planConstructor = typeof(StartupBuildingPlan).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
    var habitatPlan = (StartupBuildingPlan)planConstructor.Invoke([habitatContext, selected.Plan.FinalPlanetSize, selected.Plan.Districts, selected.Plan.Slots,
        selected.Plan.Zones, selected.Plan.Buildings, selected.Plan.Sources, selected.Plan.Gates, selected.Plan.Features, selected.Plan.DistrictCaps, selected.Plan.ZoneRecurringEconomyProofs]);
    var habitat = colony with { Homeworld = selected.Homeworld with { PlanetClass = "pc_habitat" }, Plan = habitatPlan };
    Check(policy.EvaluateHelper("has_exotic_gases_deposit", habitat) is null, "Неподдержанный habitat orbital helper стал False.");
    Check(policy.EvaluateHelper("has_exotic_gases_deposit", colony with { Homeworld = selected.Homeworld with { PlanetClass = "pc_unknown" } }) is null, "Unknown/mismatched class стал standard.");
    var query = M3RareDepositResearchPolicy.FindWithinBorder(sourceResearch!.Technologies["tech_exotic_gases"].WeightRules).Single();
    Check(policy.EvaluateWeightQuery(query, [colony]) == false && policy.EvaluateWeightQuery(query, [colony with { Features = [Feature("d_exotic_gases_1")] }]) == true
        && policy.EvaluateWeightQuery(query, [colony with { Features = [Feature("d_exotic_gases_1", true)] }]) is null, "Weight query потерял three-valued deposit facts.");
    Check(policy.EvaluateWeightQuery(ParadoxParser.Parse("is_planet_class = pc_continental"), [colony]) is null, "General within_border shape стала разрешена.");
    ResearchFacts Facts(M3GameSession session, M3StartupColony actual) => (ResearchFacts)typeof(M3GameSession)
        .GetMethod("ResearchFactsFor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session,
            [ConstructionOrdinaryTemplate.Get("economic"), 1, 0, new Dictionary<string, M3StartupColony> { [actual.ColonyId] = actual }, null])!;
    var real = NewSession(useSourceEconomy: true, research: sourceResearch);
    var facts = Facts(real, colony);
    var template = ConstructionOrdinaryTemplate.Get("economic");
    var country = ResearchCountry.Create(sourceResearch, 1, template.StartingTechnologies, facts);
    var gas = country.Assess("tech_exotic_gases");
    Check(gas.Weight > 0 && !gas.Gates.Contains("weight-unknown") && country.MissingFacts("tech_exotic_gases").Count == 0, "Source gas weight остался unknown.");
    Check(country.Assess("tech_basic_science_lab_2").Weight == 0, "Lab2 weight открыт до изучения gas.");
    var positive = ResearchCountry.Create(sourceResearch, 1, template.StartingTechnologies, Facts(real, colony with { Features = [Feature("d_bubbling_swamp")] }));
    Check(positive.Assess("tech_exotic_gases").Weight == gas.Weight * 0.5m, "Source .5 deposit weight не применён.");
    Check(!facts.Collections.ContainsKey(ResearchFacts.NumericKey("country", "any_planet_within_border")), "Введена general within_border collection.");
    var collisionPotential = new ScriptBlock([new ScriptEntry("any_planet_within_border", "=", null, query, 1, 1, "explicit-unit-fixture")]);
    foreach (var scope in new[] { "Potential", "StartingPotential", "CostRules", "SwapTriggers" })
    {
        var collision = sourceResearch.Technologies["tech_exotic_gases"] with { Id = "rc_nonweight_collision", WeightRules = null };
        collision = scope switch
        {
            "Potential" => collision with { Potential = collisionPotential },
            "StartingPotential" => collision with { StartingPotential = collisionPotential },
            "CostRules" => collision with { CostRules = collisionPotential },
            _ => collision with { SwapTriggers = Array.AsReadOnly(new[] { collisionPotential }) }
        };
        var collisionCatalog = new TechnologyCatalog(sourceResearch.Technologies.Values.Append(collision), sourceResearch.Tiers.Values,
            sourceResearch.Alternatives, sourceResearch.PreviousOptionMultiplier, sourceResearch.NoLeaderPenalty, sourceResearch.Manifest,
            sourceResearch.CategoryParents, sourceResearch.WorkforceComposites, sourceResearch.EmpireModifiers);
        var collisionFacts = Facts(NewSession(useSourceEconomy: true, research: collisionCatalog), colony);
        Check(!collisionFacts.Values.ContainsKey(ResearchFacts.Key("country", "any_planet_within_border", M3RareDepositResearchPolicy.Serialize(query))), "Weight-only факт утёк в " + scope);
    }
}

void RareResourceSourceBindings()
{
    Check(sourceEconomy is not null && sourceStartup is not null && sourceConstruction is not null, "Нужен --game-root.");
    foreach (var (producer, resource) in new[] { ("building_refinery", "exotic_gases"), ("building_chemical_plant", "volatile_motes"), ("building_crystal_plant", "rare_crystals") })
    {
        Check(!sourceConstruction!.RuntimeGates.ContainsKey(producer), "Source producer gated: " + producer);
        var selected = sourceStartup!.Select("economic", 451);
        var colony = M3StartupColony.Create("rare-binding", 1, selected.Homeworld, selected.Plan);
        var world = sourceEconomy!.CreateWorld();
        var country = world.CreateCountry(); var planet = world.CreatePlanet(country);
        sourceEconomy.InitializeColony(world, country, planet, "economic", colony.Plan);
        var runtime = sourceConstruction.Create(colony, sourceEconomy.CreateConstructionContributions(colony));
        var baseline = world.PreviewPlanetReport(planet);
        var slot = runtime.Slots.Values.First(slot => slot.Kind == ConstructionKind.Building && !runtime.Installed.ContainsKey(slot.Id));
        // Fixture проверяет только consumer source binding; paid acquisition проверяется отдельным natural runtime.
        var withProducer = new ConstructionLiveRuntime(runtime.ColonyId, runtime.OwnerId, runtime.Slots.Values,
            runtime.Installed.Values.Append(new(slot.Id, producer, ConstructionKind.Building, Amounts(), Amounts())), startupBuildingBySlot: runtime.StartupBuildingBySlot);
        sourceEconomy.RefreshInstalledGraph(world, planet, colony, withProducer, "economic");
        var report = world.PreviewPlanetReport(planet);
        var effects = world.GetInstalledJobEconomyEffects(planet)!;
        var industrialJobs = sourceEconomy.RuntimeJobs.Where(job => job.SourceEconomicCategory is "planet_metallurgists" or "planet_artisans").ToArray();
        var employed = industrialJobs.Sum(job => report.Allocated[job.Id].EffectiveWorkforce / job.WorkforcePerJob);
        Check(employed > 0 && report.Produced[resource] == employed * 0.1m * (1 + effects.OutputMultiplier), "Industrial exact-category Add/effective Workforce неверны: " + producer);
        Check(report.JobUpkeep["minerals"] - baseline.JobUpkeep["minerals"] == employed * 0.5m
            && report.BuildingUpkeep["energy"] - baseline.BuildingUpkeep["energy"] == 3m, "Job и structural upkeep не разделены: " + producer);
        Check(report.Allocated.All(pair => pair.Value == baseline.Allocated[pair.Key]), "Producer добавил jobs/изменил allocation: " + producer);
        // Отдельная source-effect fixture: не является доказательством acquisition технологий.
        var template = ConstructionOrdinaryTemplate.Get("economic");
        var researched = ResearchCountry.Create(sourceResearch!, 1,
            template.StartingTechnologies.Concat(new[] { "tech_consumer_good_refinement_1", "tech_advanced_metallurgy_1" }), new ResearchFacts(new Dictionary<string, bool?>()));
        var researchEffects = ResearchEffects.Project(researched, sourceEconomy.Catalog, sourceEconomy.RuntimeJobs,
            template.Ethics.Concat(template.Civics).Append(template.OriginId).ToHashSet(StringComparer.Ordinal), world.GetEffectiveJobResourceClosure(planet));
        sourceEconomy.ApplyResearchEffects(world, planet, "economic", researchEffects);
        var boosted = world.PreviewPlanetReport(planet).Produced[resource];
        Check(boosted == report.Produced[resource] + employed * 0.1m * 0.05m, "Source category research Mult потерял installed rare Add: " + producer);
        sourceEconomy.ApplyResearchEffects(world, planet, "economic", researchEffects);
        Check(world.PreviewPlanetReport(planet).Produced[resource] == boosted, "Повтор research overlay удвоил rare effect.");
        sourceEconomy.RefreshInstalledGraph(world, planet, colony, withProducer, "economic");
        sourceEconomy.ApplyResearchEffects(world, planet, "economic", researchEffects);
        Check(world.PreviewPlanetReport(planet).Produced[resource] == boosted, "Installed refresh потерял/удвоил source research rare effect.");
        world.AdvanceMonth();
        Check(world.GetBalance(country, resource) > 0 && world.GetBalance(country, "nanites") == 0, "Zero nanites flow заблокировал реальный credit других ресурсов.");
        var fingerprint = world.StateHash();
        try { world.SetBalance(country, "nanites", 1); throw new Exception("Setter обошёл unknown cap."); }
        catch (ResourceStorageUnresolvedException exception) { Check(exception.ResourceId == "nanites" && world.StateHash() == fingerprint, "Typed unknown-cap setter потерял атомарность."); }
        sourceEconomy.RefreshInstalledGraph(world, planet, colony, runtime, "economic");
        Check(world.PreviewPlanetReport(planet).Produced[resource] == 0 && world.GetInstalledJobEconomyEffects(planet)!.Modifiers.Count == 0, "Installed rebuild сохранил удалённый producer effect.");
    }
}

void RareResourceNaturalRuntime()
{
    Check(sourceResearch is not null && sourceEconomy is not null && sourceConstruction is not null, "Нужен --game-root с настоящими 4.5.2 sources.");
    // Полный исходный пул, исходные цены/tiers/weights, штатные commands; stocks и technologies не подменяются.
    var session = NewSession(useSourceEconomy: true, research: sourceResearch);
    var host = session.Join("Редкие ресурсы", "player", null, TimeSpan.Zero);
    var second = session.Join("Сосед", "player", null, TimeSpan.Zero);
    Import(session, host, "rare-import-1"); Import(session, second, "rare-import-2");
    Accept(session, host, new("rare-template-1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("rare-template-2", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, host, new("rare-ready-1", "ready", Ready: true)); Accept(session, second, new("rare-ready-2", "ready", Ready: true));
    Accept(session, host, new("rare-start", "start"));
    var serial = 0;
    string Id() => "rare-" + ++serial;
    M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
    decimal Stock(string resource) => View().Economy!.Resources.Single(item => item.ResourceId == resource).Stock;
    var initial = View();
    foreach (var id in new[] { "volatile_motes", "exotic_gases", "rare_crystals", "sr_zro", "sr_dark_matter", "sr_living_metal", "nanites" })
    {
        var resource = initial.Economy!.Resources.Single(item => item.ResourceId == id);
        Check(resource.Stock == 0 && (id == "nanites" ? resource.StoragePolicy == "unspecified" && resource.SourceMaximum is null : resource.SourceMaximum == 15000), "Неверный source stock/cap: " + id);
    }
    foreach (var player in new[] { host, second })
    {
        foreach (var job in session.View(player.ParticipantId).Development!.Colonies.Single().Economy!.Jobs.Where(job => job.Capacity > 0))
        {
            var colony = session.View(player.ParticipantId).Development!.Colonies.Single();
            var limit = job.JobId switch
            {
                "foundry" or "biologist" => 0m,
                "physicist" or "engineer" => player.ParticipantId == host.ParticipantId ? job.Capacity : 0m,
                "artisan" => Math.Min(job.Capacity, player.ParticipantId == host.ParticipantId ? 250m : 100m),
                "miner" or "technician" or "farmer" => job.Capacity,
                _ => 0m
            };
            Accept(session, player, new(Id(), "setWorkforceLimit", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision, Job: job.JobId, Count: limit));
        }
    }
    var day = 0;
    Accept(session, host, new(Id(), "pause", Paused: false));
    void Days(int count)
    {
        for (var index = 0; index < count; index++)
        { session.Tick(TimeSpan.FromSeconds(++day)); }
        Check(session.View(host.ParticipantId).LastError is null, "Ошибка настоящей экономики: " + session.View(host.ParticipantId).LastError);
        Check(session.ObserveTiming().ElapsedDay == day, "Штатные часы остановились до запрошенного дня.");
        foreach (var player in new[] { host, second })
        {
            var basicFlows = session.View(player.ParticipantId).Development!.Colonies.Single().Economy!.Resources
                .Where(item => item.ResourceId is "energy" or "minerals" or "food" or "consumer_goods").ToArray();
            Check(basicFlows.All(item => item.Net >= 0), "Natural strategy имеет отрицательный basic flow: " + string.Join(',', basicFlows.Select(item => item.ResourceId + "=" + item.Net)));
        }
    }
    bool Known(string id) => session.View(host.ParticipantId).Research!.Empires.Single().Researched.Any(item => item.Id == id);
    void ResearchDiagnostics()
    {
        var participants = (System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        var owner = participants.Cast<object>().First();
        var country = (ResearchCountry)owner.GetType().GetField("Research")!.GetValue(owner)!;
        foreach (var id in new[] { "tech_exotic_gases", "tech_basic_science_lab_2" })
        {
            var assessment = country.Assess(id);
            Console.WriteLine($"Natural diagnostic day {day}: {id}; available={assessment.Available}; weight={assessment.Weight}; gates={string.Join(',', assessment.Gates)}; missing={string.Join(',', country.MissingFacts(id))}");
        }
        Check(country.Assess("tech_exotic_gases").Weight is not null || args.Contains("--rare-diagnostics-only"), "tech_exotic_gases weight-unknown: " + string.Join(',', country.MissingFacts("tech_exotic_gases")));
    }
    void DevelopResearch()
    {
        // Ускоряем путь настоящей оплаченной застройкой, сохраняя исходные tech costs и обычные часы.
        var colony = View();
        var labRecipe = colony.Catalog.Single(item => item.DefinitionId == "building_research_lab_1");
        if (colony.Installed.Count(item => item.DefinitionId == labRecipe.DefinitionId) < 3
            && colony.Queue.All(item => item.Status != "pending") && labRecipe.Quote is not null && labRecipe.CanAfford == true)
        {
            Accept(session, host, new(Id(), "enqueueConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision,
                DefinitionId: labRecipe.DefinitionId, SlotId: labRecipe.SlotIds[0], OrderId: Id()), day);
            Console.WriteLine($"Natural paid lab day {day}: costs={Json(labRecipe.Quote.Costs)}");
        }
        foreach (var job in View().Economy!.Jobs.Where(item => item.JobId is "physicist" or "engineer" && item.Limit != item.Capacity))
        {
            colony = View();
            Accept(session, host, new(Id(), "setWorkforceLimitMax", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision, Job: job.JobId), day);
        }
    }
    void SelectResearch()
    {
        DevelopResearch();
        foreach (var areaId in new[] { "engineering", "physics" })
        {
            var research = session.View(host.ParticipantId).Research!.Empires.Single();
            var area = research.Areas.Single(item => item.Id == areaId);
            var target = areaId == "engineering" ? "tech_exotic_gases" : "tech_basic_science_lab_2";
            if (Known(target) || area.ActiveTechnologyId is not null || area.Choices.Count == 0) continue;
            var selected = area.Choices.OrderByDescending(item => item.Id == target)
                .ThenBy(item => sourceResearch!.Technologies[item.Id].Cost ?? decimal.MaxValue).ThenBy(item => item.Id, StringComparer.Ordinal).First();
            Accept(session, host, new(Id(), "selectResearch", Area: areaId, TechnologyId: selected.Id, ExpectedResearchRevision: research.Revision), day);
            Console.WriteLine($"Natural research day {day}: {selected.Id}, cost={sourceResearch!.Technologies[selected.Id].Cost}");
        }
    }
    SelectResearch();
    ResearchDiagnostics();
    if (args.Contains("--rare-diagnostics-only")) return;
    for (var month = 0; month < 6000 && !Known("tech_exotic_gases"); month++) { Days(30); SelectResearch(); }
    Check(Known("tech_exotic_gases"), "Исходный пул не дал исследовать tech_exotic_gases за 6000 месяцев.");
    var beforeProducer = View();
    var producer = beforeProducer.Catalog.Single(item => item.DefinitionId == "building_refinery");
    Check(producer.Quote is not null && producer.SlotIds.Count > 1 && producer.CanAfford == true, "Настоящий producer не доступен/не оплачен: " + string.Join(',', producer.Gates.Select(gate => gate.DependencyId)));
    var producerSlot = producer.SlotIds[0];
    var mineralBefore = Stock("minerals");
    Console.WriteLine($"Natural producer day {day}: tech_exotic_gases researched, minerals={mineralBefore}, costs={Json(producer.Quote!.Costs)}, gases={Stock("exotic_gases")}");
    Accept(session, host, new(Id(), "enqueueConstruction", ColonyId: beforeProducer.ColonyId, ExpectedColonyRevision: beforeProducer.Revision, DefinitionId: producer.DefinitionId, SlotId: producerSlot, OrderId: "rare-producer-cancel"), day);
    var pending = View();
    Check(Stock("minerals") == mineralBefore - producer.Quote!.Costs["minerals"] && Stock("exotic_gases") == 0, "Producer не списал настоящую стоимость или дал stock до completion.");
    Check(pending.Catalog.Single(item => item.DefinitionId == producer.DefinitionId).Gates.Any(gate => gate.DependencyId == "planet-limit-reached"), "Pending producer не резервирует source planet_limit.");
    var duplicate = session.Execute(host.ParticipantId, host.ConnectionId, new(Id(), "enqueueConstruction", ColonyId: pending.ColonyId, ExpectedColonyRevision: pending.Revision, DefinitionId: producer.DefinitionId, SlotId: producer.SlotIds[1], OrderId: "rare-duplicate"), TimeSpan.FromSeconds(day));
    Check(!duplicate.Accepted && duplicate.Code == "planet-limit-reached", "Duplicate producer обошёл pending limit.");
    Accept(session, host, new(Id(), "cancelConstruction", ColonyId: pending.ColonyId, ExpectedColonyRevision: pending.Revision, OrderId: "rare-producer-cancel"), day);
    Check(Stock("minerals") == mineralBefore && View().Catalog.Single(item => item.DefinitionId == producer.DefinitionId).Quote is not null, "Cancel не вернул payment/не освободил planet_limit.");
    var retry = View();
    Accept(session, host, new(Id(), "enqueueConstruction", ColonyId: retry.ColonyId, ExpectedColonyRevision: retry.Revision, DefinitionId: producer.DefinitionId, SlotId: producerSlot, OrderId: "rare-producer"), day);
    Days((int)Math.Ceiling(producer.Quote.DurationDays));
    Check(View().Installed.Any(item => item.DefinitionId == producer.DefinitionId), "Producer не завершён.");
    Days(30);
    var flow = View().Economy!.Resources.Single(item => item.ResourceId == "exotic_gases");
    Check(flow.Stock > 0 && flow.Produced > 0, "Source industrial Add не создал выпуск из нулевого stock.");
    var lab = View().Installed.First(item => item.DefinitionId == "building_research_lab_1");
    for (var month = 0; month < 6000; month++)
    {
        SelectResearch();
        var upgradeOption = View().Installed.Single(item => item.SlotId == lab.SlotId).Upgrades!.Single(item => item.DefinitionId == "building_research_lab_2");
        if (Known("tech_basic_science_lab_2") && upgradeOption.CanEnqueue) break;
        Days(30);
    }
    // Сохраняем текущий научный бюджет при росте capacity: не включаем новые вакансии автоматически.
    foreach (var job in View().Economy!.Jobs.Where(item => item.JobId is "physicist" or "engineer" && item.Limit > 0))
    {
        var colony = View();
        Accept(session, host, new(Id(), "setWorkforceLimit", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision,
            Job: job.JobId, Count: job.Limit * 0.95m), day);
    }
    var ready = View();
    var upgrade = ready.Installed.Single(item => item.SlotId == lab.SlotId).Upgrades!.Single(item => item.DefinitionId == "building_research_lab_2");
    Check(Known("tech_basic_science_lab_2") && upgrade.Quote is not null && upgrade.CanEnqueue, "Настоящие tech/stock не открыли paid lab upgrade.");
    var gasBefore = Stock("exotic_gases");
    Console.WriteLine($"Natural upgrade day {day}: tech_basic_science_lab_2 researched, costs={Json(upgrade.Quote!.Costs)}, gases={gasBefore}");
    Accept(session, host, new(Id(), "enqueueUpgrade", ColonyId: ready.ColonyId, ExpectedColonyRevision: ready.Revision, DefinitionId: upgrade.DefinitionId, SourceDefinitionId: lab.DefinitionId, SlotId: lab.SlotId, ZoneInstanceId: upgrade.ZoneInstanceId, OrderId: "rare-lab-upgrade"), day);
    Check(Stock("exotic_gases") == gasBefore - upgrade.Quote!.Costs["exotic_gases"] && View().Installed.Single(item => item.SlotId == lab.SlotId).DefinitionId == lab.DefinitionId, "Upgrade не оплатил gases/преждевременно заменил старое здание.");
    Days((int)Math.Ceiling(upgrade.Quote.DurationDays));
    Check(View().Installed.Single(item => item.SlotId == lab.SlotId).DefinitionId == upgrade.DefinitionId, "Atomic replacement не выполнен.");
    Days(30);
    var recurring = View().Economy!.Resources.Single(item => item.ResourceId == "exotic_gases");
    Check(recurring.BuildingUpkeep > 0 && recurring.Produced > 0 && Stock("nanites") == 0, "Rare recurring upkeep/production/zero nanites не дошли до общего месяца.");
    Console.WriteLine($"Natural rare path completed day {day}: gases stock={recurring.Stock}, produced={recurring.Produced}, upkeep={recurring.BuildingUpkeep}");
}

void ResearchIntegration()
{
    Check(sourceResearch is not null && sourceResearch.Technologies.Count > 256, "Реальный каталог технологий не импортирован.");
    _ = new ConstructionContext(technologies: sourceResearch!.Technologies.Keys);
    var starting = ConstructionOrdinaryTemplate.Get("economic").StartingTechnologies;
    var chosen = new[] { "tech_basic_science_lab_2", "tech_genome_mapping", "tech_space_mining_1" };
    var definitions = starting.Concat(chosen).Distinct(StringComparer.Ordinal).Select(id => sourceResearch.Technologies[id] with
    {
        Cost = chosen.Contains(id) ? 1m : sourceResearch.Technologies[id].Cost, Tier = 0, Potential = null, WeightRules = null,
        Prerequisites = [], Gates = [], SwapTriggers = []
    }).ToArray();
    var catalog = new TechnologyCatalog(definitions, [new(0, 0, null)], 3, 0.5m, sourceResearch.NoLeaderPenalty,
        categoryParents: sourceResearch.CategoryParents, workforceComposites: sourceResearch.WorkforceComposites, empireModifiers: sourceResearch.EmpireModifiers);
    var session = NewSession(useSourceEconomy: true, research: catalog);
    var host = session.Join("Наука", "player", null, TimeSpan.Zero);
    var second = session.Join("Сосед", "player", null, TimeSpan.Zero);
    var observer = session.Join("Наблюдатель", "observer", null, TimeSpan.Zero);
    Import(session, host, "ri1"); Import(session, second, "ri2"); Import(session, observer, "ri3");
    Reject(session, host, new("premature", "selectResearch", Area: "physics", TechnologyId: chosen[0], ExpectedResearchRevision: 0), "INVALID_PHASE");
    Accept(session, host, new("rt1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("rt2", "selectEmpireTemplate", TemplateId: "scientific"));
    Accept(session, host, new("ra", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Accept(session, host, new("rr1", "ready", Ready: true)); Accept(session, second, new("rr2", "ready", Ready: true));
    Accept(session, host, new("rs", "start"));
    var initial = session.View(host.ParticipantId);
    Check(initial.Research!.Empires.Single().ParticipantId == host.ParticipantId, "Утечка науки соседа.");
    Check(session.View(observer.ParticipantId).Research!.Empires.Count == 2, "Общий наблюдатель не получил read-only науку.");
    var initialColony = initial.Development!.Colonies.Single();
    var lab = initialColony.Installed.First(item => item.DefinitionId == "building_research_lab_1");
    Check(!initialColony.Catalog.Single(item => item.DefinitionId == "building_research_lab_2").SlotIds.Contains(lab.SlotId), "Закрытый upgrade доступен до технологии.");
    var first = initial.Research.Empires.Single();
    Reject(session, observer, new("observer-science", "selectResearch", Area: "physics", TechnologyId: chosen[0], ExpectedResearchRevision: first.Revision), "OUT_OF_SCOPE");
    var current = first;
    foreach (var area in current.Areas)
    {
        var command = new M3ClientCommand("select-" + area.Id, "selectResearch", Area: area.Id, TechnologyId: area.Choices.Single().Id, ExpectedResearchRevision: current.Revision);
        Accept(session, host, command);
        var retry = session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero);
        Check(retry.Accepted, "Повтор выбора не вернул ACK.");
        Reject(session, host, command with { CommandId = "stale-" + area.Id }, "STALE_REVISION");
        current = session.View(host.ParticipantId).Research!.Empires.Single();
    }
    for (var day = 0; day < 30; day++) { Accept(session, host, new("research-day-" + day, "stepDay")); }
    var completed = session.View(host.ParticipantId);
    Check(chosen.All(id => completed.Research!.Empires.Single().Researched.Any(item => item.Id == id)), "Не завершены три направления.");
    Check(completed.Research!.Empires.Single().Areas.All(area => area.ActiveTechnologyId is null && area.LastCompletedTechnologyId is not null && area.Reserve == 0), "Завершение/потеря избытка/уведомление неверны.");
    var neighbor = session.View(second.ParticipantId).Research!.Empires.Single();
    Check(neighbor.Areas.All(area => area.Reserve > 0 && area.ActiveTechnologyId is null), "Выпуск без выбора не накоплен.");
    foreach (var area in neighbor.Areas)
    {
        Accept(session, second, new("neighbor-select-" + area.Id, "selectResearch", Area: area.Id, TechnologyId: area.Choices.Single().Id, ExpectedResearchRevision: neighbor.Revision));
        neighbor = session.View(second.ParticipantId).Research!.Empires.Single();
    }
    var expectedNeighborReserves = neighbor.Areas.ToDictionary(area => area.Id, area => Math.Max(0, area.Reserve - area.MonthlyResearch), StringComparer.Ordinal);
    session.Disconnect(second.ParticipantId, second.ConnectionId, TimeSpan.Zero);
    for (var day = 0; day < 30; day++) { Accept(session, host, new("reserve-day-" + day, "stepDay")); }
    neighbor = session.View(second.ParticipantId).Research!.Empires.Single();
    Check(neighbor.Areas.All(area => area.Reserve == expectedNeighborReserves[area.Id] && area.LastCompletedTechnologyId is not null), "Старый резерв не израсходован ровно один раз или AFK остановил исследование.");
    // Тот же synthetic funding fixture должен предшествовать серверной оценке affordability.
    var world = (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    var participantList = (System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    var owner = participantList.Cast<object>().First();
    var countryId = (EntityId)owner.GetType().GetField("Country")!.GetValue(owner)!;
    foreach (var participant in participantList.Cast<object>())
    {
        var country = (EntityId?)participant.GetType().GetField("Country")!.GetValue(participant);
        if (country is null) { continue; }
        foreach (var resource in sourceEconomy!.Resources.Values.Where(resource => resource.SourceMaximum is not null))
        { world.SetBalance(country.Value, resource.Id, resource.SourceMaximum!.Value); }
    }
    completed = session.View(host.ParticipantId);
    var colony = completed.Development!.Colonies.Single();
    var upgrade = colony.Installed.Single(item => item.SlotId == lab.SlotId).Upgrades!.Single(item => item.DefinitionId == "building_research_lab_2");
    Check(upgrade.SlotId == lab.SlotId && upgrade.Quote is not null && upgrade.Eligibility == "available", "Изучение не открыло exact source upgrade.");
    var consumer = RunConstructionConsumer(colony, completed.Development.Gates);
    Check(consumer.EnumerateArray().Any(operation => operation.GetProperty("type").GetString() == "enqueueUpgrade"
        && operation.GetProperty("definitionId").GetString() == upgrade.DefinitionId && operation.GetProperty("slotId").GetString() == lab.SlotId),
        "Реальный consumer не создал upgrade команду по серверному occupied slot.");
    var commandUpgrade = new M3ClientCommand("upgrade-lab", "enqueueUpgrade", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision,
        DefinitionId: upgrade.DefinitionId, SourceDefinitionId: lab.DefinitionId, ZoneInstanceId: upgrade.ZoneInstanceId, SlotId: lab.SlotId, OrderId: "lab-upgrade");
    Accept(session, host, commandUpgrade);
    Accept(session, host, new("upgrade-first-day", "stepDay"));
    var pending = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(pending.Installed.Single(item => item.SlotId == lab.SlotId).DefinitionId == lab.DefinitionId, "Старое здание удалено при заказе.");
    Check(pending.Economy!.Jobs.Single(item => item.JobId == "physicist").Capacity == colony.Economy!.Jobs.Single(item => item.JobId == "physicist").Capacity,
        "Незавершённый upgrade преждевременно изменил jobs.");
    Accept(session, host, new("cancel-upgrade", "cancelConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: pending.Revision, OrderId: "lab-upgrade"));
    var cancelled = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(cancelled.Installed.Single(item => item.SlotId == lab.SlotId).DefinitionId == lab.DefinitionId, "Отмена не сохранила старое здание.");
    Accept(session, host, commandUpgrade with { CommandId = "upgrade-again", OrderId = "lab-upgrade-complete", ExpectedColonyRevision = cancelled.Revision });
    var beforeCapacity = cancelled.Economy!.Jobs.Single(item => item.JobId == "physicist").Capacity;
    for (var day = 0; day < (int)Math.Ceiling(upgrade.Quote!.DurationDays); day++) { Accept(session, host, new("lab-day-" + day, "stepDay")); }
    var replaced = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(replaced.Installed.Single(item => item.SlotId == lab.SlotId).DefinitionId == upgrade.DefinitionId && replaced.Economy!.Jobs.Single(item => item.JobId == "physicist").Capacity > beforeCapacity,
        "Завершение не заменило здание/economy graph атомарно.");
}

void ResearchEffectsIntegration()
{
    var selected = new[] { "tech_administrative_ai", "tech_eco_simulation", "tech_powered_exoskeletons", "tech_assembly_pattern" };
    var starting = ConstructionOrdinaryTemplate.Get("economic").StartingTechnologies;
    var definitions = starting.Concat(selected).Distinct(StringComparer.Ordinal).Select(id => sourceResearch!.Technologies[id] with
    { Cost = selected.Contains(id) ? 1m : sourceResearch!.Technologies[id].Cost, Tier = 0, Potential = null, WeightRules = null, Weight = 1m, CostRules = null, Prerequisites = [], Gates = [] }).ToArray();
    var diagnosticFixture = new TechnologyDefinition("tech_rc_unknown_swap", "physics", 1, 0, ["computing"], [], false, false, null, null, 1, null, [], "synthetic", 1, [])
    {
        SourceFields = Array.AsReadOnly(ParadoxParser.Parse("modifier = { all_technology_research_speed = 0.05 } technology_swap = { trigger = { has_country_flag = rc_unknown } inherit_effects = no modifier = { all_technology_research_speed = 0.20 } }").Entries.ToArray())
    };
    definitions = [.. definitions, diagnosticFixture];
    var catalog = new TechnologyCatalog(definitions, [new(0, 0, null)], 10, 0.5m, sourceResearch!.NoLeaderPenalty,
        categoryParents: sourceResearch.CategoryParents, workforceComposites: sourceResearch.WorkforceComposites, empireModifiers: sourceResearch.EmpireModifiers);
    var session = NewSession(useSourceEconomy: true, research: catalog);
    var host = session.Join("Эффекты", "player", null, TimeSpan.Zero);
    var second = session.Join("Сосед", "player", null, TimeSpan.Zero);
    Import(session, host, "ef1"); Import(session, second, "ef2");
    Accept(session, host, new("eft1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("eft2", "selectEmpireTemplate", TemplateId: "scientific"));
    Accept(session, host, new("efr1", "ready", Ready: true)); Accept(session, second, new("efr2", "ready", Ready: true));
    Accept(session, host, new("efs", "start"));
    var diagnosticEffect = session.View(host.ParticipantId).Research!.Empires.Single().Areas.Single(area => area.Id == "physics").Choices
        .Single(technology => technology.Id == diagnosticFixture.Id).Effects!.Single();
    Check(diagnosticEffect.Amount is null && diagnosticEffect.DiagnosticCode == "unknown-effect-swap" && diagnosticEffect.Status == "deferred",
        "Проекция скрыла точную причину неразрешённого значения эффекта.");
    var diagnosticWire = JsonSerializer.SerializeToElement(diagnosticEffect, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    Check(diagnosticWire.GetProperty("diagnosticCode").GetString() == "unknown-effect-swap", "Причина эффекта потеряна в camelCase wire DTO.");
    EconomyWorld World() => (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    object Owner(int id) => ((System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!)
        .Cast<object>().Single(owner => (int)owner.GetType().GetField("Id")!.GetValue(owner)! == id);
    EntityId Planet(object owner) => (EntityId)owner.GetType().GetField("Planet")!.GetValue(owner)!;
    var owner = Owner(host.ParticipantId);
    var planet = Planet(owner);
    var baseline = World().PreviewPlanetReport(planet);
    var beforeHash = World().StateHash();
    _ = session.View(host.ParticipantId);
    Check(World().StateHash() == beforeHash && World().Month == 0, "Предварительная проекция мутировала мир.");
    foreach (var id in selected.Take(3))
    {
        var current = session.View(host.ParticipantId).Research!.Empires.Single();
        var area = catalog.Technologies[id].Area;
        Check(current.Areas.Single(item => item.Id == area).Choices.Single(item => item.Id == id).Effects!.Any(effect => effect.Status == "active"), "Поддержанный будущий эффект остался deferred: " + id);
        Accept(session, host, new("ef-select-" + id, "selectResearch", Area: area, TechnologyId: id, ExpectedResearchRevision: current.Revision));
    }
    for (var day = 0; day < 30; day++) { Accept(session, host, new("ef-day-" + day, "stepDay")); }
    var month = World().GetPlanetReport(planet);
    Check(month.Produced.SequenceEqual(baseline.Produced) && month.JobUpkeep.SequenceEqual(baseline.JobUpkeep), "Новые эффекты изменили уже закрытый месяц.");
    var preview = World().PreviewPlanetReport(planet);
    Check(preview.Produced["food"] > month.Produced["food"], "Farmer output/worker Workforce не дошли до preview.");
    Check(preview.JobUpkeep["consumer_goods"] > month.JobUpkeep["consumer_goods"], "Admin AI upkeep не применён.");
    var country = (ResearchCountry)owner.GetType().GetField("Research")!.GetValue(owner)!;
    var effects = ResearchEffects.Project(country, sourceEconomy!.Catalog, sourceEconomy.RuntimeJobs,
        ConstructionOrdinaryTemplate.Get("economic").Ethics.Concat(ConstructionOrdinaryTemplate.Get("economic").Civics).Append("origin_default").ToHashSet(StringComparer.Ordinal), World().GetEffectiveJobResourceClosure(planet));
    var researchView = session.View(host.ParticipantId).Research!.Empires.Single();
    foreach (var area in researchView.Areas)
    { Check(area.ActiveTechnologyId is null && area.MonthlyResearch == preview.Net[area.Id + "_research"], "Idle science rate не соответствует RAW накоплению резерва после новых эффектов."); }
    var ownerCountry = (EntityId)owner.GetType().GetField("Country")!.GetValue(owner)!;
    foreach (var resource in sourceEconomy.Resources.Values.Where(resource => resource.SourceMaximum is not null))
    { World().SetBalance(ownerCountry, resource.Id, resource.SourceMaximum!.Value); }
    var colony = session.View(host.ParticipantId).Development!.Colonies.Single();
    var recipe = colony.Catalog.First(item => item.Kind == "building" && item.Quote is { DurationDays: > 90 } && item.SlotIds.Count > 0);
    var order = new M3ClientCommand("ef-build", "enqueueConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision,
        DefinitionId: recipe.DefinitionId, SlotId: recipe.SlotIds[0], OrderId: "ef-building");
    Accept(session, host, order);
    var paid = session.View(host.ParticipantId).Development!.Colonies.Single().Queue.Single(item => item.OrderId == "ef-building").PaidCosts;
    for (var day = 30; day < 59; day++) { Accept(session, host, new("ef-day-" + day, "stepDay")); }
    var refreshed = World().PreviewPlanetReport(planet);
    Check(refreshed.Produced.SequenceEqual(preview.Produced) && refreshed.JobUpkeep.SequenceEqual(preview.JobUpkeep), "Daily refresh накопил или потерял overlay.");
    // Ошибка второго владельца не публикует settlement первого.
    var secondOwner = Owner(second.ParticipantId);
    var secondCountry = (EntityId)secondOwner.GetType().GetField("Country")!.GetValue(secondOwner)!;
    World().SetBalance(secondCountry, "physics_research", decimal.MaxValue);
    var failedHash = World().StateHash();
    var failedResearch = owner.GetType().GetField("Research")!.GetValue(owner);
    var failure = session.Execute(host.ParticipantId, host.ConnectionId, new("ef-fail-month", "stepDay"), TimeSpan.Zero);
    Check(!failure.Accepted && World().StateHash() == failedHash && ReferenceEquals(failedResearch, owner.GetType().GetField("Research")!.GetValue(owner)), "Отказ второго владельца опубликовал часть транзакции.");
    World().SetBalance(secondCountry, "physics_research", 0);
    foreach (var resource in sourceEconomy.Resources.Values.Where(resource => resource.SourceMaximum is not null))
    { World().SetBalance(secondCountry, resource.Id, resource.SourceMaximum!.Value); }
    var next = session.View(host.ParticipantId).Research!.Empires.Single();
    Accept(session, host, new("ef-assembly", "selectResearch", Area: "engineering", TechnologyId: "tech_assembly_pattern", ExpectedResearchRevision: next.Revision));
    var activeEngineering = session.View(host.ParticipantId).Research!.Empires.Single().Areas.Single(area => area.Id == "engineering");
    Check(activeEngineering.MonthlyResearch == World().PreviewPlanetReport(planet).Net["engineering_research"] * effects.AreaResearchMultipliers["engineering"],
        "После выбора проекта отображаемая скорость не применяет исходный active multiplier.");
    var beforeCompletionWork = session.View(host.ParticipantId).Development!.Colonies.Single().Queue.Single(item => item.OrderId == "ef-building").CompletedWork;
    Accept(session, host, new("ef-day-59", "stepDay"));
    var baseSpeed = sourceConstruction!.PresetModifiers(ConstructionOrdinaryTemplate.Get("economic"), ConstructionKind.Building).Speed;
    var completionWork = session.View(host.ParticipantId).Development!.Colonies.Single().Queue.Single(item => item.OrderId == "ef-building").CompletedWork;
    Check(completionWork == beforeCompletionWork + 1 + baseSpeed, "Новая технология добавила работу в день завершения исследования.");
    Accept(session, host, new("ef-work", "stepDay"));
    var pending = session.View(host.ParticipantId).Development!.Colonies.Single().Queue.Single(item => item.OrderId == "ef-building");
    Check(pending.CompletedWork == completionWork + 1 + baseSpeed + .25m && pending.PaidCosts.SequenceEqual(paid), "Live building speed или фиксированная стоимость оплаченного заказа неверны.");
}

void FullResearchSmoke()
{
    foreach (var template in new[] { "economic", "scientific", "military" })
    {
        var session = NewSession(useSourceEconomy: true, research: sourceResearch);
        var host = session.Join("Пул", "player", null, TimeSpan.Zero);
        var second = session.Join("Сосед", "player", null, TimeSpan.Zero);
        Import(session, host, "sm1"); Import(session, second, "sm2");
        Accept(session, host, new("smt1", "selectEmpireTemplate", TemplateId: template));
        Accept(session, second, new("smt2", "selectEmpireTemplate", TemplateId: "economic"));
        Accept(session, host, new("smr1", "ready", Ready: true)); Accept(session, second, new("smr2", "ready", Ready: true));
        Accept(session, host, new("sms", "start"));
        var state = session.View(host.ParticipantId).Research!.Empires.Single();
        Console.WriteLine($"Источник {template}: catalog={state.CatalogCount}, eligible={state.EligibleCount}, unknown={state.Unavailable!.Count}; "
            + string.Join("; ", state.Areas.Select(area => area.Id + "=" + string.Join(",", area.Choices.Select(item => item.Id)))));
        if (template == "economic")
        {
            var participants = (System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
            var owner = participants.Cast<object>().First();
            var country = (ResearchCountry)owner.GetType().GetField("Research")!.GetValue(owner)!;
            Console.WriteLine("Unknown source facts: " + string.Join(", ", sourceResearch!.Technologies.Keys.SelectMany(id => country.MissingFacts(id)).GroupBy(key => key).OrderByDescending(group => group.Count()).Take(18).Select(group => group.Key + "=" + group.Count())));
            var assessed = sourceResearch.Technologies.Keys.Select(id => (Id: id, Legal: country.Eligibility(id), Draw: country.Assess(id))).ToArray();
            Console.WriteLine($"Potential: true={assessed.Count(item => !item.Legal.Gates.Contains("potential-unknown") && !item.Legal.Gates.Contains("potential-false"))}, false={assessed.Count(item => item.Legal.Gates.Contains("potential-false"))}, unknown={assessed.Count(item => item.Legal.Gates.Contains("potential-unknown"))}.");
            Console.WriteLine("Eligible legality blocked only draw weight: " + string.Join(", ", assessed.Where(item => item.Legal.Available && !item.Draw.Available).Select(item => item.Id + "[" + string.Join(";", item.Draw.Gates) + "]")));
            foreach (var technology in new[] { "tech_physics_1", "tech_society_1", "tech_engineering_1", "tech_basic_science_lab_2", "tech_colonial_centralization" })
            {
                Console.WriteLine(technology + ": " + string.Join(",", country.Assess(technology).Gates) + "; missing=" + string.Join(",", country.MissingFacts(technology)));
            }
        }
        if (args.Contains("--research-diagnostics")) { continue; }
        Check(state.Areas.All(area => area.Choices.Count > 0), "Исходный пул не дал выбора во всех трёх направлениях.");
        foreach (var area in state.Areas)
        {
            Accept(session, host, new("sm-select-" + area.Id, "selectResearch", Area: area.Id, TechnologyId: area.Choices[0].Id, ExpectedResearchRevision: state.Revision));
            state = session.View(host.ParticipantId).Research!.Empires.Single();
        }
        for (var day = 0; day < 30; day++) { Accept(session, host, new("sm-day-" + day, "stepDay")); }
        Check(session.View(host.ParticipantId).Research!.Empires.Single().Areas.All(area => area.Progress > 0), "Source исследования не получили месячный прогресс.");
    }
}

void LiveConstructionIntegration()
{
    if (sourceEconomy is null || sourceStartup is null || sourceConstruction is null) { throw new Exception("Real source construction profile was not created."); }
    var session = NewSession(useSourceEconomy: true);
    var host = session.Join("Интеграция", "player", null, TimeSpan.Zero);
    var second = session.Join("Сосед", "player", null, TimeSpan.Zero);
    var observer = session.Join("Наблюдатель", "observer", null, TimeSpan.Zero);
    Import(session, host, "ci1"); Import(session, second, "ci2"); Import(session, observer, "ci3");
    Accept(session, host, new("ct1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("ct2", "selectEmpireTemplate", TemplateId: "scientific"));
    Accept(session, host, new("approve-ci-observer", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Accept(session, host, new("cr1", "ready", Ready: true)); Accept(session, second, new("cr2", "ready", Ready: true));
    Accept(session, host, new("cs", "start"));
    var initial = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(initial.Catalog.Count(item => item.Kind == "building") == 165 && initial.Catalog.Count(item => item.Kind == "zone") == 29,
        "Расширенный authoritative каталог потерял ordinary buildings/zones.");
    var lockedTier = initial.Catalog.Single(item => item.DefinitionId == "building_research_lab_2");
    Check(lockedTier.UpgradesFrom!.Contains("building_research_lab_1") && lockedTier.Prerequisites!.Count > 0
        && lockedTier.SourceRecipe!.Count > 0 && lockedTier.CompatibleSlotIds!.Count > 0 && lockedTier.SlotIds.Count == 0
        && lockedTier.UpgradeOperationGate == "installed-source-required", "Locked upgrade потерял source recipe/graph/совместимые места.");
    Check(initial.Catalog.All(item => item.LocalizationKey is not null) && initial.Installed.All(item => item.LocalizationKey is not null)
        && initial.Features.All(item => item.LocalizationKey is not null), "Catalog/installed/features потеряли source localization keys.");
    Check(initial.Economy!.JobDefinitions?.Count == 31 && initial.Economy.JobDefinitions.All(item => item.LocalizationKey is not null)
        && initial.Economy.Jobs.Single(item => item.JobId == "physicist").LocalizationKey == "job_physicist"
        && initial.Economy.Jobs.Single(item => item.JobId == "foundry").DescriptionKey == "job_foundry_desc", "Live и неназначенные JobCapacity definitions потеряли exact job localization keys.");
    Check(session.View(host.ParticipantId).Development!.Gates.Count == 0 && initial.Gates.Count == 0,
        $"Fully imported live source runtime retained gates: profile={string.Join(",", session.View(host.ParticipantId).Development!.Gates.Select(gate => gate.DependencyId))}; colony={string.Join(",", initial.Gates.Select(gate => gate.DependencyId))}; missingStartup={initial.StartupDistricts is null}; catalogImage={initial.Catalog.FirstOrDefault()?.ImageDefinitionKey ?? "null"}.");
    Check(initial.Slots.Count > 0 && initial.Installed.Count > 0 && initial.StartupBuildings!.All(building => initial.Installed.Any(item => item.SlotId == building.SlotInstanceId && item.DefinitionId == building.DefinitionId)),
        "Accepted startup graph was not bound to exact runtime slots without duplication.");
    Check(initial.Slots.Where(item => item.Kind == "building").All(item => item.ZoneInstanceId is not null && initial.StartupZones!.Any(zone => zone.InstanceId == item.ZoneInstanceId))
        && initial.Slots.Where(item => item.Kind == "district").All(item => item.ZoneInstanceId is null),
        "Construction slots did not preserve exact source zone instance bindings (or district slots gained a zone binding).");
    Check(initial.Catalog.Any(item => item.DefinitionId == "building_foundry_1" && item.Eligibility == "available" && item.SlotIds.Count > 0 && item.Quote is not null),
        "A source-supported ordinary building did not produce an exact live quote/slot.");
    var observerBefore = JsonSerializer.Serialize(session.View(observer.ParticipantId).Development);
    Check(!observerBefore.Contains(initial.ColonyId, StringComparison.Ordinal), "General observer received a private construction colony.");
    var quote = initial.Catalog.Single(item => item.DefinitionId == "building_foundry_1").Quote!;
    var cityDistrictQuote = initial.Catalog.Single(item => item.DefinitionId == "district_city" && item.Eligibility == "available").Quote!;
    var slot = initial.Catalog.Single(item => item.DefinitionId == "building_foundry_1").SlotIds[0];
    var beforeStock = initial.Economy!.Resources.ToDictionary(item => item.ResourceId, item => item.Stock, StringComparer.Ordinal);
    var command = new M3ClientCommand("build1", "enqueueConstruction", ColonyId: initial.ColonyId, ExpectedColonyRevision: initial.Revision,
        DefinitionId: "building_foundry_1", SlotId: slot, OrderId: "order-foundry-1");
    Reject(session, host, command, "insufficient-resources");
    var rejected = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(rejected.Revision == initial.Revision && rejected.Queue.Count == 0
        && beforeStock.All(pair => rejected.Economy!.Resources.Single(item => item.ResourceId == pair.Key).Stock == pair.Value),
        "Unaffordable construction changed revision, queue or resource stock.");
    var replay = session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero);
    Check(!replay.Accepted && replay.Code == "insufficient-resources", "Same rejected command ID/payload did not replay its receipt.");
    Reject(session, host, command with { CommandId = "stale-build", ExpectedColonyRevision = initial.Revision + 1, OrderId = "order-stale" }, "STALE_REVISION");
    Reject(session, second, command with { CommandId = "foreign-build", ColonyId = initial.ColonyId, OrderId = "order-foreign" }, "OUT_OF_SCOPE");
    Reject(session, observer, command with { CommandId = "observer-build", OrderId = "order-observer" }, "OUT_OF_SCOPE");
    var conflict = session.Execute(host.ParticipantId, host.ConnectionId, command with { DefinitionId = "building_stronghold" }, TimeSpan.Zero);
    Check(!conflict.Accepted && conflict.Code == "ID_CONFLICT", "Reused command ID with a different body did not conflict.");

    for (var day = 1; day <= 30; day++) { Accept(session, host, new("baseline-day-" + day, "stepDay")); }
    var baselineEconomy = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!;
    var baselineMineralNet = baselineEconomy.Resources.Single(item => item.ResourceId == "minerals").Net;
    var baselineConsumerGoodsNet = baselineEconomy.Resources.Single(item => item.ResourceId == "consumer_goods").Net;
    Check(baselineMineralNet == -14m,
        $"Baseline funding net changed: minerals={baselineMineralNet}, consumer_goods={baselineConsumerGoodsNet}.");

    foreach (var job in initial.Economy.Jobs.Where(item => item.JobId is ("foundry" or "artisan" or "physicist" or "biologist" or "engineer") && item.Capacity > 0))
    {
        var current = session.View(host.ParticipantId).Development!.Colonies.Single();
        if (job.JobId == "foundry")
        {
            Accept(session, host, new("focus-zero-" + job.JobId, "setWorkforceLimitMin", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId));
        }
        else if (job.JobId == "artisan")
        {
            Accept(session, host, new("focus-half-" + job.JobId, "setWorkforceLimit", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId, Count: job.Capacity / 2m));
        }
        else
        {
            Accept(session, host, new("focus-zero-researcher-" + job.JobId, "setWorkforceLimitMin", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId));
        }
    }
    var secondInitial = session.View(second.ParticipantId).Development!.Colonies.Single();
    foreach (var job in secondInitial.Economy!.Jobs.Where(item => item.JobId is ("foundry" or "artisan" or "physicist" or "biologist" or "engineer") && item.Capacity > 0))
    {
        var current = session.View(second.ParticipantId).Development!.Colonies.Single();
        if (job.JobId == "foundry")
        {
            Accept(session, second, new("second-focus-foundry", "setWorkforceLimitMin", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId));
        }
        else if (job.JobId == "artisan")
        {
            Accept(session, second, new("second-focus-half-artisan", "setWorkforceLimit", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId, Count: job.Capacity / 2m));
        }
        else
        {
            Accept(session, second, new("second-focus-zero-researcher-" + job.JobId, "setWorkforceLimitMin", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId));
        }
    }
    var focusedSecond = session.View(second.ParticipantId).Development!.Colonies.Single();
    Accept(session, second, new("second-focus-max-miner", "setWorkforceLimitMax", ColonyId: focusedSecond.ColonyId,
        ExpectedColonyRevision: focusedSecond.Revision, Job: "miner"));
    focusedSecond = session.View(second.ParticipantId).Development!.Colonies.Single();
    var secondMiner = focusedSecond.Economy!.Jobs.Single(item => item.JobId == "miner");
    Check(secondMiner.Assigned > 0, $"Second-country miner was not assigned after focus: {secondMiner.Assigned}/{secondMiner.Capacity}; jobs={string.Join(",", focusedSecond.Economy.Jobs.Select(item => item.JobId + "=" + item.Assigned + "/" + item.Limit))}.");
    var focused = session.View(host.ParticipantId).Development!.Colonies.Single();
    Accept(session, host, new("focus-miner", "setWorkforceLimitMax", ColonyId: focused.ColonyId,
        ExpectedColonyRevision: focused.Revision, Job: "miner"));
    var focusedEconomy = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!;
    for (var day = 31; day <= 60; day++) { Accept(session, host, new("focused-day-" + day, "stepDay")); }
    focusedEconomy = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!;
    var focusedMineralNet = focusedEconomy.Resources.Single(item => item.ResourceId == "minerals").Net;
    var focusedConsumerGoodsNet = focusedEconomy.Resources.Single(item => item.ResourceId == "consumer_goods").Net;
    Console.WriteLine($"Funding monthly net: minerals {baselineMineralNet} → {focusedMineralNet}; consumer_goods {baselineConsumerGoodsNet} → {focusedConsumerGoodsNet}.");
    Check(focusedMineralNet is > 0 && focusedConsumerGoodsNet is >= 0,
        $"Legitimate workforce funding did not change monthly net to sustainable values: minerals={focusedMineralNet}, consumer_goods={focusedConsumerGoodsNet}.");
    var secondEconomy = session.View(second.ParticipantId).Development!.Colonies.Single().Economy!;
    var secondMineralNet = secondEconomy.Resources.Single(item => item.ResourceId == "minerals").Net;
    Check(secondMineralNet is >= 0, $"Second-country funding net minerals={secondMineralNet}; jobs={string.Join(",", secondEconomy.Jobs.Select(item => item.JobId + "=" + item.Assigned + "/" + item.Limit))}.");
    Check(focusedEconomy.Jobs.Single(item => item.JobId == "miner").Assigned == focusedEconomy.Jobs.Single(item => item.JobId == "miner").Capacity
        && focusedEconomy.Jobs.Single(item => item.JobId == "foundry").Limit == 0,
        "Source WorkforceLimit commands did not free miner capacity from mineral-consuming jobs.");
    var affordable = false;
    for (var month = 0; month < 30 && !affordable; month++)
    {
        for (var day = 0; day < 30; day++) { Accept(session, host, new($"fund-{month}-{day}", "stepDay")); }
        var stock = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!.Resources.Single(item => item.ResourceId == "minerals").Stock;
        affordable = stock >= quote.Costs.GetValueOrDefault("minerals");
    }
    Check(affordable, "Legitimate source WorkforceLimit progression did not fund the accepted building quote within 30 months.");
    var funded = session.View(host.ParticipantId).Development!.Colonies.Single();
    var liveOperations = RunConstructionConsumer(funded, session.View(host.ParticipantId).Development!.Gates);
    var enqueueOperation = liveOperations.EnumerateArray().First(operation => operation.GetProperty("type").GetString() == "enqueueConstruction"
        && operation.GetProperty("definitionId").GetString() == "building_foundry_1");
    var nodeSlot = enqueueOperation.GetProperty("slotId").GetString()!;
    var nodeBuild = new M3ClientCommand("paid-build", "enqueueConstruction", ColonyId: funded.ColonyId, ExpectedColonyRevision: funded.Revision,
        DefinitionId: "building_foundry_1", SlotId: nodeSlot, OrderId: "order-paid");
    var fundedStock = funded.Economy!.Resources.ToDictionary(item => item.ResourceId, item => item.Stock, StringComparer.Ordinal);
    Accept(session, host, nodeBuild);
    var paid = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(paid.Queue.Single(item => item.OrderId == "order-paid").Status == "pending"
        && quote.Costs.All(pair => paid.Economy!.Resources.Single(item => item.ResourceId == pair.Key).Stock == fundedStock[pair.Key] - pair.Value),
        "Funded source construction did not reserve the exact server quote.");
    var faultBefore = JsonSerializer.Serialize(session.View(host.ParticipantId).Development);
    var startupMap = typeof(M3GameSession).GetField("_startupColonies", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    var backingField = startupMap.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(field => typeof(IDictionary<string, M3StartupColony>).IsAssignableFrom(field.FieldType));
    var startupBacking = (IDictionary<string, M3StartupColony>)backingField.GetValue(startupMap)!;
    var originalStartup = startupBacking[paid.ColonyId];
    var faultZoneId = originalStartup.Zones.First(zone => sourceCatalog!.Zones.ContainsKey(zone.ZoneId)).InstanceId;
    startupBacking[paid.ColonyId] = originalStartup with
    {
        Zones = Array.AsReadOnly(originalStartup.Zones.Select(zone => zone.InstanceId == faultZoneId
            ? zone with { DistrictInstanceId = "fault-injected-unresolved-district" } : zone).ToArray())
    };
    var graphFailure = session.Execute(host.ParticipantId, host.ConnectionId,
        new M3ClientCommand("fault-cancel", "cancelConstruction", ColonyId: paid.ColonyId,
            ExpectedColonyRevision: paid.Revision, OrderId: "order-paid"), TimeSpan.Zero);
    Check(!graphFailure.Accepted && graphFailure.Code == "INVALID_TARGET",
        $"Invalid source graph did not become a typed atomic construction rejection: accepted={graphFailure.Accepted}, code={graphFailure.Code}.");
    startupBacking[paid.ColonyId] = originalStartup;
    Check(faultBefore == JsonSerializer.Serialize(session.View(host.ParticipantId).Development),
        "Graph failure changed the published bank, queue, revision, date, or source projection.");
    var cancelOperations = RunConstructionConsumer(paid, session.View(host.ParticipantId).Development!.Gates);
    Check(cancelOperations.EnumerateArray().Any(operation => operation.GetProperty("type").GetString() == "cancelConstruction"
        && operation.GetProperty("orderId").GetString() == "order-paid"),
        "Actual live Node consumer did not expose cancellation for an accepted paid order.");
    var nodeCancel = cancelOperations.EnumerateArray().Single(operation => operation.GetProperty("type").GetString() == "cancelConstruction"
        && operation.GetProperty("orderId").GetString() == "order-paid");
    Accept(session, host, new("cancel-paid", "cancelConstruction", ColonyId: paid.ColonyId, ExpectedColonyRevision: paid.Revision,
        OrderId: nodeCancel.GetProperty("orderId").GetString()));
    var refunded = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(quote.Costs.All(pair => refunded.Economy!.Resources.Single(item => item.ResourceId == pair.Key).Stock == fundedStock[pair.Key]),
        "Cancel did not refund the paid construction cost to its original source account.");
    Accept(session, host, command with { CommandId = "complete-build", ExpectedColonyRevision = refunded.Revision, OrderId = "order-complete" });
    for (var day = 0; day < 29; day++) { Accept(session, host, new("before-atomic-month-" + day, "stepDay")); }
    var beforeAtomicHost = session.View(host.ParticipantId);
    var beforeAtomicSecond = session.View(second.ParticipantId);
    var secondStartup = startupBacking[session.View(second.ParticipantId).Development!.Colonies.Single().ColonyId];
    var secondFaultZone = secondStartup.Zones.First(zone => sourceCatalog!.Zones.ContainsKey(zone.ZoneId)).InstanceId;
    startupBacking[secondStartup.ColonyId] = secondStartup with
    {
        Zones = Array.AsReadOnly(secondStartup.Zones.Select(zone => zone.InstanceId == secondFaultZone
            ? zone with { DistrictInstanceId = "fault-injected-later-colony" } : zone).ToArray())
    };
    var laterColonyFailure = session.Execute(host.ParticipantId, host.ConnectionId, new("later-colony-month-fault", "stepDay"), TimeSpan.Zero);
    startupBacking[secondStartup.ColonyId] = secondStartup;
    var afterAtomicHost = session.View(host.ParticipantId);
    var afterAtomicSecond = session.View(second.ParticipantId);
    Check(!laterColonyFailure.Accepted && laterColonyFailure.Code == "INVALID_PHASE",
        "A later-colony source failure at month end was not returned as a controlled day rejection.");
    Check(beforeAtomicHost.ElapsedDay == afterAtomicHost.ElapsedDay && beforeAtomicHost.Tick == afterAtomicHost.Tick
        && JsonSerializer.Serialize(beforeAtomicHost.Development) == JsonSerializer.Serialize(afterAtomicHost.Development)
        && beforeAtomicSecond.ElapsedDay == afterAtomicSecond.ElapsedDay && beforeAtomicSecond.Tick == afterAtomicSecond.Tick
        && JsonSerializer.Serialize(beforeAtomicSecond.Development) == JsonSerializer.Serialize(afterAtomicSecond.Development),
        "Later-colony/month failure partially changed day, queue progress, revisions, resources or projections.");
    Accept(session, host, new("retry-atomic-month-day", "stepDay"));
    for (var day = 2; day <= 314; day++) { Accept(session, host, new("buildday" + day, "stepDay")); }
    var completed = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(completed.Queue.Single(order => order.OrderId == "order-complete").Status == "completed", "A 360-work building at +15% did not complete on the 314th active day.");
    Check(completed.Installed.Any(item => item.DefinitionId == "building_foundry_1" && item.SlotId == slot), "Completion did not refresh the installed graph in its canonical slot.");
    Check(completed.Slots.Single(item => item.SlotId == slot).ZoneInstanceId == initial.Slots.Single(item => item.SlotId == slot).ZoneInstanceId
        && completed.StartupZones!.Any(zone => zone.InstanceId == completed.Slots.Single(item => item.SlotId == slot).ZoneInstanceId),
        "Completed paid building lost the canonical selected slot's exact source zone instance binding.");
    Check(completed.Economy!.Jobs.Single(item => item.JobId == "foundry").Capacity > funded.Economy.Jobs.Single(item => item.JobId == "foundry").Capacity,
        "Completed building did not refresh source job capacity.");
    Check(completed.Economy.Jobs.Single(item => item.JobId == "foundry").Limit == 0,
        "New building capacity reset the user's zero foundry WorkforceLimit.");
    var citySlot = completed.Slots.First(item => item.Kind == "district" && item.ReservedByOrderId is null && item.InstalledDefinitionId is null);
    var cityBefore = completed.StartupDistricts!.Single(item => item.DefinitionId == "district_city").Level;
    Check(cityBefore == 3, "Source startup city district level was not three before the live upgrade.");
    var cityQuote = completed.Catalog.Single(item => item.DefinitionId == "district_city" && item.Eligibility == "available").Quote!;
    var cityAffordable = false;
    for (var month = 0; month < 100 && !cityAffordable; month++)
    {
        for (var day = 0; day < 30; day++) { var current = session.View(host.ParticipantId).Development!.Colonies.Single(); Accept(session, host, new($"city-fund-{month}-{day}", "stepDay")); }
        cityAffordable = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!.Resources.Single(item => item.ResourceId == "minerals").Stock
            >= cityQuote.Costs.GetValueOrDefault("minerals");
    }
    Check(cityAffordable, $"Accepted source WorkforceLimit choices did not fund city quote: stock={session.View(host.ParticipantId).Development!.Colonies.Single().Economy!.Resources.Single(item => item.ResourceId == "minerals").Stock}, cost={cityQuote.Costs.GetValueOrDefault("minerals")}.");
    completed = session.View(host.ParticipantId).Development!.Colonies.Single();
    Accept(session, host, new("city-upgrade", "enqueueConstruction", ColonyId: completed.ColonyId, ExpectedColonyRevision: completed.Revision,
        DefinitionId: "district_city", SlotId: citySlot.SlotId, OrderId: "order-city-level-four"));
    var cityQueued = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(cityQueued.Queue.Single(order => order.OrderId == "order-city-level-four").Status == "pending",
        "Actual city 3→4 district order was not queued.");
    var cityDays = (int)Math.Ceiling(cityQuote.DurationDays);
    for (var day = 1; day <= cityDays; day++) { Accept(session, host, new("city-day" + day, "stepDay")); }
    var cityCompleted = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(cityCompleted.StartupDistricts!.Single(item => item.DefinitionId == "district_city").Level == 4,
        "Live district completion did not update city level 3→4.");
    Check(cityCompleted.Economy!.Jobs.Single(item => item.JobId == "physicist").Capacity
        > completed.Economy!.Jobs.Single(item => item.JobId == "physicist").Capacity,
        "Live city 3→4 did not add an attached-district researcher contribution.");
    Check(cityCompleted.Economy.Jobs.Single(item => item.JobId == "foundry").Limit == 0,
        "City district capacity refresh reset the user's zero foundry WorkforceLimit.");
    Check(cityCompleted.Economy.Resources.All(resource => resource.ZoneUpkeep == completed.Economy.Resources.Single(before => before.ResourceId == resource.ResourceId).ZoneUpkeep),
        "Live city 3→4 multiplied static zone upkeep.");
    var observerProjection = session.View(observer.ParticipantId).Development!.Colonies;
    Check(observerProjection.Count == 0, "General observer received construction state after progression.");


}

void ExtendedOrdinaryRuntime()
{
    var session = NewSession(useSourceEconomy: true);
    var host = session.Join("Каталог", "player", null, TimeSpan.Zero);
    var second = session.Join("Сосед", "player", null, TimeSpan.Zero);
    Import(session, host, "ext-import-host"); Import(session, second, "ext-import-second");
    Accept(session, host, new("ext-template-host", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("ext-template-second", "selectEmpireTemplate", TemplateId: "scientific"));
    Accept(session, host, new("ext-ready-host", "ready", Ready: true));
    Accept(session, second, new("ext-ready-second", "ready", Ready: true));
    Accept(session, host, new("ext-start", "start"));
    foreach (var player in new[] { host, second })
    {
        var initial = session.View(player.ParticipantId).Development!.Colonies.Single();
        foreach (var job in initial.Economy!.Jobs.Where(job => job.JobId is "foundry" or "artisan" or "physicist" or "biologist" or "engineer"))
        {
            var current = session.View(player.ParticipantId).Development!.Colonies.Single();
            Accept(session, player, new("ext-focus-" + player.ParticipantId + "-" + job.JobId, "setWorkforceLimit", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId, Count: job.JobId == "artisan" ? job.Capacity / 2 : 0));
        }
        var mining = session.View(player.ParticipantId).Development!.Colonies.Single();
        Accept(session, player, new("ext-miner-" + player.ParticipantId, "setWorkforceLimitMax", ColonyId: mining.ColonyId, ExpectedColonyRevision: mining.Revision, Job: "miner"));
    }
    var cityCompleted = session.View(host.ParticipantId).Development!.Colonies.Single();
    var extended = cityCompleted.Catalog.Single(item => item.DefinitionId == "building_hydroponics_farm");
    Check(extended.Quote is not null && extended.Eligibility == "available", "Source-backed добавление не исполняемо.");
    for (var month = 0; month < 100 && !extended.Quote!.Costs.All(pair => cityCompleted.Economy!.Resources.Single(resource => resource.ResourceId == pair.Key).Stock >= pair.Value); month++)
    {
        for (var day = 0; day < 30; day++) { Accept(session, host, new($"extended-fund-{month}-{day}", "stepDay")); }
        cityCompleted = session.View(host.ParticipantId).Development!.Colonies.Single();
    }
    var extendedBefore = cityCompleted;
    var extendedSlot = extended.SlotIds[0];
    Accept(session, host, new("extended-hydroponics", "enqueueConstruction", ColonyId: cityCompleted.ColonyId, ExpectedColonyRevision: cityCompleted.Revision,
        DefinitionId: extended.DefinitionId, SlotId: extendedSlot, OrderId: "order-extended"));
    var extendedPaid = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(extended.Quote!.Costs.All(pair => extendedPaid.Economy!.Resources.Single(resource => resource.ResourceId == pair.Key).Stock
        == extendedBefore.Economy!.Resources.Single(resource => resource.ResourceId == pair.Key).Stock - pair.Value), "Расширенное здание не списало точную цену.");
    for (var day = 1; day <= (int)Math.Ceiling(extended.Quote.DurationDays); day++) { Accept(session, host, new("extended-day-" + day, "stepDay")); }
    var extendedCompleted = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(extendedCompleted.Installed.Count(item => item.SlotId == extendedSlot && item.DefinitionId == extended.DefinitionId) == 1
        && extendedCompleted.Economy!.Jobs.Single(job => job.JobId == "farmer").Capacity > extendedBefore.Economy!.Jobs.Single(job => job.JobId == "farmer").Capacity,
        "Расширенное здание не создало реальные farmer jobs ровно один раз.");
    Check(extendedCompleted.Slots.Single(item => item.SlotId == extendedSlot).ZoneInstanceId == extendedBefore.Slots.Single(item => item.SlotId == extendedSlot).ZoneInstanceId
        && extendedCompleted.Economy!.Jobs.Single(job => job.JobId == "foundry").Limit == 0, "Расширенное здание потеряло slot-zone identity либо zero limit.");
    Accept(session, host, new("extended-farmer-max", "setWorkforceLimitMax", ColonyId: extendedCompleted.ColonyId, ExpectedColonyRevision: extendedCompleted.Revision, Job: "farmer"));
    for (var day = 0; day < 30; day++) { Accept(session, host, new("extended-production-" + day, "stepDay")); }
    var extendedReport = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!;
    Check(extendedReport.Resources.Single(item => item.ResourceId == "food").Produced > extendedBefore.Economy!.Resources.Single(item => item.ResourceId == "food").Produced
        && extendedReport.Resources.Single(item => item.ResourceId == "energy").BuildingUpkeep > extendedBefore.Economy.Resources.Single(item => item.ResourceId == "energy").BuildingUpkeep,
        "Расширенное здание не изменило настоящие production/upkeep отчёта месяца.");
}

void LongAutomaticConstructionRuntime()
{
    var tech = ConstructionOrdinaryTemplate.Get("economic").StartingTechnologies.ToHashSet(StringComparer.Ordinal);
    var session = NewSession(useSourceEconomy: true, researchedTechnologies: _ => tech);
    var host = session.Join("Long automatic", "player", null, TimeSpan.Zero);
    var second = session.Join("Long second", "player", null, TimeSpan.Zero);
    foreach (var player in new[] { host, second })
    {
        Import(session, player, "long-import");
        Accept(session, player, new("long-template", "selectEmpireTemplate", TemplateId: player == host ? "economic" : "scientific"));
        Accept(session, player, new("long-ready", "ready", Ready: true));
    }
    Accept(session, host, new("long-start", "start"));
    foreach (var player in new[] { host, second })
    {
        var initial = session.View(player.ParticipantId).Development!.Colonies.Single();
        foreach (var job in initial.Economy!.Jobs.Where(job => job.JobId is "foundry" or "artisan" or "physicist" or "biologist" or "engineer"))
        {
            var current = session.View(player.ParticipantId).Development!.Colonies.Single();
            Accept(session, player, new("long-focus-" + job.JobId, "setWorkforceLimit", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, Job: job.JobId, Count: job.JobId == "artisan" ? job.Capacity / 2 : 0));
        }
        var mining = session.View(player.ParticipantId).Development!.Colonies.Single();
        Accept(session, player, new("long-miner", "setWorkforceLimitMax", ColonyId: mining.ColonyId, ExpectedColonyRevision: mining.Revision, Job: "miner"));
    }
    var now = 0;
    AdvanceAutomatic(6000, "idle");
    var idle = View();
    Check(session.ObserveTiming().ElapsedDay == 6000 && session.View(host.ParticipantId).LastError is null,
        "Source automatic clock stopped before 6000 days: elapsed=" + session.ObserveTiming().ElapsedDay + "; " + session.View(host.ParticipantId).LastError);
    var runtimes = (Dictionary<string, ConstructionLiveRuntime>)typeof(M3GameSession).GetField("_constructionRuntimes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    foreach (var runtime in runtimes.Values)
    {
        var receipts = (IReadOnlyDictionary<string, ConstructionReceipt>)typeof(ConstructionLiveRuntime).GetField("_receipts", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(runtime)!;
        Check(receipts.Count == 0, "Empty queue consumed construction receipts.");
        var ownerView = session.View(runtime.OwnerId).Development!.Colonies.Single();
        Check(runtime.Revision == ownerView.Revision, "Idle runtime revision detached from participant revision.");
    }
    var building = idle.Catalog.Single(item => item.DefinitionId == "building_hydroponics_farm");
    Check(building.Quote is not null && building.CanAfford == true, "Actual months/workforce did not fund source build after 6000 days.");
    var beforeMinerals = idle.Economy!.Resources.Single(item => item.ResourceId == "minerals").Stock;
    Accept(session, host, new("long-paid-build", "enqueueConstruction", ColonyId: idle.ColonyId, ExpectedColonyRevision: idle.Revision,
        DefinitionId: building.DefinitionId, SlotId: building.SlotIds[0], OrderId: "long-building"), now);
    Check(View().Economy!.Resources.Single(item => item.ResourceId == "minerals").Stock == beforeMinerals - building.Quote!.Costs["minerals"], "Long automatic build did not pay real bank.");
    AdvanceAutomatic((int)Math.Ceiling(building.Quote.DurationDays), "building");
    var built = View();
    Check(built.Queue.Single(item => item.OrderId == "long-building").Status == "completed", "Nonempty queue stopped checking/working after idle days.");
    // Только prerequisite unit fixture; stocks и 6000+ дней получены настоящей экономикой/clock, без reset.
    tech.Add("tech_colonial_centralization");
    var capital = built.Installed.Single(item => item.DefinitionId == "building_capital");
    var upgrade = View().Installed.Single(item => item.SlotId == capital.SlotId).Upgrades!.Single(item => item.DefinitionId == "building_major_capital");
    Check(upgrade.Quote is not null && upgrade.CanEnqueue, "Real bank/queue cannot enqueue upgrade after >4096 elapsed days.");
    Accept(session, host, new("long-paid-upgrade", "enqueueUpgrade", ColonyId: built.ColonyId, ExpectedColonyRevision: built.Revision,
        DefinitionId: upgrade.DefinitionId, SourceDefinitionId: upgrade.SourceDefinitionId, SlotId: upgrade.SlotId, ZoneInstanceId: upgrade.ZoneInstanceId, OrderId: "long-upgrade"), now);
    AdvanceAutomatic((int)Math.Ceiling(upgrade.Quote!.DurationDays), "upgrade");
    var upgraded = View();
    Check(upgraded.Installed.Single(item => item.SlotId == capital.SlotId).DefinitionId == "building_major_capital"
        && upgraded.Queue.Single(item => item.OrderId == "long-upgrade").Status == "completed", "Paid startup upgrade failed in long-lived session.");

    M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
    void AdvanceAutomatic(int days, string phase)
    {
        Accept(session, host, new("long-resume-" + phase, "pause", Paused: false), now);
        for (var day = 0; day < days; day++) { session.Tick(TimeSpan.FromSeconds(++now)); }
        Accept(session, host, new("long-pause-" + phase, "pause", Paused: true), now);
    }
}

void InfrastructureConditionRefresh()
{
    // Явный test-only permission: не native default и не изменение файлов установки.
    var fixture = ConstructionPermissionFixture.AllowDisable(sourceConstruction!, "building_generator_districts_1", "building_capital");
    Check(!sourceConstruction!.Content.Catalogs.Values.SelectMany(catalog => catalog.Definitions.Values).SelectMany(definition => definition.Fields)
        .Any(field => field.Key == "can_be_disabled" && field.Scalar == "yes"), "Fixture изменила native import");
    foreach (var mode in new[] { 0, 1, 2 })
    {
        var rollback = mode == 1;
        var scenarioContent = mode == 2 ? ConstructionPermissionFixture.UntilBuildingCount(sourceConstruction!, "building_foundry_1", "building_foundry_1", 1) : fixture;
        var session = NewSession(useSourceEconomy: true, constructionOverride: scenarioContent);
        var host = session.Join("Condition fixture", "player", null, TimeSpan.Zero);
        var second = session.Join("Other fixture", "player", null, TimeSpan.Zero);
        foreach (var player in new[] { host, second }) Import(session, player, "refresh-import-" + player.ParticipantId);
        Accept(session, host, new("refresh-template", "selectEmpireTemplate", TemplateId: "economic"));
        Accept(session, second, new("refresh-template-2", "selectEmpireTemplate", TemplateId: "scientific"));
        Accept(session, host, new("refresh-ready-1", "ready", Ready: true)); Accept(session, second, new("refresh-ready-2", "ready", Ready: true));
        Accept(session, host, new("refresh-start", "start"));
        M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
        var world = (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        var participants = ((System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!).Cast<object>().ToArray();
        object Owner(int id) => participants.Single(item => (int)item.GetType().GetField("Id")!.GetValue(item)! == id);
        EntityId Country(int id) => (EntityId)Owner(id).GetType().GetField("Country")!.GetValue(Owner(id))!;
        var runtimes = (Dictionary<string, ConstructionLiveRuntime>)typeof(M3GameSession).GetField("_constructionRuntimes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        var colonyId = View().ColonyId; var runtime = runtimes[colonyId];
        if (mode == 2)
        {
            // Core fixture сокращает только работу head до одного дня; source conditions
            // и contributions сохраняются. Placement не является предметом этого сценария.
            Check(runtime.Installed.Values.All(item => item.DefinitionId != "building_foundry_1"), "Daily fixture initial foundry count");
            var startupMap = (IReadOnlyDictionary<string, M3StartupColony>)typeof(M3GameSession).GetField("_startupColonies", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
            var startup = startupMap[colonyId]; var template = ConstructionOrdinaryTemplate.Get("economic");
            var context = scenarioContent.Context(startup, template, runtime, M3ConstructionContent.FromBinding.ActingCountry(false));
            var facts = M3ConstructionContent.EconomyFacts(startup);
            var modifiers = scenarioContent.PresetModifiers(template, ConstructionKind.Building);
            var head = scenarioContent.Content.Quote("building_foundry_1", context, modifiers.Cost, modifiers.Speed, facts).Quote ?? throw new InvalidOperationException("Daily head quote unresolved");
            var tailResult = scenarioContent.Content.Quote("building_foundry_1", context, modifiers.Cost, modifiers.Speed, facts);
            var tail = tailResult.Quote ?? throw new InvalidOperationException("Daily tail quote unresolved: " + tailResult.Code);
            head = new(head.ContentProfileId, head.PolicyId, head.DefinitionId, head.Kind, 1, head.Costs, head.InfrastructureUpkeep, head.JobCapacity, head.Gates);
            world.SetBalance(Country(host.ParticipantId), "minerals", 10000);
            var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [host.ParticipantId] = world.GetBalances(Country(host.ParticipantId)) },
                sourceCatalog!.Resources.ToDictionary(pair => pair.Key, pair => new ConstructionStorage(pair.Value.StoragePolicy == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyStoragePolicy.UnlimitedReserve, pair.Value.SourceMaximum)));
            foreach (var (id, quote) in new[] { ("completion-head", head), ("condition-tail", tail) })
            {
                var target = runtime.Slots.Values.First(slot => slot.Kind == quote.Kind && !runtime.Installed.ContainsKey(slot.Id) && !runtime.Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending && order.SlotId == slot.Id));
                var paid = runtime.Enqueue("fixture-" + id, host.ParticipantId, runtime.Revision, id, target.Id, quote, bank);
                var staged = ConstructionLiveTransaction.Stage(paid, bank); Check(staged.Accepted, "Daily fixture payment: " + id + "/" + staged.Code);
                runtime = staged.Runtime!; bank = staged.Bank!;
            }
            foreach (var amount in bank.Accounts[host.ParticipantId]) world.SetBalance(Country(host.ParticipantId), amount.Key, amount.Value);
            runtimes[colonyId] = runtime; Owner(host.ParticipantId).GetType().GetField("ColonyRevision")!.SetValue(Owner(host.ParticipantId), runtime.Revision);
            var before = View(); var ack = Accept(session, host, new("completion-refresh-day", "stepDay")); var after = View();
            Check(after.Revision == before.Revision + 1 && after.Queue.Single(item => item.OrderId == "completion-head").Status == "completed"
                && after.Queue.Single(item => item.OrderId == "condition-tail") is { Status: "cancelled", CancellationReason: "condition-lost", CompletedWork: 0 },
                "Daily refresh не увидел completed installed graph до публикации");
            var snapshot = Json(after);
            Check(session.Execute(host.ParticipantId, host.ConnectionId, new("completion-refresh-day", "stepDay"), TimeSpan.Zero) == ack && Json(View()) == snapshot, "Daily replay повторил completion/refund");
        }
        else if (rollback)
        {
            // Нормальный adapter не создаёт такой заказ: заведомо Unknown recheck fixture
            // проверяет rollback после успешного первого Stage, без production test hook.
            var free = runtime.Slots.Values.First(slot => slot.Kind == ConstructionKind.Building && !runtime.Installed.ContainsKey(slot.Id));
            var quote = new ConstructionQuote("fixture", "fixture-unknown-recheck", "building_capital", ConstructionKind.Building, 10,
                new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), []);
            var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [host.ParticipantId] = new Dictionary<string, decimal>() }, new Dictionary<string, ConstructionStorage>());
            var injected = runtime.Enqueue("fixture-unknown", host.ParticipantId, runtime.Revision, "unknown-order", free.Id, quote, bank);
            Check(injected.Accepted, "Unknown fixture enqueue"); runtimes[colonyId] = injected.State;
            Owner(host.ParticipantId).GetType().GetField("ColonyRevision")!.SetValue(Owner(host.ParticipantId), injected.State.Revision);
            var target = View().Installed.Single(item => item.DefinitionId == "building_capital");
            var before = Json(View()); var hash = world.StateHash();
            Reject(session, host, new("refresh-rollback", "setBuildingEnabled", ColonyId: colonyId, ExpectedColonyRevision: View().Revision,
                DefinitionId: target.DefinitionId, SlotId: target.SlotId, Enabled: false), "condition-context-unresolved");
            Check(Json(View()) == before && world.StateHash() == hash && ReferenceEquals(runtimes[colonyId], injected.State),
                "Unknown после первого Stage опубликовал bank/runtime/economy/revision");
        }
        else
        {
            var slot = runtime.Slots.Values.First(item => item.Kind == ConstructionKind.Building && !runtime.Installed.ContainsKey(item.Id));
            var modifier = new ConstructionInstalled(slot.Id, "building_generator_districts_1", ConstructionKind.Building, new Dictionary<string, decimal>(), new Dictionary<string, decimal>());
            runtime = new(runtime.ColonyId, runtime.OwnerId, runtime.Slots.Values, runtime.Installed.Values.Append(modifier), runtime.Revision, runtime.StartupBuildingBySlot);
            var startups = (IReadOnlyDictionary<string, M3StartupColony>)typeof(M3GameSession).GetField("_startupColonies", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
            var startup = startups[colonyId];
            var typeCap = M3DistrictCapacity.Evaluate(fixture, startup, runtime, "district_generator", fixture.Context(startup, ConstructionOrdinaryTemplate.Get("economic"), runtime), M3ConstructionContent.EconomyFacts(startup)).Capacity!.Value;
            var needed = typeCap - runtime.Installed.Values.Count(item => item.DefinitionId == "district_generator");
            var empty = runtime.Slots.Values.Count(item => item.Kind == ConstructionKind.District && !runtime.Installed.ContainsKey(item.Id));
            // Изолируем type-pool от случайного global pool стартовой планеты: дополнительные
            // места — только fixture, не вывод о native вместимости или layout.
            var extras = Enumerable.Range(0, Math.Max(0, needed - empty)).Select(index => new ConstructionLiveSlot(colonyId + "/fixture-extra-district/" + index, ConstructionKind.District, null, null));
            runtimes[colonyId] = new(runtime.ColonyId, runtime.OwnerId, runtime.Slots.Values.Concat(extras), runtime.Installed.Values, runtime.Revision, runtime.StartupBuildingBySlot);
            world.SetBalance(Country(host.ParticipantId), "minerals", 10000);
            var index = 0;
            while (View().Catalog.Single(item => item.DefinitionId == "district_generator") is { Eligibility: "available", SlotIds.Count: > 0 } entry)
            {
                var before = View();
                Accept(session, host, new("refresh-reserve-" + index, "enqueueConstruction", ColonyId: colonyId, ExpectedColonyRevision: before.Revision,
                    DefinitionId: entry.DefinitionId, SlotId: entry.SlotIds[0], OrderId: "refresh-order-" + index++));
                Check(index < 20, "Fixture rural pool не исчерпывается");
            }
            var reserved = View(); Check(reserved.Queue.Count(item => item.Status == "pending") > 0, "Нет source reservations");
            world = (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
            var cap = sourceCatalog!.Resources["minerals"].SourceMaximum!.Value;
            world.SetBalance(Country(host.ParticipantId), "minerals", cap - 5);
            var otherBank = world.GetBalances(Country(second.ParticipantId))["minerals"];
            var command = new M3ClientCommand("refresh-disable-cap", "setBuildingEnabled", ColonyId: colonyId, ExpectedColonyRevision: reserved.Revision,
                DefinitionId: modifier.DefinitionId, SlotId: modifier.SlotId, Enabled: false);
            var ack = Accept(session, host, command); var after = View();
            var cancelled = after.Queue.Where(item => item.Status == "cancelled").ToArray();
            Check(cancelled.Length > 0 && cancelled.All(item => item.CancellationReason == "condition-lost")
                && after.Revision == reserved.Revision + 1 && after.Queue.All(item => item.CompletedWork == 0), "Session не отменил excess атомарно/добавил работу");
            var currentWorld = (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
            Check(currentWorld.GetBalances(Country(host.ParticipantId))["minerals"] == cap
                && currentWorld.GetBalances(Country(second.ParticipantId))["minerals"] == otherBank, "Session refund cap/payer");
            var snapshot = Json(after);
            Check(session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero) == ack && Json(View()) == snapshot,
                "Replay повторно применил refund или revision");
        }
    }
}

void OrdinaryBuildingEnabled()
{
    foreach (var preset in new[] { "economic", "scientific", "military" })
    {
        var session = NewSession(useSourceEconomy: true);
        var host = session.Join("Enabled fixture", "player", null, TimeSpan.Zero);
        var second = session.Join("Other fixture", "player", null, TimeSpan.Zero);
        var observer = session.Join("Read only fixture", "observer", null, TimeSpan.Zero);
        foreach (var player in new[] { host, second, observer }) Import(session, player, "enabled-import-" + player.ParticipantId);
        Accept(session, host, new("enabled-template-1", "selectEmpireTemplate", TemplateId: preset));
        Accept(session, second, new("enabled-template-2", "selectEmpireTemplate", TemplateId: "scientific"));
        Accept(session, host, new("enabled-ready-1", "ready", Ready: true)); Accept(session, second, new("enabled-ready-2", "ready", Ready: true));
        Accept(session, host, new("enabled-start", "start"));
        var colony = session.View(host.ParticipantId).Development!.Colonies.Single();
        var participants = ((System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!).Cast<object>().ToArray();
        var hostState = participants.Single(item => (int)item.GetType().GetField("Id")!.GetValue(item)! == host.ParticipantId);
        var planet = (EntityId)hostState.GetType().GetField("Planet")!.GetValue(hostState)!;
        var country = (EntityId)hostState.GetType().GetField("Country")!.GetValue(hostState)!;
        EconomyWorld World() => (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        var factory = colony.Installed.Single(item => item.DefinitionId == "building_factory_1");
        Check(factory.Enabled == true && factory.Toggle is { CanToggle: true, TargetEnabled: false, Gates.Count: 0 }
            && colony.Queue.Count == 0, "Source factory capability или чистый начальный lifecycle не опубликованы");
        var factoryUpkeep = World().GetBuildingUpkeep(planet);
        var factoryBalance = World().GetBalances(country);
        var factoryJobs = colony.Economy!.Jobs.ToDictionary(item => item.JobId, item => item.Capacity, StringComparer.Ordinal);
        var factoryOff = new M3ClientCommand("toggle-factory-off", "setBuildingEnabled", ColonyId: colony.ColonyId,
            ExpectedColonyRevision: colony.Revision, SlotId: factory.SlotId, DefinitionId: factory.DefinitionId, Enabled: false);
        Accept(session, host, factoryOff);
        var offColony = session.View(host.ParticipantId).Development!.Colonies.Single();
        var offFactory = offColony.Installed.Single(item => item.SlotId == factory.SlotId);
        Check(offColony.Revision == colony.Revision + 1 && offFactory is { DefinitionId: "building_factory_1", Enabled: false }
            && offFactory.JobCapacity.Count == 0 && offFactory.InfrastructureUpkeep.Count == 0
            && Json(offColony.Queue) == Json(colony.Queue), "Factory off не сохранил instance/slot или изменил очередь/effects раньше snapshot");
        foreach (var job in factory.JobCapacity)
            Check(offColony.Economy!.Jobs.Single(item => item.JobId == job.Key).Capacity == factoryJobs.GetValueOrDefault(job.Key) - job.Value,
                "Session factory off не убрал job capacity ровно один раз: " + job.Key);
        foreach (var upkeep in factory.InfrastructureUpkeep)
            Check(World().GetBuildingUpkeep(planet).GetValueOrDefault(upkeep.Key) == factoryUpkeep.GetValueOrDefault(upkeep.Key) - upkeep.Value,
                "Session factory off не обновил source upkeep: " + upkeep.Key);
        var factoryOn = new M3ClientCommand("toggle-factory-on", "setBuildingEnabled", ColonyId: offColony.ColonyId,
            ExpectedColonyRevision: offColony.Revision, SlotId: factory.SlotId, DefinitionId: factory.DefinitionId, Enabled: true);
        Accept(session, host, factoryOn);
        var onColony = session.View(host.ParticipantId).Development!.Colonies.Single();
        var onFactory = onColony.Installed.Single(item => item.SlotId == factory.SlotId);
        Check(onFactory is { DefinitionId: "building_factory_1", Enabled: true }
            && onFactory.JobCapacity.OrderBy(item => item.Key).SequenceEqual(factory.JobCapacity.OrderBy(item => item.Key))
            && onFactory.InfrastructureUpkeep.OrderBy(item => item.Key).SequenceEqual(factory.InfrastructureUpkeep.OrderBy(item => item.Key))
            && onColony.Economy!.Jobs.All(item => item.Capacity == factoryJobs.GetValueOrDefault(item.JobId))
            && World().GetBuildingUpkeep(planet).OrderBy(item => item.Key).SequenceEqual(factoryUpkeep.OrderBy(item => item.Key))
            && World().GetBalances(country).OrderBy(item => item.Key).SequenceEqual(factoryBalance.OrderBy(item => item.Key)),
            "Session factory on не восстановил authoritative state/economy либо toggle создал оплату");
        foreach (var building in colony.Installed.Where(item => item.Kind == "building"))
        {
            var current = session.View(host.ParticipantId).Development!.Colonies.Single();
            var currentBuilding = current.Installed.Single(item => item.SlotId == building.SlotId);
            if (building.DefinitionId == "building_factory_1") { continue; }
            Check(currentBuilding.Enabled == true && currentBuilding.Toggle is { CanToggle: false, TargetEnabled: false }, "Initial explicit no/Unknown capability не опубликован");
            var command = new M3ClientCommand("toggle-" + building.DefinitionId, "setBuildingEnabled", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, SlotId: currentBuilding.SlotId, DefinitionId: currentBuilding.DefinitionId, Enabled: false);
            var snapshot = Json(session.View(host.ParticipantId).Development!);
            var reason = building.Toggle!.Gates.Single().DependencyId;
            Reject(session, host, command, reason);
            Reject(session, second, command with { CommandId = "foreign-" + building.DefinitionId }, "OUT_OF_SCOPE");
            Reject(session, observer, command with { CommandId = "observer-" + building.DefinitionId }, "OUT_OF_SCOPE");
            Reject(session, host, command with { CommandId = "stale-" + building.DefinitionId, ExpectedColonyRevision = current.Revision + 1 }, "STALE_REVISION");
            Reject(session, host, command with { CommandId = "missing-" + building.DefinitionId, Enabled = null }, "INVALID_COMMAND");
            Check(Json(session.View(host.ParticipantId).Development!) == snapshot, "Rejected toggle изменил authoritative snapshot");
            Check(currentBuilding.Demolition is { CanDemolish: false }, "Unknown открыл Session demolition");
            var demolition = new M3ClientCommand("demolition-" + building.DefinitionId, "demolishBuilding", ColonyId: current.ColonyId,
                ExpectedColonyRevision: current.Revision, SlotId: currentBuilding.SlotId, DefinitionId: currentBuilding.DefinitionId, ConfirmDemolition: true);
            Reject(session, host, demolition, currentBuilding.Demolition!.Gates.Single().DependencyId);
            Reject(session, second, demolition with { CommandId = "demolition-foreign-" + building.DefinitionId }, "OUT_OF_SCOPE");
            Reject(session, observer, demolition with { CommandId = "demolition-observer-" + building.DefinitionId }, "OUT_OF_SCOPE");
            Reject(session, host, demolition with { CommandId = "demolition-stale-" + building.DefinitionId, ExpectedColonyRevision = current.Revision + 1 }, "STALE_REVISION");
            Reject(session, host, demolition with { CommandId = "demolition-missing-" + building.DefinitionId, ConfirmDemolition = null }, "INVALID_COMMAND");
            Reject(session, host, demolition with { CommandId = "demolition-unconfirmed-" + building.DefinitionId, ConfirmDemolition = false }, "DEMOLITION_CONFIRMATION_REQUIRED");
            Check(Json(session.View(host.ParticipantId).Development!) == snapshot, "Rejected demolition изменил authoritative snapshot");
        }
        Check(session.View(second.ParticipantId).Development!.Colonies.All(item => item.ColonyId != colony.ColonyId), "Enabled state утёк в другой POV");
    }
}

void OrdinaryZoneReplacement()
{
    foreach (var preset in new[] { "economic", "scientific", "military" })
    {
        var session = NewSession(useSourceEconomy: true);
        var host = session.Join("Replacement fixture", "player", null, TimeSpan.Zero);
        var second = session.Join("Other fixture", "player", null, TimeSpan.Zero);
        var observer = session.Join("Readonly fixture", "observer", null, TimeSpan.Zero);
        foreach (var player in new[] { host, second, observer }) Import(session, player, "replacement-import-" + player.ParticipantId);
        Accept(session, host, new("replacement-template-1", "selectEmpireTemplate", TemplateId: preset));
        Accept(session, second, new("replacement-template-2", "selectEmpireTemplate", TemplateId: "scientific"));
        Accept(session, host, new("replacement-ready-1", "ready", Ready: true)); Accept(session, second, new("replacement-ready-2", "ready", Ready: true));
        Accept(session, host, new("replacement-start", "start"));
        M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
        var initial = View();
        var preview = initial.Installed.Single(item => item.DefinitionId == "zone_industrial").ZoneReplacements!.Single(item => item.DefinitionId == "zone_foundry");
        Check(preview.CanEnqueue && preview.Quote is { DurationDays: > 0 } && preview.Quote.Costs.ContainsKey("minerals")
            && preview.RemovedBuildings.Count == 1 && preview.RemovedBuildings[0].DefinitionId == "building_factory_1", "Session потерял executable source preview/quote");
        var command = new M3ClientCommand("zone-replacement", "enqueueZoneReplacement", ColonyId: initial.ColonyId, ExpectedColonyRevision: initial.Revision,
            DefinitionId: preview.DefinitionId, SourceDefinitionId: preview.SourceDefinitionId, SlotId: preview.SlotId, ZoneInstanceId: preview.ZoneInstanceId,
            OrderId: "replacement-order", ZoneReplacementPreviewId: preview.PreviewId, ConfirmZoneRemoval: true);
        EconomyWorld World() => (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        var snapshot = Json(View()); var worldHash = World().StateHash();
        Reject(session, host, command with { CommandId = "unconfirmed", ConfirmZoneRemoval = false }, "ZONE_REMOVAL_CONFIRMATION_REQUIRED");
        Reject(session, host, command with { CommandId = "missing", ConfirmZoneRemoval = null }, "INVALID_COMMAND");
        Reject(session, host, command with { CommandId = "stale", ExpectedColonyRevision = initial.Revision + 1 }, "ZONE_REPLACEMENT_PREVIEW_CHANGED");
        Reject(session, host, command with { CommandId = "wrong-preview", ZoneReplacementPreviewId = "wrong" }, "ZONE_REPLACEMENT_PREVIEW_CHANGED");
        Reject(session, host, command with { CommandId = "extra", Enabled = false }, "INVALID_COMMAND");
        Reject(session, second, command with { CommandId = "foreign" }, "OUT_OF_SCOPE");
        Reject(session, observer, command with { CommandId = "observer" }, "OUT_OF_SCOPE");
        Check(Json(View()) == snapshot && World().StateHash() == worldHash, "Rejected replacement изменил Session graph/банк/revision");
        Check(session.View(second.ParticipantId).Development!.Colonies.All(item => item.ColonyId != initial.ColonyId), "Replacement preview утёк в чужой POV");
    }
}

void OrdinaryRuralZones()
{
    foreach (var preset in new[] { "economic", "scientific", "military" })
    {
        var session = NewSession(useSourceEconomy: true);
        var host = session.Join("Zone fixture", "player", null, TimeSpan.Zero);
        var second = session.Join("Second fixture", "player", null, TimeSpan.Zero);
        foreach (var player in new[] { host, second }) Import(session, player, "zone-import-" + player.ParticipantId);
        Accept(session, host, new("zone-template-1", "selectEmpireTemplate", TemplateId: preset));
        Accept(session, second, new("zone-template-2", "selectEmpireTemplate", TemplateId: "scientific"));
        Accept(session, host, new("zone-ready-1", "ready", Ready: true)); Accept(session, second, new("zone-ready-2", "ready", Ready: true));
        Accept(session, host, new("zone-start", "start"));
        M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
        var initial = View(); var zoneSlot = initial.Slots.Single(slot => slot.Kind == "zone" && slot.ZoneSlotDefinitionId == "slot_energy");
        Check(initial.Slots.Count(slot => slot.Kind == "zone" && slot.ZoneSlotDefinitionId is "slot_energy" or "slot_minerals" or "slot_food") == 3 && zoneSlot.RequirementLocalizationKeys!.SequenceEqual(["zone_unavailable_tech_power_hub_1"])
            && zoneSlot.Gates.Count == 0, "Source zone slots/requirements не дошли до Session DTO");
        var command = new M3ClientCommand("zone-build", "enqueueConstruction", ColonyId: initial.ColonyId, ExpectedColonyRevision: initial.Revision,
            DefinitionId: "zone_energy", SlotId: zoneSlot.SlotId, OrderId: "zone-order");
        Reject(session, host, command with { CommandId = "zone-native" }, "numerical-modifiers-unresolved");
        Check(View().Revision == initial.Revision && View().Queue.Count == 0, "Production Unknown изменил colony");
        Reject(session, second, command with { CommandId = "zone-foreign" }, "OUT_OF_SCOPE");
        Check(session.View(second.ParticipantId).Development!.Colonies.All(colony => colony.ColonyId != initial.ColonyId), "Zone graph утёк другому POV");
        var snapshot = Json(View());
        Reject(session, host, command, "numerical-modifiers-unresolved");
        Check(Json(View()) == snapshot, "Повтор отклонённого zone order изменил снимок");
    }
}

void OrdinaryRuralDistricts()
{
    foreach (var preset in new[] { "economic", "scientific", "military" })
    {
        var session = NewSession(useSourceEconomy: true);
        var host = session.Join("Rural unit", "player", null, TimeSpan.Zero);
        var second = session.Join("Second unit", "player", null, TimeSpan.Zero);
        foreach (var player in new[] { host, second }) Import(session, player, "rural-import-" + player.ParticipantId);
        Accept(session, host, new("rural-template-1", "selectEmpireTemplate", TemplateId: preset));
        Accept(session, second, new("rural-template-2", "selectEmpireTemplate", TemplateId: "scientific"));
        Accept(session, host, new("rural-ready-1", "ready", Ready: true)); Accept(session, second, new("rural-ready-2", "ready", Ready: true));
        Accept(session, host, new("rural-start", "start"));
        var world = (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        foreach (var participant in (System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!)
            if (participant!.GetType().GetField("Country")!.GetValue(participant) is EntityId country) world.SetBalance(country, "minerals", 10000);
        M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
        var commandNumber = 0;
        void Days(int count) { for (var index = 0; index < count; index++) Accept(session, host, new("rural-day-" + commandNumber++, "stepDay")); }
        Days(30);
        foreach (var type in new[] { "district_generator", "district_mining", "district_farming" })
        {
            var before = View(); var entry = before.Catalog.Single(item => item.DefinitionId == type);
            Check(entry is { Quote: not null, CanAfford: true } && entry.SlotIds.Count > 0, "Source rural quote unavailable: " + preset + "/" + type);
            var orderId = "session-" + type;
            var command = new M3ClientCommand("build-" + type, "enqueueConstruction", ColonyId: before.ColonyId, ExpectedColonyRevision: before.Revision,
                DefinitionId: type, SlotId: entry.SlotIds[0], OrderId: orderId);
            Accept(session, host, command);
            var pending = View();
            Check(pending.Economy!.Jobs.All(job => job.Capacity == before.Economy!.Jobs.Single(old => old.JobId == job.JobId).Capacity)
                && pending.StartupZones!.Count == before.StartupZones!.Count && pending.Slots.Count == before.Slots.Count
                && pending.DistrictTypePools!.Single(pool => pool.DefinitionId == type).Reserved == 1, "Pending rural дал ранние jobs/zone/slots либо потерял reservation");
            Check(Accept(session, host, command).Accepted && View().Queue.Count(order => order.OrderId == orderId) == 1, "Rural replay создал дубль");
            Reject(session, second, command with { CommandId = "foreign-" + type }, "OUT_OF_SCOPE");
            Days((int)Math.Ceiling(entry.Quote!.DurationDays));
            var completed = View();
            Check(completed.Queue.Single(order => order.OrderId == orderId).Status == "completed"
                && completed.DistrictTypePools!.Single(pool => pool.DefinitionId == type).Reserved == 0
                && completed.DistrictTypePools!.Single(pool => pool.DefinitionId == type).Installed == before.DistrictTypePools!.Single(pool => pool.DefinitionId == type).Installed + 1,
                "Rural completion не перевёл reservation в installed");
            foreach (var job in entry.Quote.JobCapacity)
                Check(completed.Economy!.Jobs.Single(item => item.JobId == job.Key).Capacity == before.Economy!.Jobs.Single(item => item.JobId == job.Key).Capacity + job.Value,
                    "Source rural job capacity не применена ровно один раз: " + job.Key);
            Days(30);
            foreach (var upkeep in entry.Quote.InfrastructureUpkeep)
                Check(View().Economy!.Resources.Single(item => item.ResourceId == upkeep.Key).DistrictUpkeep
                    == before.Economy!.Resources.Single(item => item.ResourceId == upkeep.Key).DistrictUpkeep + upkeep.Value,
                    "Rural district upkeep не попал в закрытый месяц ровно один раз");
        }
    }
}

void OrdinaryQueueReorder()
{
    var session = NewSession(useSourceEconomy: true);
    var host = session.Join("Queue unit", "player", null, TimeSpan.Zero);
    var second = session.Join("Second unit", "player", null, TimeSpan.Zero);
    var observer = session.Join("Observer unit", "observer", null, TimeSpan.Zero);
    foreach (var player in new[] { host, second, observer }) Import(session, player, "queue-import-" + player.ParticipantId);
    Accept(session, host, new("queue-template-1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("queue-template-2", "selectEmpireTemplate", TemplateId: "scientific"));
    Accept(session, host, new("queue-ready-1", "ready", Ready: true)); Accept(session, second, new("queue-ready-2", "ready", Ready: true));
    Accept(session, host, new("queue-start", "start"));
    // Явная unit fixture банка; не подменяет browser-приёмку естественных запасов.
    var world = (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    foreach (var participant in (System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!)
        if (participant!.GetType().GetField("Country")!.GetValue(participant) is EntityId country) world.SetBalance(country, "minerals", 10000);
    M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
    var initialPool = View().DistrictPool ?? throw new InvalidOperationException("Нет authoritative district pool.");
    var initialCapacity = View().Economy!.Jobs.ToDictionary(item => item.JobId, item => item.Capacity);
    foreach (var id in new[] { "a", "b", "c" })
    {
        var current = View(); var entry = current.Catalog.Single(item => item.DefinitionId == "district_city");
        Accept(session, host, new("queue-build-" + id, "enqueueConstruction", ColonyId: current.ColonyId, ExpectedColonyRevision: current.Revision,
            DefinitionId: entry.DefinitionId, SlotId: entry.SlotIds[0], OrderId: id));
    }
    Accept(session, host, new("queue-day-1", "stepDay"));
    var before = View();
    Check(before.DistrictPool!.PolicyId == "ordinary-district-slot-pool-v1" && before.DistrictPool.Installed == initialPool.Installed
        && before.DistrictPool.Reserved == initialPool.Reserved + 3 && before.DistrictPool.Free == initialPool.Free - 3
        && before.Economy!.Jobs.All(item => item.Capacity == initialCapacity[item.JobId]), "Pending районы изменили installed/effects или не зарезервировали общий пул.");
    Check(before.QueuePolicy == new M3ConstructionQueuePolicyView("ordinary-sequential-v1", 1, true), "Нет авторитетной ordinary queue policy.");
    var request = new M3ClientCommand("queue-swap", "reorderConstruction", ColonyId: before.ColonyId, ExpectedColonyRevision: before.Revision, OrderIds: ["a", "c", "b"]);
    Reject(session, second, request with { CommandId = "queue-foreign" }, "OUT_OF_SCOPE");
    Reject(session, observer, request with { CommandId = "queue-observer" }, "OUT_OF_SCOPE");
    Reject(session, host, request with { CommandId = "queue-stale", ExpectedColonyRevision = before.Revision - 1 }, "STALE_REVISION");
    Reject(session, host, request with { CommandId = "queue-head", OrderIds = ["b", "a", "c"] }, "active-order-locked");
    Reject(session, host, request with { CommandId = "queue-noop", OrderIds = ["a", "b", "c"] }, "queue-unchanged");
    Reject(session, host, request with { CommandId = "queue-member", OrderIds = ["a", "b"] }, "invalid-queue-membership");
    Reject(session, host, request with { CommandId = "queue-duplicate", OrderIds = ["a", "b", "b"] }, "INVALID_COMMAND");
    Accept(session, host, request);
    var changed = View();
    Check(changed.Revision == before.Revision + 1 && changed.Queue.Where(item => item.Status == "pending").Select(item => item.OrderId).SequenceEqual(["a", "c", "b"]), "Порядок/ревизия не изменены атомарно.");
    Check(changed.Queue.All(item => item.CompletedWork == before.Queue.Single(previous => previous.OrderId == item.OrderId).CompletedWork)
        && changed.Economy!.Resources.All(item => item.Stock == before.Economy!.Resources.Single(previous => previous.ResourceId == item.ResourceId).Stock), "Перестановка изменила работу/банк.");
    Accept(session, host, request);
    Check(View().Revision == changed.Revision, "Повтор перестановки изменил revision.");
    Reject(session, host, request with { OrderIds = ["a", "b", "c"] }, "ID_CONFLICT");
    Accept(session, host, new("queue-cancel-head", "cancelConstruction", ColonyId: changed.ColonyId, ExpectedColonyRevision: changed.Revision, OrderId: "a"));
    Check(View().Queue.Single(item => item.OrderId == "c").CompletedWork == 0, "Promotion начислил работу вне дневной фазы.");
    Accept(session, host, new("queue-day-2", "stepDay"));
    Check(View().Queue.Single(item => item.OrderId == "c").CompletedWork > 0 && View().Queue.Single(item => item.OrderId == "b").CompletedWork == 0, "Следующий день не использовал новый порядок.");
    var free = View().DistrictPool!.Free; string? finalSlot = null;
    Check(free > 0, "Unit fixture не оставляет места для проверки заполнения пула.");
    for (var index = 0; index < free; index++)
    {
        var current = View(); var entry = current.Catalog.Single(item => item.DefinitionId == "district_city"); finalSlot = entry.SlotIds[0];
        Accept(session, host, new("pool-build-" + index, "enqueueConstruction", ColonyId: current.ColonyId, ExpectedColonyRevision: current.Revision,
            DefinitionId: entry.DefinitionId, SlotId: finalSlot, OrderId: "pool-" + index));
    }
    var full = View();
    Check(full.DistrictPool!.Free == 0 && full.DistrictPool.Installed + full.DistrictPool.Reserved == full.DistrictPool.Capacity
        && full.Catalog.Single(item => item.DefinitionId == "district_city").SlotIds.Count == 0, "City catalog превысил общий пул/reservations.");
    Reject(session, host, new("pool-overflow", "enqueueConstruction", ColonyId: full.ColonyId, ExpectedColonyRevision: full.Revision,
        DefinitionId: "district_city", SlotId: finalSlot, OrderId: "pool-overflow"), "slot-unavailable");
    Check(View().Revision == full.Revision && View().Economy!.Resources.All(item => item.Stock == full.Economy!.Resources.Single(previous => previous.ResourceId == item.ResourceId).Stock), "Отказ на cap изменил revision/банк.");
    var releasedSlot = full.Queue.Single(item => item.OrderId == "pool-0").SlotId;
    Accept(session, host, new("pool-cancel", "cancelConstruction", ColonyId: full.ColonyId, ExpectedColonyRevision: full.Revision, OrderId: "pool-0"));
    var released = View();
    Check(released.DistrictPool!.Free == 1 && released.DistrictPool.Reserved == full.DistrictPool.Reserved - 1
        && released.Catalog.Single(item => item.DefinitionId == "district_city").SlotIds.SequenceEqual([releasedSlot]), "Cancel не освободил ровно одно место общего пула.");
    Accept(session, host, new("pool-reuse", "enqueueConstruction", ColonyId: released.ColonyId, ExpectedColonyRevision: released.Revision,
        DefinitionId: "district_city", SlotId: releasedSlot, OrderId: "pool-reuse"));
    Check(View().DistrictPool!.Free == 0, "Освобождённое место не удалось повторно зарезервировать.");
    // Явный unit-fixture переполнения проверяет consent/ACK, не естественный ход экономики.
    var hostOwner = ((System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!)
        .Cast<object>().Single(item => (int)item.GetType().GetField("Id")!.GetValue(item)! == host.ParticipantId);
    var hostCountry = (EntityId)hostOwner.GetType().GetField("Country")!.GetValue(hostOwner)!;
    EconomyWorld CurrentWorld() => (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    var storageCap = View().Economy!.Resources.Single(item => item.ResourceId == "minerals").SourceMaximum!.Value;
    CurrentWorld().SetBalance(hostCountry, "minerals", storageCap);
    var lossView = View(); var lossOrder = lossView.Queue.Single(item => item.OrderId == "pool-reuse");
    var lossCommand = new M3ClientCommand("refund-loss-confirm", "cancelConstruction", ColonyId: lossView.ColonyId,
        ExpectedColonyRevision: lossView.Revision, OrderId: lossOrder.OrderId, RefundPreviewId: lossOrder.RefundQuote!.PreviewId, ConfirmRefundLoss: true);
    Reject(session, host, lossCommand with { CommandId = "refund-loss-unconfirmed", ConfirmRefundLoss = false }, "REFUND_CONFIRMATION_REQUIRED");
    Reject(session, host, lossCommand with { CommandId = "refund-loss-revision", ExpectedColonyRevision = lossView.Revision - 1 }, "REFUND_PREVIEW_CHANGED");
    CurrentWorld().SetBalance(hostCountry, "minerals", storageCap - 1m);
    Reject(session, host, lossCommand with { CommandId = "refund-loss-stale" }, "REFUND_PREVIEW_CHANGED");
    Check(View().Revision == lossView.Revision && View().Queue.Single(item => item.OrderId == lossOrder.OrderId).Status == "pending"
        && View().Economy!.Resources.Single(item => item.ResourceId == "minerals").Stock == storageCap - 1m, "Отказ preview изменил банк/заказ/revision.");
    lossCommand = lossCommand with { RefundPreviewId = View().Queue.Single(item => item.OrderId == lossOrder.OrderId).RefundQuote!.PreviewId };
    var lossAck = Accept(session, host, lossCommand);
    Check(lossAck.RefundLosses!.Single().Value == lossOrder.PaidCosts["minerals"] - 1m && View().DistrictPool!.Free == 1
        && View().Economy!.Resources.Single(item => item.ResourceId == "minerals").Stock == storageCap, "ACK/банк не отражают фактическую потерю capped refund.");
    Check(Accept(session, host, lossCommand) == lossAck, "Replay потерял исходный ACK потерь.");
}

void UpgradeRuntime()
{
    // Явная unit fixture технологий и stock; это не ручной сценарий research acquisition.
    var tech = ConstructionOrdinaryTemplate.Get("economic").StartingTechnologies.ToHashSet(StringComparer.Ordinal);
    var session = NewSession(useSourceEconomy: true, researchedTechnologies: _ => tech);
    var host = session.Join("Upgrade unit", "player", null, TimeSpan.Zero);
    var second = session.Join("Second unit", "player", null, TimeSpan.Zero);
    var observer = session.Join("Observer unit", "observer", null, TimeSpan.Zero);
    foreach (var player in new[] { host, second, observer }) Import(session, player, "upgrade-import-" + player.ParticipantId);
    Accept(session, host, new("up-template-1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("up-template-2", "selectEmpireTemplate", TemplateId: "scientific"));
    Accept(session, host, new("up-observer", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Accept(session, host, new("up-ready-1", "ready", Ready: true)); Accept(session, second, new("up-ready-2", "ready", Ready: true));
    Accept(session, host, new("up-start", "start"));
    var initial = View();
    var capital = initial.Installed.Single(item => item.DefinitionId == "building_capital");
    var zone = initial.Slots.Single(item => item.SlotId == capital.SlotId).ZoneInstanceId!;
    var request = new M3ClientCommand("up-missing-tech", "enqueueUpgrade", ColonyId: initial.ColonyId, ExpectedColonyRevision: initial.Revision,
        DefinitionId: "building_major_capital", SourceDefinitionId: "building_capital", SlotId: capital.SlotId, ZoneInstanceId: zone, OrderId: "up-order");
    Reject(session, host, request, "not-eligible");
    tech.Add("tech_colonial_centralization");
    var unitWorld = (EconomyWorld)typeof(M3GameSession).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    foreach (var participant in (System.Collections.IEnumerable)typeof(M3GameSession).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!)
    {
        if (participant!.GetType().GetField("Country")!.GetValue(participant) is not EntityId country) continue;
        foreach (var resource in sourceEconomy!.Resources.Values.Where(resource => resource.StoragePolicy != "unspecified"))
            unitWorld.SetBalance(country, resource.Id, Math.Min(10000, resource.SourceMaximum ?? 10000));
    }
    var funded = View();
    tech.Add("tech_alloys_1"); // Только explicit unit tech fixture для composite source quote.
    // Quote-only source edge fixture. Exact installed binding проверяется настоящим startup capital ниже.
    var colonyMap = (IReadOnlyDictionary<string, M3StartupColony>)typeof(M3GameSession).GetField("_startupColonies", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    var runtimeMap = (Dictionary<string, ConstructionLiveRuntime>)typeof(M3GameSession).GetField("_constructionRuntimes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
    var economicTemplate = ConstructionOrdinaryTemplate.Get("economic");
    var sourceContext = sourceConstruction!.Context(colonyMap[funded.ColonyId], economicTemplate, runtimeMap[funded.ColonyId], M3ConstructionContent.FromBinding.ActingCountry(false), tech, sourceEconomy!.InitialPopulation);
    var sourceModifiers = sourceConstruction.PresetModifiers(economicTemplate, ConstructionKind.Building);
    var sourceFacts = sourceContext.SourceFacts!;
    ConstructionContext PopulationContext(decimal? amount) => new(sourceContext.OwnerExists, sourceContext.OwnerFacts, sourceContext.FromNomadic, sourceContext.PlanetClass,
        sourceContext.Modifiers, sourceContext.CarrierFlags, sourceContext.Technologies, sourceContext.BuildingCounts,
        new(sourceFacts.DistrictSet, sourceFacts.OwnerCountryType, sourceFacts.Civics, sourceFacts.Traditions, sourceFacts.MenacePerks, sourceFacts.Deposits,
            sourceFacts.PlanetFacts, sourceFacts.ValidCivics, sourceFacts.OwnerFlags, sourceFacts.Shelter, amount));
    Check(sourceConstruction.Content.Quote("building_major_capital", PopulationContext(2499), sourceModifiers.Cost, sourceModifiers.Speed,
        M3ConstructionContent.EconomyFacts(colonyMap[funded.ColonyId]), "building_capital").Code == "not-eligible", "Capital upgrade did not enforce source population >=2500.");
    Check(sourceConstruction.Content.Quote("building_major_capital", PopulationContext(null), sourceModifiers.Cost, sourceModifiers.Speed,
        M3ConstructionContent.EconomyFacts(colonyMap[funded.ColonyId]), "building_capital").Code == "eligibility-unresolved", "Missing sapient population was guessed.");
    var activeCountryFacts = M3ConstructionContent.EconomyFacts(colonyMap[funded.ColonyId]).ToDictionary(pair => pair.Key, pair => pair.Value);
    activeCountryFacts["owner:has_corporate_government"] = true;
    activeCountryFacts["owner:has_origin:origin_evolutionary_predators"] = true;
    activeCountryFacts["owner:is_mutation_authority"] = true;
    Check(sourceConstruction.Content.Quote("building_major_capital", sourceContext, sourceModifiers.Cost, sourceModifiers.Speed,
        activeCountryFacts, "building_capital").Code == "definition-economy-scope-unresolved", "Active unsupported country modifier was silently dropped.");
    var foundryUpgrade = sourceConstruction.Content.Quote("building_foundry_2", sourceContext, sourceModifiers.Cost, sourceModifiers.Speed,
        M3ConstructionContent.EconomyFacts(colonyMap[funded.ColonyId]), "building_foundry_1");
    Check(foundryUpgrade.Quote is not null && foundryUpgrade.Quote.Costs.GetValueOrDefault("minerals") == 510
        && foundryUpgrade.Quote.Costs.GetValueOrDefault("volatile_motes") == 85
        && foundryUpgrade.Quote.InfrastructureUpkeep.GetValueOrDefault("energy") == 5
        && foundryUpgrade.Quote.InfrastructureUpkeep.GetValueOrDefault("volatile_motes") == 2,
        "Source foundry upgrade потерял additive strategic cost/upkeep blocks.");
    var option = funded.Installed.Single(item => item.SlotId == capital.SlotId).Upgrades!.Single(item => item.DefinitionId == "building_major_capital");
    Check(option.Quote is not null && option.CanAfford == true && option.CanEnqueue && option.CommandType == "enqueueUpgrade",
        "Startup capital target quote is gated: " + string.Join(',', option.Gates.Select(gate => gate.DependencyId)));
    Reject(session, host, request with { CommandId = "up-stale", ExpectedColonyRevision = funded.Revision + 1 }, "STALE_REVISION");
    Reject(session, second, request with { CommandId = "up-foreign" }, "OUT_OF_SCOPE");
    Reject(session, observer, request with { CommandId = "up-observer-mutate" }, "OUT_OF_SCOPE");
    Reject(session, host, request with { CommandId = "up-forged-source", SourceDefinitionId = "building_colony_shelter" }, "upgrade-source-mismatch");
    Reject(session, host, request with { CommandId = "up-forged-zone", ZoneInstanceId = "forged-zone" }, "upgrade-source-mismatch");
    Reject(session, host, request with { CommandId = "up-wrong-edge", DefinitionId = "building_research_lab_2" }, "upgrade-edge-unavailable");
    var empty = funded.Slots.First(item => item.Kind == "building" && item.InstalledDefinitionId is null && item.ReservedByOrderId is null);
    Reject(session, host, request with { CommandId = "up-scratch", Type = "enqueueConstruction", SourceDefinitionId = null, ZoneInstanceId = null, SlotId = empty.SlotId }, "source-can-build-no");
    Accept(session, host, new("up-zero-politician", "setWorkforceLimitMin", ColonyId: funded.ColonyId, ExpectedColonyRevision: funded.Revision, Job: "politician"));
    var before = View();
    var enqueue = request with { CommandId = "up-enqueue", ExpectedColonyRevision = before.Revision };
    Accept(session, host, enqueue);
    var paid = View();
    Check(paid.Queue.Single(item => item.OrderId == "up-order").LocalizationKey == "building_major_capital"
        && paid.Installed.Single(item => item.SlotId == capital.SlotId).LocalizationKey == "building_capital", "Pending target/source localization keys перепутаны.");
    Check(option.Quote!.Costs.All(pair => Stock(paid, pair.Key) == Stock(before, pair.Key) - pair.Value), "Upgrade did not debit fixed composite price.");
    CheckOld(paid, before);
    Accept(session, host, enqueue);
    Check(View().Revision == paid.Revision, "Upgrade retry changed revision.");
    Reject(session, host, enqueue with { DefinitionId = "building_system_capital" }, "ID_CONFLICT");
    Accept(session, host, new("up-pending-day", "stepDay"));
    var pending = View(); CheckOld(pending, before);
    Check(pending.Queue.Single(item => item.OrderId == "up-order").CompletedWork > 0, "Pending upgrade made no daily work.");
    Accept(session, host, new("up-cancel", "cancelConstruction", ColonyId: pending.ColonyId, ExpectedColonyRevision: pending.Revision, OrderId: "up-order"));
    var cancelled = View(); CheckOld(cancelled, before);
    Check(option.Quote.Costs.All(pair => Stock(cancelled, pair.Key) == Stock(before, pair.Key)), "Cancel did not refund paid price.");
    Accept(session, host, request with { CommandId = "up-condition-enqueue", ExpectedColonyRevision = cancelled.Revision, OrderId = "up-condition" });
    tech.Remove("tech_colonial_centralization");
    Accept(session, host, new("up-condition-day", "stepDay"));
    var lost = View(); CheckOld(lost, before);
    Check(lost.Queue.Single(item => item.OrderId == "up-condition").Status == "cancelled"
        && option.Quote.Costs.All(pair => Stock(lost, pair.Key) == Stock(before, pair.Key)), "Pending tech loss did not cancel/refund.");
    tech.Add("tech_colonial_centralization");
    Accept(session, host, request with { CommandId = "up-complete-enqueue", ExpectedColonyRevision = lost.Revision, OrderId = "up-complete" });
    var days = (int)Math.Ceiling(option.Quote.DurationDays);
    for (var day = 1; day <= days; day++)
    {
        Accept(session, host, new("up-complete-day-" + day, "stepDay"));
        if (day == days - 1) CheckOld(View(), before);
    }
    var completed = View(); CheckTarget(completed);
    Check(completed.Queue.Single(item => item.OrderId == "up-complete").Status == "completed", "Upgrade did not complete on accepted daily boundary.");
    for (var day = 0; day < 30; day++) Accept(session, host, new("up-month-" + day, "stepDay"));
    var monthly = View(); CheckTarget(monthly);
    Check(monthly.Economy!.Resources.Single(item => item.ResourceId == "energy").BuildingUpkeep
        == monthly.Installed.Where(item => item.Kind == "building").Sum(item => item.InfrastructureUpkeep.GetValueOrDefault("energy")), "Month double-counted old capital upkeep.");
    var city = monthly.Catalog.Single(item => item.DefinitionId == "district_city");
    Accept(session, host, new("up-after-city", "enqueueConstruction", ColonyId: monthly.ColonyId, ExpectedColonyRevision: monthly.Revision, DefinitionId: city.DefinitionId, SlotId: city.SlotIds[0], OrderId: "up-city"));
    for (var day = 1; day <= (int)Math.Ceiling(city.Quote!.DurationDays); day++) Accept(session, host, new("up-city-day-" + day, "stepDay"));
    CheckTarget(View());

    M3DevelopmentColonyView View() => session.View(host.ParticipantId).Development!.Colonies.Single();
    static decimal Stock(M3DevelopmentColonyView colony, string resource) => colony.Economy!.Resources.Single(item => item.ResourceId == resource).Stock;
    void CheckOld(M3DevelopmentColonyView current, M3DevelopmentColonyView baseline)
    {
        Check(current.Installed.Count(item => item.SlotId == capital.SlotId) == 1 && current.Installed.Single(item => item.SlotId == capital.SlotId).DefinitionId == "building_capital"
            && current.Economy!.Jobs.Single(item => item.JobId == "politician").Capacity == baseline.Economy!.Jobs.Single(item => item.JobId == "politician").Capacity,
            "Pending/cancel replaced or duplicated old capital effects.");
    }
    void CheckTarget(M3DevelopmentColonyView current)
    {
        Check(current.Installed.Count(item => item.SlotId == capital.SlotId) == 1 && current.Installed.Single(item => item.SlotId == capital.SlotId).DefinitionId == "building_major_capital"
            && current.Slots.Single(item => item.SlotId == capital.SlotId).ZoneInstanceId == zone
            && current.Economy!.Jobs.Single(item => item.JobId == "politician").Capacity > before.Economy!.Jobs.Single(item => item.JobId == "politician").Capacity
            && current.Economy.Jobs.Single(item => item.JobId == "politician").Limit == 0,
            "Startup upgrade lost actual target/slot-zone or restored/doubled old capacity/zero limit.");
        var startupTarget = current.StartupBuildings!.Single(item => item.SlotInstanceId == capital.SlotId);
        Check(startupTarget.DefinitionId == "building_major_capital" && startupTarget.LocalizationKey == "building_major_capital"
            && startupTarget.DescriptionKey == "building_major_capital_desc", "Startup occurrence и source keys не обновились вместе с установленным upgrade.");
    }
}

JsonElement RunConstructionConsumer(M3DevelopmentColonyView colony, IReadOnlyList<M3DevelopmentGate> profileGates)
{
    var modulePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Stellaris.Server/wwwroot/m3-construction-controls.mjs"));
    if (!File.Exists(modulePath)) { throw new FileNotFoundException("Live construction consumer module is missing.", modulePath); }
    var moduleUrl = new Uri(modulePath).AbsoluteUri;
    var script = "import { constructionOperations } from " + JsonSerializer.Serialize(moduleUrl)
        + ";let input='';for await(const chunk of process.stdin) input+=chunk;console.log(JSON.stringify(constructionOperations(JSON.parse(input))));";
    var model = new
    {
        id = colony.ColonyId,
        gates = profileGates,
        development = new { revision = colony.Revision, gates = colony.Gates, catalog = colony.Catalog, slots = colony.Slots,
            installed = colony.Installed, queue = colony.Queue, features = colony.Features }
    };
    var payload = JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var start = new ProcessStartInfo("node") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add("--input-type=module"); start.ArgumentList.Add("-e"); start.ArgumentList.Add(script);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Node consumer process did not start.");
    process.StandardInput.WriteLine(payload); process.StandardInput.Close();
    var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
    if (!process.WaitForExit(15_000) || process.ExitCode != 0) { process.Kill(true); throw new InvalidOperationException("Node construction consumer failed: " + error); }
    return JsonDocument.Parse(output).RootElement.Clone();
}

void SourceEconomyRuntime()
{
    if (sourceEconomy is null || sourceStartup is null) { throw new Exception("Real source profile was not created."); }
    foreach (var templateId in new[] { "economic", "scientific", "military" })
    {
        var selected = sourceStartup.Select(templateId, 451);
        Check(selected.Plan.Gates.Count == 0 && selected.Homeworld.PlanetClass == "pc_continental", $"Supported source preset {templateId} retained an initial startup gate.");
        var world = sourceEconomy.CreateWorld();
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        sourceEconomy.InitializeColony(world, country, planet, templateId, selected.Plan);
        var installedCapacity = world.GetInstalledJobCapacity(planet) ?? throw new Exception("Source graph did not materialize capacity.");
        Check(installedCapacity.Count > 0, $"Source graph did not materialize job capacities for {templateId}.");
        Check(installedCapacity.GetValueOrDefault("physicist") == ExpectedInstalledCapacity(selected.Plan, "physicist"),
            $"District-scaled researcher capacity does not match the typed source applications for {templateId}.");
        Check(world.GetInstalledZoneBuildingSlotsAdd(planet) == ExpectedZoneBuildingSlots(selected.Plan),
            $"Static zone building slots were multiplied by attached district level for {templateId}.");
        Check(world.GetInstalledJobEconomyEffects(planet)?.OutputMultiplier >= 0.1m,
            $"Source capital employment production modifier was omitted for {templateId}.");
        if (templateId == "economic")
        {
            var city = selected.Plan.Districts.Single(item => item.DefinitionId == "district_city");
            Check(city.Level == 3, "Ordinary source graph no longer has initial city 1 plus two source levels.");
            var extraResearchCapacity = selected.Plan.Zones.Where(zone => zone.ZoneId == "zone_research_unity" && zone.DistrictId == "district_city")
                .Sum(_ => sourceCatalog!.Zones["zone_research_unity"].Terms.Where(term => term.Kind == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyTermKind.WorkforceCapacity
                    && term.TargetId == "physicist" && term.ApplicationScope == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyApplicationScope.PerAttachedDistrictLevel
                    && IsActiveSourceTerm(term)).Sum(term => term.Value!.Value));
            Check(extraResearchCapacity > 0, "No source-confirmed district-scaled researcher contribution was selected.");
            var levelFourPlan = WithCityLevel(selected.Plan, city.Level + 1);
            var levelFourWorld = sourceEconomy.CreateWorld();
            var levelFourCountry = levelFourWorld.CreateCountry();
            var levelFourPlanet = levelFourWorld.CreatePlanet(levelFourCountry);
            sourceEconomy.InitializeColony(levelFourWorld, levelFourCountry, levelFourPlanet, templateId, levelFourPlan);
            Check(levelFourWorld.GetInstalledJobCapacity(levelFourPlanet)!["physicist"] - installedCapacity["physicist"] == extraResearchCapacity,
                "Changing city level 3→4 did not add exactly one attached-district researcher contribution.");
            Check(levelFourWorld.GetInstalledZoneBuildingSlotsAdd(levelFourPlanet) == world.GetInstalledZoneBuildingSlotsAdd(planet),
                "Changing attached city level 3→4 changed static zone building slots.");
            world.AdvanceMonth(); levelFourWorld.AdvanceMonth();
            var levelThreeReport = world.GetPlanetReport(planet);
            var levelFourReport = levelFourWorld.GetPlanetReport(levelFourPlanet);
            Check(levelFourReport.ZoneUpkeep.SequenceEqual(levelThreeReport.ZoneUpkeep),
                "Changing attached city level 3→4 multiplied static zone upkeep.");
        }
    }
    var session = NewSession(useSourceEconomy: true);
    var host = session.Join("Хост", "player", null, TimeSpan.Zero);
    var second = session.Join("Игрок", "player", null, TimeSpan.Zero);
    var observer = session.Join("Наблюдатель", "observer", null, TimeSpan.Zero);
    Import(session, host, "se1"); Import(session, second, "se2"); Import(session, observer, "se3");
    Accept(session, host, new("template1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("template2", "selectEmpireTemplate", TemplateId: "scientific"));
    Accept(session, host, new("approvee", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Accept(session, host, new("readye1", "ready", Ready: true)); Accept(session, second, new("readye2", "ready", Ready: true));
    Accept(session, host, new("starte", "start"));
    var own = session.View(host.ParticipantId).Development!.Colonies.Single();
    var economy = own.Economy ?? throw new Exception("Source economy DTO missing from owner colony.");
    Check(economy.Population == 3200 && economy.BaseWorkforce == 3200 && economy.ReportMonth is null, "Source start pop/base workforce/report timing mismatch.");
    Check(sourceEconomy.StartingStocks.All(stock => economy.Resources.Single(resource => resource.ResourceId == stock.Key).Stock == stock.Value),
        "Live M3 stock did not match reached source start-resource branches.");
    Check(economy.Jobs.Any(job => job.JobId == "foundry" && job.Capacity > 0) && economy.Jobs.Any(job => job.JobId == "artisan" && job.Capacity > 0)
        && economy.Jobs.Any(job => job.JobId == "physicist" && job.Capacity > 0), "Source district/zone/building graph did not produce foundry/artisan/research capacity.");
    Check(economy.Jobs.Single(job => job.JobId == "technician").Assigned is not null && economy.Jobs.Single(job => job.JobId == "technician").BonusWorkforce is not null,
        "Pre-month current allocation must be an explicit live preview.");
    Check(economy.CoverageOmissions?.Any(item => item.JobId == "soldier" && item.DefinitionId.Length > 0 && item.TermKind.Length > 0 && item.Reason.Length > 0) == true,
        "Leader-dependent soldier omission was not kept as typed owner coverage metadata.");
    var ownerJson = JsonSerializer.Serialize(own);
    Check(ownerJson.Contains("DefinitionId", StringComparison.Ordinal) && ownerJson.Contains("JobId", StringComparison.Ordinal)
        && ownerJson.Contains("TermKind", StringComparison.Ordinal) && ownerJson.Contains("Reason", StringComparison.Ordinal)
        && ownerJson.Contains("soldier", StringComparison.Ordinal), "Serialized source economy DTO omitted typed coverage definition/job/term/reason.");
    Check(!ownerJson.Contains("01_start_of_game_effects.txt", StringComparison.Ordinal) && !ownerJson.Contains("on_all_capital_buildings.txt", StringComparison.Ordinal)
        && !ownerJson.Contains("Sha256", StringComparison.Ordinal) && !ownerJson.Contains("sourcePath", StringComparison.OrdinalIgnoreCase)
        && !ownerJson.Contains("sourceLine", StringComparison.OrdinalIgnoreCase) && !ownerJson.Contains("sourceExpression", StringComparison.OrdinalIgnoreCase)
        && !ownerJson.Contains("sourceHash", StringComparison.OrdinalIgnoreCase),
        "Serialized source economy DTO exposed internal provenance instead of sanitized typed coverage metadata: "
            + string.Join(",", new[] { "01_start_of_game_effects.txt", "on_all_capital_buildings.txt", "Sha256", "sourcePath", "sourceLine", "sourceExpression", "sourceHash" }
                .Where(token => ownerJson.Contains(token, StringComparison.OrdinalIgnoreCase))));
    var selectedJob = economy.Jobs.First(job => job.Capacity > 0);
    var targetRevision = own.Revision;
    Accept(session, host, new("limitdown", "adjustWorkforceLimit", ColonyId: own.ColonyId, ExpectedColonyRevision: targetRevision, Job: selectedJob.JobId, Count: -1));
    var adjusted = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!.Jobs.Single(job => job.JobId == selectedJob.JobId);
    Check(adjusted.Limit == selectedJob.Capacity * 0.95m, "WorkforceLimit step is not 5% of maximum capacity.");
    Reject(session, host, new("stale-limit", "setWorkforceLimitMin", ColonyId: own.ColonyId, ExpectedColonyRevision: targetRevision, Job: selectedJob.JobId), "STALE_REVISION");
    Reject(session, second, new("foreign-limit", "setWorkforceLimitMin", ColonyId: own.ColonyId, ExpectedColonyRevision: targetRevision + 1, Job: selectedJob.JobId), "OUT_OF_SCOPE");
    Reject(session, observer, new("observer-limit", "setWorkforceLimitMin", ColonyId: own.ColonyId, ExpectedColonyRevision: targetRevision + 1, Job: selectedJob.JobId), "OUT_OF_SCOPE");
    Accept(session, host, new("limitmin", "setWorkforceLimitMin", ColonyId: own.ColonyId, ExpectedColonyRevision: targetRevision + 1, Job: selectedJob.JobId));
    Accept(session, host, new("limitmax", "setWorkforceLimitMax", ColonyId: own.ColonyId, ExpectedColonyRevision: targetRevision + 2, Job: selectedJob.JobId));
    Reject(session, host, new("limitover", "setWorkforceLimit", ColonyId: own.ColonyId, ExpectedColonyRevision: targetRevision + 3, Job: selectedJob.JobId, Count: selectedJob.Capacity + 1), "INVALID_TARGET");
    for (var day = 1; day <= 30; day++) { Accept(session, host, new("sd" + day, "stepDay")); }
    var after = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!;
    Check(after.ReportMonth == 1 && after.Resources.Any(resource => resource.ResourceId == "unity" && resource.StoragePolicy == "unlimited-reserve" && resource.Stock > 50), "Successful monthly report/unlimited unity reserve missing.");
    Check(new[] { "physics_research", "society_research", "engineering_research" }.All(id => after.Resources.Any(resource => resource.ResourceId == id && resource.StoragePolicy == "unlimited-reserve" && resource.Stock > 0)),
        "Three uncapped science reserves did not accumulate from source jobs.");
    Check(after.Resources.Any(resource => resource.Produced > 0) && after.Resources.Any(resource => resource.JobUpkeep > 0)
        && after.Resources.Any(resource => resource.BuildingUpkeep > 0 || resource.DistrictUpkeep > 0 || resource.ZoneUpkeep > 0),
        "Source monthly report omitted production, job upkeep, or structure upkeep channels.");
    Check(after.Jobs.Where(job => job.Assigned is not null).All(job => job.Assigned <= job.Limit && job.Limit <= job.Capacity), "Published assignment exceeds current limit/capacity.");
    var previousReportMonth = after.ReportMonth;
    var previousFlows = after.Resources.ToDictionary(resource => resource.ResourceId, resource => resource.Produced, StringComparer.Ordinal);
    var occupiedJob = after.Jobs.First(job => job.Assigned > 0);
    var beforeLower = session.View(host.ParticipantId).Development!.Colonies.Single();
    Accept(session, host, new("post-month-min", "setWorkforceLimitMin", ColonyId: beforeLower.ColonyId, ExpectedColonyRevision: beforeLower.Revision, Job: occupiedJob.JobId));
    var afterLower = session.View(host.ParticipantId).Development!.Colonies.Single().Economy!;
    var loweredJob = afterLower.Jobs.Single(job => job.JobId == occupiedJob.JobId);
    Check(loweredJob.Limit == 0 && loweredJob.Assigned == 0 && loweredJob.BonusWorkforce == 0 && loweredJob.EffectiveWorkforce == 0,
        "Lowered WorkforceLimit did not update the current allocation preview.");
    Check(afterLower.ReportMonth == previousReportMonth && afterLower.Resources.All(resource => resource.Produced == previousFlows[resource.ResourceId]),
        "A limit change rewrote the last successful monthly report.");
    Accept(session, observer, new("general-econ", "observerView", TargetParticipantId: 0));
    Check(session.View(observer.ParticipantId).Development!.Colonies.Count == 0, "General observer saw private economy DTO.");
    Accept(session, observer, new("host-econ", "observerView", TargetParticipantId: host.ParticipantId));
    var observerOwnerEconomy = session.View(observer.ParticipantId).Development!.Colonies.Single().Economy;
    Check(observerOwnerEconomy?.ProfileId == after.ProfileId && observerOwnerEconomy.CoverageOmissions?.Any(item => item.JobId == "soldier") == true,
        "Selected owner POV cannot read owner economy and its coverage metadata.");
}

void StartupProjection(M3StartupCatalog catalog)
{
    var session = NewSession();
    var host = session.Join("Хост", "player", null, TimeSpan.Zero);
    var second = session.Join("Игрок", "player", null, TimeSpan.Zero);
    var observer = session.Join("Наблюдатель", "observer", null, TimeSpan.Zero);
    Import(session, host, "si1"); Import(session, second, "si2");
    Import(session, observer, "si3");
    Accept(session, host, new("select1", "selectEmpireTemplate", TemplateId: "economic"));
    Accept(session, second, new("select2", "selectEmpireTemplate", TemplateId: "military"));
    Accept(session, host, new("approve1", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Accept(session, host, new("ready1", "ready", Ready: true)); Accept(session, second, new("ready2", "ready", Ready: true));
    Accept(session, host, new("start1", "start"));
    Check(session.View(host.ParticipantId).Development!.Gates.Count > 0,
        "Legacy startup without live source/economy/asset closure lost its dependency gates.");
    var own = session.View(host.ParticipantId).Development!.Colonies.Single();
    Check(own.HomeworldInitializerId is not null && own.PlanetClass == "pc_continental" && own.PlanetSize is >= 18 and <= 21, "Source homeworld was not installed in player session state.");
    Check(own.StartupDistricts!.Any(item => item.DefinitionId == "district_city" && item.Level == 3) && own.StartupDistricts!.Any(item => item.DefinitionId == "district_farming") && own.StartupBuildings!.Any(item => item.DefinitionId == "building_capital"), "Actual source default starting district plus ordered effects/buildings missing from player projection.");
    Check(!own.Gates.Any(gate => gate.DependencyId.Contains("rural", StringComparison.OrdinalIgnoreCase) || gate.DependencyId.Contains("origin_default", StringComparison.Ordinal)), "Supported ordinary preset retained a rural/origin source gate.");
    Check(own.StartupZones is { Count: > 0 } && own.StartupFeatures is { Count: > 0 } && own.StartupDistrictCaps is { Count: > 0 }, "Live M3 DTO omitted explicit source zone/feature/cap instances.");
    Check(own.StartupFeatures!.Where(item => item.HiddenByBlockerInstanceId is not null).All(item => own.StartupFeatures!.Any(blocker => blocker.InstanceId == item.HiddenByBlockerInstanceId && blocker.HiddenFeatureInstanceId == item.InstanceId)), "Live M3 DTO broke blocker/hidden-feature instance links.");
    Check(own.StartupBuildings!.SelectMany(item => item.CompatibleZoneInstanceIds ?? Array.Empty<string>()).All(id => own.StartupZones!.Any(zone => zone.InstanceId == id)), "Live M3 building compatible-zone IDs do not reference source zone instances.");
    Check(session.View(second.ParticipantId).Development!.Colonies.Single().ColonyId != own.ColonyId, "A player received a foreign startup colony graph.");
    Accept(session, observer, new("general", "observerView", TargetParticipantId: 0));
    Check(session.View(observer.ParticipantId).Development!.Colonies.Count == 0, "General observer saw a private startup graph.");
    Accept(session, observer, new("ownerpov", "observerView", TargetParticipantId: host.ParticipantId));
    Check(session.View(observer.ParticipantId).Development!.Colonies.Single().ColonyId == own.ColonyId, "Selected observer-player POV could not read owner graph.");
}

decimal ExpectedInstalledCapacity(StartupBuildingPlan plan, string jobId)
{
    var definitions = sourceCatalog ?? throw new Exception("Source catalog was not imported.");
    decimal total = 0;
    foreach (var district in plan.Districts)
    {
        if (!definitions.Districts.TryGetValue(district.DefinitionId, out var definition)) { continue; }
        total += definition.Terms.Where(term => term.Kind == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyTermKind.WorkforceCapacity
                && term.TargetId == jobId && IsActiveSourceTerm(term))
            .Sum(term => term.Value!.Value * district.Level);
    }
    foreach (var zone in plan.Zones)
    {
        if (!definitions.Zones.TryGetValue(zone.ZoneId, out var definition)) { continue; }
        var district = plan.Districts.Single(item => item.DefinitionId == zone.DistrictId);
        total += definition.Terms.Where(term => term.Kind == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyTermKind.WorkforceCapacity
                && term.TargetId == jobId && IsActiveSourceTerm(term))
            .Sum(term => term.Value!.Value * (term.ApplicationScope == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyApplicationScope.PerAttachedDistrictLevel ? district.Level : 1));
    }
    foreach (var building in plan.Buildings)
    {
        if (!definitions.Buildings.TryGetValue(building.DefinitionId, out var definition)) { continue; }
        total += definition.Terms.Where(term => term.Kind == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyTermKind.WorkforceCapacity
                && term.TargetId == jobId && IsActiveSourceTerm(term))
            .Sum(term => term.Value!.Value);
    }
    return total;
}

decimal ExpectedZoneBuildingSlots(StartupBuildingPlan plan)
{
    var definitions = sourceCatalog ?? throw new Exception("Source catalog was not imported.");
    return plan.Zones.Where(zone => definitions.Zones.ContainsKey(zone.ZoneId))
        .Select(zone => definitions.Zones[zone.ZoneId].Terms.Where(term => term.Kind == Stellaris.Core.Content.PlanetEconomy.PlanetEconomyTermKind.PlanetModifier
            && term.TargetId == "zone_building_slots_add" && IsActiveSourceTerm(term)).Sum(term => term.Value!.Value))
        .Sum();
}

bool IsActiveSourceTerm(Stellaris.Core.Content.PlanetEconomy.PlanetEconomyTerm term) =>
    term.ConditionDisposition is Stellaris.Core.Content.PlanetEconomy.PlanetEconomyConditionDisposition.Unconditional
        or Stellaris.Core.Content.PlanetEconomy.PlanetEconomyConditionDisposition.ActiveInOrdinaryContext;

StartupBuildingPlan WithCityLevel(StartupBuildingPlan plan, int level)
{
    var districts = plan.Districts.Select(item => item.DefinitionId == "district_city" ? item with { Level = level } : item).ToArray();
    var constructor = typeof(StartupBuildingPlan).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
    return (StartupBuildingPlan)constructor.Invoke([plan.Context, plan.FinalPlanetSize, districts, plan.Slots, plan.Zones, plan.Buildings,
        plan.Sources, plan.Gates, plan.Features, plan.DistrictCaps, plan.ZoneRecurringEconomyProofs]);
}

(M3GameSession Session, M3JoinGrant Host, M3JoinGrant Second, M3JoinGrant Observer) Running(decimal jobUpkeep = 0)
{
    var session = NewSession(jobUpkeep);
    var host = session.Join("Хост", "player", null, TimeSpan.Zero);
    var second = session.Join("Игрок", "player", null, TimeSpan.Zero);
    var observer = session.Join("Наблюдатель", "observer", null, TimeSpan.Zero);
    Accept(session, host, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Import(session, host, "import1");
    Import(session, second, "import2");
    Accept(session, host, new("r1", "ready", Ready: true));
    Accept(session, second, new("r2", "ready", Ready: true));
    Accept(session, host, new("start", "start"));
    return (session, host, second, observer);
}

void Import(M3GameSession session, M3JoinGrant grant, string id)
{
    Accept(session, grant, new(id, "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest));
}

void Lobby()
{
    var session = NewSession();
    var host = session.Join("Хост", "player", null, TimeSpan.Zero);
    var observer = session.Join("Наблюдатель", "observer", null, TimeSpan.Zero);
    var second = session.Join("Игрок", "player", null, TimeSpan.Zero);
    Check(session.View(observer.ParticipantId).Planets.Count == 0 && session.View(observer.ParticipantId).Spatial.Systems.Count == 0, "Pending не видит мир.");
    Reject(session, host, new("r", "ready", Ready: true), "ASSETS_NOT_READY");
    Reject(session, host, new("wrong", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: new string('0', 64)), "PROFILE_MISMATCH");
    Import(session, host, "a");
    Import(session, second, "b");
    Accept(session, host, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Accept(session, host, new("r1", "ready", Ready: true));
    Accept(session, second, new("r2", "ready", Ready: true));
    Accept(session, host, new("start", "start"));
    var own = session.View(host.ParticipantId);
    var other = session.View(second.ParticipantId);
    Check(own.Planets.Single().Id == 1 && other.Planets.Single().Id == 4, "Реальные planet IDs 1/4.");
    Check(own.Spatial.Colonies.Single(item => item.OwnerParticipantId == host.ParticipantId).EconomyPlanetId == 1, "Ссылка host colony.");
    Check(other.Spatial.Colonies.Single(item => item.OwnerParticipantId == second.ParticipantId).EconomyPlanetId == 4, "Ссылка second colony.");
    Check(own.Spatial.Colonies.Single(item => item.OwnerParticipantId == second.ParticipantId).EconomyPlanetId == null, "Чужая экономика скрыта.");
    Check(session.View(observer.ParticipantId).Planets.Count == 2 && session.View(observer.ParticipantId).Spatial.Systems.Count == 16, "Наблюдатель видит полный мир.");
}

void LobbyMembership()
{
    var session = NewSession();
    var host = session.Join("Host", "player", null, TimeSpan.Zero);
    var second = session.Join("Second", "player", null, TimeSpan.Zero);
    var observer = session.Join("Observer", "observer", null, TimeSpan.Zero);
    Accept(session, host, new("approve-observer", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Import(session, host, "host-assets");
    Import(session, second, "second-assets");
    Import(session, observer, "observer-assets");
    Accept(session, observer, new("observer-pov", "observerView", TargetParticipantId: second.ParticipantId));
    Accept(session, host, new("host-ready", "ready", Ready: true));
    Accept(session, second, new("second-ready", "ready", Ready: true));
    Check(session.View(host.ParticipantId).Participants.Where(item => item.Role == "player").All(item => item.Ready), "Ready votes applied before membership changes.");
    Reject(session, second, new("non-host-kick", "kickParticipant", TargetParticipantId: observer.ParticipantId), "OUT_OF_SCOPE");
    Reject(session, host, new("self-kick", "kickParticipant", TargetParticipantId: host.ParticipantId), "INVALID_TARGET");
    Reject(session, host, new("unknown-kick", "kickParticipant", TargetParticipantId: 999), "INVALID_TARGET");
    Accept(session, host, new("kick-observer", "kickParticipant", TargetParticipantId: observer.ParticipantId));
    session.Disconnect(observer.ParticipantId, observer.ConnectionId, TimeSpan.Zero);
    Reject(session, observer, new("removed-observer-command", "observerView", TargetParticipantId: 0), "OUT_OF_SCOPE");
    Throws<InvalidOperationException>(() => session.Join("Observer return", "observer", observer.Token, TimeSpan.Zero));
    Check(session.View(host.ParticipantId).Participants.Where(item => item.Role == "player").All(item => item.Ready), "Observer removal leaves player votes intact.");

    session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
    session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
    Reject(session, host, new("stale-host-command", "kickParticipant", TargetParticipantId: second.ParticipantId), "OUT_OF_SCOPE");
    Throws<InvalidOperationException>(() => session.Join("Old host", "player", host.Token, TimeSpan.Zero));
    Check(session.View(second.ParticipantId).Participants.Single(item => item.Id == second.ParticipantId).IsHost, "Host exit removes the slot and assigns the connected player.");
    Check(session.View(second.ParticipantId).Participants.All(item => !item.Ready), "Player departure resets readiness.");

    var replacement = session.Join("Replacement", "player", null, TimeSpan.Zero);
    Check(replacement.ParticipantId > observer.ParticipantId && session.View(second.ParticipantId).Participants.Single(item => item.Id == second.ParticipantId).IsHost, "New player gets a monotonic ID without replacing the current host.");
    Import(session, second, "second-assets-again");
    Import(session, replacement, "replacement-assets");
    Accept(session, second, new("second-ready-again", "ready", Ready: true));
    Accept(session, replacement, new("replacement-ready", "ready", Ready: true));
    var pov = session.Join("Observer again", "observer", null, TimeSpan.Zero);
    Accept(session, second, new("approve-again", "approveObserver", TargetParticipantId: pov.ParticipantId));
    Import(session, pov, "observer-assets-again");
    Accept(session, pov, new("pov-to-removal", "observerView", TargetParticipantId: replacement.ParticipantId));
    Reject(session, replacement, new("non-host-kick-player", "kickParticipant", TargetParticipantId: second.ParticipantId), "OUT_OF_SCOPE");
    Reject(session, second, new("kick-self-after-approve", "kickParticipant", TargetParticipantId: second.ParticipantId), "INVALID_TARGET");
    Accept(session, second, new("kick-player", "kickParticipant", TargetParticipantId: replacement.ParticipantId));
    Check(session.View(pov.ParticipantId).ViewContext == new M3ViewContext("general", null), "Removed POV owner clears observer selection.");
    Check(session.View(second.ParticipantId).Participants.All(item => item.Id != replacement.ParticipantId) &&
        !session.View(second.ParticipantId).Participants.Any(item => item.Role == "player" && item.Ready), "Player kick removes the slot and invalidates all votes.");
    session.Disconnect(replacement.ParticipantId, replacement.ConnectionId, TimeSpan.Zero);
    Reject(session, replacement, new("stale-kicked-command", "ready", Ready: true), "OUT_OF_SCOPE");
    Throws<InvalidOperationException>(() => session.Join("Kicked return", "player", replacement.Token, TimeSpan.Zero));

    var next = session.Join("Next", "player", null, TimeSpan.Zero);
    Check(next.ParticipantId > replacement.ParticipantId, "ID is never reused after kick.");
    session.Disconnect(second.ParticipantId, second.ConnectionId, TimeSpan.Zero);
    Check(session.View(next.ParticipantId).Participants.Single(item => item.Id == next.ParticipantId).IsHost, "Host handoff selects the connected player with lowest ID.");
    session.Disconnect(next.ParticipantId, next.ConnectionId, TimeSpan.Zero);
    var last = session.Join("Last", "player", null, TimeSpan.Zero);
    Check(session.View(last.ParticipantId).Participants.Single(item => item.Id == last.ParticipantId).IsHost, "With no players, the next player becomes host.");
}

void Reconnect()
{
    var (session, host, _, _) = Running();
    var imported = new M3ClientCommand("import1", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest);
    var original = session.Execute(host.ParticipantId, host.ConnectionId, imported, TimeSpan.Zero);
    session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
    var next = session.Join("ignored", "player", host.Token, TimeSpan.Zero);
    Check(next.ConnectionId != host.ConnectionId && next.Token == host.Token, "Новая connection identity.");
    var before = session.View(host.ParticipantId).Sequence;
    session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
    Reject(session, host, imported, "OUT_OF_SCOPE");
    Check(session.Execute(next.ParticipantId, next.ConnectionId, imported, TimeSpan.Zero) == original, "Replay исходного ACK.");
    Check(!session.View(next.ParticipantId).Participants.Single(p => p.Id == next.ParticipantId).AssetReady && session.View(next.ParticipantId).Sequence == before, "Replay не подтверждает новый импорт.");
    Reject(session, next, new("step-no", "stepDay"), "ASSETS_NOT_READY");
    Accept(session, next, new("pause-no-assets", "pause", Paused: true));
    Import(session, next, "new-import");
    Accept(session, next, new("step", "stepDay"));
}

void ObserverPerspectives()
{
    var lobby = NewSession();
    var lobbyHost = lobby.Join("Host", "player", null, TimeSpan.Zero);
    var pending = lobby.Join("Observer", "observer", null, TimeSpan.Zero);
    Check(lobby.View(pending.ParticipantId).ViewContext == new M3ViewContext("pending", null), "Pending context.");
    using (var pendingJson = JsonDocument.Parse(Json(lobby.View(pending.ParticipantId))))
    {
        Check(pendingJson.RootElement.GetProperty("viewContext").GetProperty("participantId").ValueKind == JsonValueKind.Null, "Pending participantId is explicit null.");
    }
    Import(lobby, pending, "pending-import");
    Reject(lobby, pending, new("pending-view", "observerView", TargetParticipantId: lobbyHost.ParticipantId), "OUT_OF_SCOPE");
    Check(lobby.View(pending.ParticipantId).Planets.Count == 0 && lobby.View(pending.ParticipantId).Spatial.Systems.Count == 0, "Pending observer gains no data.");
    Accept(lobby, lobbyHost, new("approve", "approveObserver", TargetParticipantId: pending.ParticipantId));
    Check(lobby.View(pending.ParticipantId).ViewContext == new M3ViewContext("general", null), "Approved observer defaults to general.");
    Accept(lobby, pending, new("lobby-view", "observerView", TargetParticipantId: lobbyHost.ParticipantId));
    Check(Json(lobby.View(pending.ParticipantId).Planets) == Json(lobby.View(lobbyHost.ParticipantId).Planets), "Lobby perspective uses player economy.");

    var (session, host, second, observer) = Running();
    Check(session.View(host.ParticipantId).ViewContext == new M3ViewContext("player", host.ParticipantId), "Player context identifies actual player.");
    Reject(session, observer, new("not-imported", "observerView", TargetParticipantId: host.ParticipantId), "ASSETS_NOT_READY");
    Import(session, observer, "observer-import");
    Accept(session, host, new("move-first", "moveFleetToSystem", FleetId: "fleet:1", TargetSystemId: "S16"));
    Accept(session, second, new("jobs-second", "setJobs", PlanetId: 4, Job: "miner", Count: 5));
    var playerSpatial = Json(session.View(host.ParticipantId).Spatial);
    var playerEconomy = Json(session.View(host.ParticipantId).Planets);
    var firstCommand = new M3ClientCommand("view-first", "observerView", TargetParticipantId: host.ParticipantId);
    var firstAck = Accept(session, observer, firstCommand);
    var first = session.View(observer.ParticipantId);
    Check(first.You == observer.ParticipantId && first.Participants.Single(p => p.Id == first.You).Role == "observer", "Display identity does not replace observer authority.");
    Check(first.ViewContext == new M3ViewContext("player", host.ParticipantId), "Authoritative player perspective.");
    Check(Json(first.Spatial) == playerSpatial && Json(first.Planets) == playerEconomy, "Observer POV exactly matches player spatial/economy projection.");
    Check(first.Spatial.Fleets.Single(f => f.OwnerParticipantId == second.ParticipantId).Movement == null, "Foreign movement remains private.");
    Check(first.Spatial.Colonies.Single(c => c.OwnerParticipantId == second.ParticipantId).EconomyPlanetId == null, "Foreign economy links remain private.");
    Check(Json(session.View(host.ParticipantId).Spatial) == playerSpatial && Json(session.View(host.ParticipantId).Planets) == playerEconomy, "Other recipients retain their data.");
    foreach (var target in new[] { -1, 999, observer.ParticipantId })
    {
        var sequence = first.Sequence;
        Reject(session, observer, new("invalid-" + target, "observerView", TargetParticipantId: target), "OUT_OF_SCOPE");
        Check(session.View(observer.ParticipantId).ViewContext == first.ViewContext && session.View(observer.ParticipantId).Sequence == sequence, "Rejected target preserves context/sequence.");
    }
    Reject(session, observer, new("missing-view", "observerView"), "INVALID_COMMAND");
    Reject(session, observer, new("null-view", "observerView", TargetParticipantId: null), "INVALID_COMMAND");
    Reject(session, observer, new("extra-view", "observerView", TargetParticipantId: 0, Ready: true), "INVALID_COMMAND");
    Reject(session, host, new("player-view", "observerView", TargetParticipantId: second.ParticipantId), "OUT_OF_SCOPE");
    Reject(session, observer, new("observer-jobs", "setJobs", PlanetId: 1, Job: "miner", Count: 6), "OUT_OF_SCOPE");
    Reject(session, observer, new("observer-move", "moveFleetToSystem", FleetId: "fleet:1", TargetSystemId: "S16"), "OUT_OF_SCOPE");
    Reject(session, observer, new("observer-pause", "pause", Paused: false), "OUT_OF_SCOPE");
    Accept(session, observer, new("view-second", "observerView", TargetParticipantId: second.ParticipantId));
    Check(Json(session.View(observer.ParticipantId).Spatial) == Json(session.View(second.ParticipantId).Spatial) && Json(session.View(observer.ParticipantId).Planets) == Json(session.View(second.ParticipantId).Planets), "Second POV uses its distinct projection/economy.");
    var beforeReplay = session.View(observer.ParticipantId);
    Check(session.Execute(observer.ParticipantId, observer.ConnectionId, firstCommand, TimeSpan.Zero) == firstAck, "Duplicate returns original ACK.");
    Check(session.View(observer.ParticipantId).ViewContext == beforeReplay.ViewContext && session.View(observer.ParticipantId).Sequence == beforeReplay.Sequence, "Replay cannot restore an older POV.");
    Reject(session, observer, firstCommand with { TargetParticipantId = second.ParticipantId }, "ID_CONFLICT");
    session.Disconnect(second.ParticipantId, second.ConnectionId, TimeSpan.Zero);
    Accept(session, observer, new("offline-player", "observerView", TargetParticipantId: second.ParticipantId));
    session.Disconnect(observer.ParticipantId, observer.ConnectionId, TimeSpan.Zero);
    var returned = session.Join("Ignored", "observer", observer.Token, TimeSpan.Zero);
    Check(session.View(returned.ParticipantId).ViewContext == new M3ViewContext("player", second.ParticipantId), "POV persists across live reconnect.");
    Reject(session, observer, new("old-connection", "observerView", TargetParticipantId: 0), "OUT_OF_SCOPE");
    Reject(session, returned, new("fresh-assets-required", "observerView", TargetParticipantId: 0), "ASSETS_NOT_READY");
    Check(session.Execute(returned.ParticipantId, returned.ConnectionId, firstCommand, TimeSpan.Zero) == firstAck && session.View(returned.ParticipantId).ViewContext.ParticipantId == second.ParticipantId, "Reconnect replay preserves current POV and original ACK.");
    Import(session, returned, "reimport");
    Accept(session, host, new("unpause-view-test", "pause", Paused: false));
    Accept(session, returned, new("general", "observerView", TargetParticipantId: 0));
    var general = session.View(returned.ParticipantId);
    Check(general.ViewContext == new M3ViewContext("general", null) && general.Planets.Count == 2 && !general.Paused && general.Tick == first.Tick, "General view restores overview without changing simulation.");
    using (var generalJson = JsonDocument.Parse(Json(general)))
    {
        Check(generalJson.RootElement.GetProperty("viewContext").GetProperty("participantId").ValueKind == JsonValueKind.Null, "General participantId is explicit null.");
    }

    // Reuse the session's permitted-system filter; do not emulate fog in the client.
    typeof(M3GameSession).GetField("_systems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(session, new HashSet<string>(["S01"], StringComparer.Ordinal));
    Accept(session, returned, new("filtered-first", "observerView", TargetParticipantId: host.ParticipantId));
    var filtered = session.View(returned.ParticipantId);
    Check(Json(filtered.Spatial) == Json(session.View(host.ParticipantId).Spatial) && filtered.Spatial.Systems.Count == 1, "POV shares permitted-system filtering with the actual player.");
    Check(filtered.Spatial.Fleets.All(f => f.Movement == null), "Movement with hidden destination is omitted in POV.");
    Check(!Json(filtered.Spatial).Contains("S16", StringComparison.Ordinal), "Filtered POV does not disclose hidden destination IDs.");
    using var json = JsonDocument.Parse(Json(filtered));
    Check(json.RootElement.GetProperty("viewContext").GetProperty("participantId").GetInt32() == host.ParticipantId && json.RootElement.GetProperty("you").GetInt32() == observer.ParticipantId, "Wire JSON separates display and control identity.");
}

void Commands()
{
    var (session, host, second, observer) = Running();
    Reject(session, host, new("legacy", "step"), "INVALID_COMMAND");
    Reject(session, host, new("unknown", "unrecognized"), "INVALID_COMMAND");
    Reject(session, host, new("extra", "stepDay", X: 1), "INVALID_COMMAND");
    Reject(session, host, new("missing", "moveFleetToPoint", FleetId: "fleet:1"), "INVALID_COMMAND");
    var before = session.View(host.ParticipantId);
    foreach (var fleet in new[] { "fleet:2", "absent" })
    {
        Reject(session, host, new("bad-" + fleet, "stopFleet", FleetId: fleet), "OUT_OF_SCOPE");
    }
    Reject(session, host, new("unknown-system", "moveFleetToSystem", FleetId: "fleet:1", TargetSystemId: "secret"), "OUT_OF_SCOPE");
    Reject(session, host, new("nan", "moveFleetToPoint", FleetId: "fleet:1", SystemId: "S01", X: double.NaN, Z: 0), "INVALID_TARGET");
    Reject(session, host, new("outside", "moveFleetToPoint", FleetId: "fleet:1", SystemId: "S01", X: 1000, Z: 0), "INVALID_TARGET");
    Check(session.View(host.ParticipantId).Sequence == before.Sequence, "Отказы не изменяют мир.");
    var move = new M3ClientCommand("move", "moveFleetToPoint", FleetId: "fleet:1", SystemId: "S01", X: 0, Z: 0);
    var accepted = Accept(session, host, move);
    Check(accepted.AppliedTick == 0 && accepted.CommandSequence != null && accepted.StateSequence == session.View(host.ParticipantId).Sequence, "ACK привязан к состоянию.");
    var state = Json(session.View(host.ParticipantId).Spatial);
    Check(session.Execute(host.ParticipantId, host.ConnectionId, move, TimeSpan.Zero) == accepted && Json(session.View(host.ParticipantId).Spatial) == state, "Повтор движения идемпотентен.");
    Reject(session, host, move with { Z = 1 }, "ID_CONFLICT");
    Import(session, observer, "observer-import");
    Reject(session, observer, new("observer-move", "stopFleet", FleetId: "fleet:1"), "OUT_OF_SCOPE");
    Reject(session, second, new("foreign-jobs", "setJobs", PlanetId: 1, Job: "miner", Count: 0), "OUT_OF_SCOPE");
}

void Calendar()
{
    var (session, host, _, _) = Running();
    Check(session.View(host.ParticipantId).Date == new M3DateView(2200, 1, 1), "day0.");
    for (var day = 1; day <= 360; day++)
    {
        var command = new M3ClientCommand("d" + day, "stepDay");
        var result = Accept(session, host, command);
        var view = session.View(host.ParticipantId);
        Check(view.Tick == day && view.ElapsedDay == day && view.Month == day / 30, "Дневной/месячный счётчик.");
        Check(session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero) == result && session.View(host.ParticipantId).Sequence == view.Sequence, "Повтор шага не мутирует.");
        if (day == 29) Check(view.Date == new M3DateView(2200, 1, 30) && view.Planets[0].ReportMonth == null, "day29.");
        if (day == 30) Check(view.Date == new M3DateView(2200, 2, 1) && view.Planets[0].ReportMonth == 1, "day30.");
        if (day == 359) Check(view.Date == new M3DateView(2200, 12, 30), "day359.");
        if (day == 360) Check(view.Date == new M3DateView(2201, 1, 1) && view.Planets[0].ReportMonth == 12, "day360.");
    }
}

void Clock()
{
    var (session, host, _, _) = Running();
    session.Tick(TimeSpan.FromSeconds(10));
    Check(session.View(host.ParticipantId).Tick == 0, "Пауза не двигает.");
    Throws<ArgumentOutOfRangeException>(() => session.Tick(TimeSpan.FromSeconds(9)));
    Accept(session, host, new("unpause", "pause", Paused: false), 10);
    session.Tick(TimeSpan.FromSeconds(10.9));
    Check(session.View(host.ParticipantId).Tick == 0, "Deadline впереди.");
    session.Tick(TimeSpan.FromSeconds(30));
    Check(session.View(host.ParticipantId).Tick == 1, "Нет catch-up.");
    session.Tick(TimeSpan.FromSeconds(30));
    Check(session.View(host.ParticipantId).Tick == 1, "Deadline сброшен от текущего now.");
    Accept(session, host, new("speed", "speed", Speed: 4), 30);
    session.Tick(TimeSpan.FromSeconds(30.24));
    Check(session.View(host.ParticipantId).Tick == 1, "4 дня/сек ещё рано.");
    session.Tick(TimeSpan.FromSeconds(30.25));
    Check(session.View(host.ParticipantId).Tick == 2, "4 дня/сек наступил.");
    Accept(session, host, new("pause", "pause", Paused: true), 31);
    Accept(session, host, new("step", "stepDay"), 31);
    session.Tick(TimeSpan.FromSeconds(100));
    Check(session.View(host.ParticipantId).Tick == 3, "step и paused timer.");
}

void Disconnection()
{
    var (session, host, second, _) = Running();
    Reject(session, host, new("kick-running", "kickParticipant", TargetParticipantId: second.ParticipantId), "INVALID_PHASE");
    Accept(session, host, new("move", "moveFleetToPoint", FleetId: "fleet:1", SystemId: "S01", X: 0, Z: 0));
    Accept(session, host, new("unpause", "pause", Paused: false));
    session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
    session.Tick(TimeSpan.FromSeconds(59));
    var first = session.View(second.ParticipantId);
    Check(first.Tick == 1 && !first.Paused && first.Participants.Single(p => p.Id == host.ParticipantId).IsHost && !first.Participants.Single(p => p.Id == host.ParticipantId).Afk, "Host disconnect не меняет права/паузу.");
    session.Tick(TimeSpan.FromSeconds(60));
    Check(session.View(second.ParticipantId).Participants.Single(p => p.Id == host.ParticipantId).Afk, "AFK60.");
    Check(((M3SystemLocationView)session.View(second.ParticipantId).Spatial.Fleets.Single(f => f.OwnerParticipantId == host.ParticipantId).Location).Position.Z > -100, "Приказ выполняется offline.");
    var returned = session.Join("host", "player", host.Token, TimeSpan.FromSeconds(60));
    Accept(session, returned, new("transfer", "transferHost", TargetParticipantId: second.ParticipantId), 60);
    Accept(session, second, new("pause", "pause", Paused: true), 60);
    Check(session.View(second.ParticipantId).Participants.Single(p => p.Id == second.ParticipantId).IsHost, "Передача прав без импорта.");
}

void AtomicFailure()
{
    var (session, host, second, observer) = Running(1000);
    Accept(session, host, new("safe-first", "setJobs", PlanetId: 1, Job: "technician", Count: 0));
    for (var day = 1; day <= 29; day++) Accept(session, host, new("d" + day, "stepDay"));
    Accept(session, host, new("move", "moveFleetToPoint", FleetId: "fleet:1", SystemId: "S01", X: 0, Z: 0));
    var before = session.View(observer.ParticipantId);
    var command = new M3ClientCommand("fail-day", "stepDay");
    Reject(session, host, command, "INVALID_PHASE");
    var after = session.View(observer.ParticipantId);
    Check(after.ElapsedDay == 29 && after.Tick == 29 && after.Month == 0 && after.Paused && after.Sequence == before.Sequence + 1 && after.LastError != null, "Day30 отказ атомарен.");
    Check(Json(before.Spatial) == Json(after.Spatial) && Json(before.Planets) == Json(after.Planets), "Позиции, приказы, балансы и reports не изменились.");
    var sequence = after.Sequence;
    Reject(session, host, command, "INVALID_PHASE");
    Check(session.View(host.ParticipantId).Sequence == sequence, "Отказ кешируется.");
    Accept(session, second, new("repair", "setJobs", PlanetId: 4, Job: "technician", Count: 0));
    Accept(session, host, new("retry", "stepDay"));
    var repaired = session.View(observer.ParticipantId);
    Check(repaired.Tick == 30 && repaired.Month == 1 && repaired.LastError == null && repaired.Planets.All(p => p.ReportMonth == 1), "Новый шаг после исправления выполняется один раз.");
    Check(((M3SystemLocationView)repaired.Spatial.Fleets[0].Location).Position.Z > -100, "Ранее принятый приказ сохраняется после ошибки.");
}

void AtomicSuccess()
{
    var (session, host, _, observer) = Running();
    for (var day = 1; day <= 29; day++) Accept(session, host, new("d" + day, "stepDay"));
    Accept(session, host, new("move", "moveFleetToPoint", FleetId: "fleet:1", SystemId: "S01", X: 0, Z: 0));
    var before = session.View(observer.ParticipantId);
    Accept(session, host, new("day30", "stepDay"));
    var after = session.View(observer.ParticipantId);
    Check(after.Tick == 30 && after.Month == 1 && after.Planets.All(p => p.ReportMonth == 1), "Обе экономики опубликованы.");
    Check(Json(before.Spatial) != Json(after.Spatial) && after.LastError == null && after.LastTickMilliseconds >= 0, "Движение и календарь опубликованы совместно.");
}

void Economy()
{
    var session = NewSession(1);
    var host = session.Join("Хост", "player", null, TimeSpan.Zero);
    var second = session.Join("Игрок", "player", null, TimeSpan.Zero);
    Import(session, host, "a"); Import(session, second, "b");
    Accept(session, host, new("cfg", "configure", Traits: ["trait_ingenious"], Buildings: ["building_energy_grid"]));
    Reject(session, host, new("duplicate", "configure", Traits: ["trait_ingenious", "trait_ingenious"], Buildings: []), "INVALID_COMMAND");
    Accept(session, host, new("r1", "ready", Ready: true)); Accept(session, second, new("r2", "ready", Ready: true)); Accept(session, host, new("s", "start"));
    for (var day = 1; day <= 30; day++) Accept(session, host, new("d" + day, "stepDay"));
    var report = session.View(host.ParticipantId).Planets.Single().Report!;
    // Синтетический Add worker распространяется также на miner/farmer: ещё 2 energy.
    Check(report.Allocated["technician"].BonusWorkforce == 30 && report.Produced["energy"] == 16.95m && report.JobUpkeep["energy"] == 2.3m && report.BuildingUpkeep["energy"] == 2 && report.Upkeep["energy"] == 4.3m, "Workforce, add и отдельный building upkeep.");
    Accept(session, host, new("jobs", "setJobs", PlanetId: 1, Job: "technician", Count: 0.5m));
    Reject(session, host, new("jobsbad", "setJobs", PlanetId: 1, Job: "technician", Count: 0.3m), "INVALID_TARGET");
    Reject(session, host, new("cfg-running", "configure", Traits: [], Buildings: []), "INVALID_PHASE");
}

void Hidden()
{
    var world = SpaceWorld.CreateFixture(1, 2);
    var all = world.Systems.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
    var bodies = world.Bodies.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
    var ids = new Dictionary<int, int> { [1] = 1, [2] = 4 };
    world = world.MoveToSystem(1, "fleet:1", "S03", all);
    var visible = new HashSet<string>(["S01", "S02"], StringComparer.Ordinal);
    var view = M3Projection.Project(world, 1, false, visible, bodies, ids);
    Check(view.Hyperlanes.All(l => visible.Contains(l.FromSystemId) && visible.Contains(l.ToSystemId)) && view.Fleets.Single().Movement == null, "Скрытая route/target скрывает движение, edges только с видимыми ends.");
    var bodyHidden = bodies.Where(id => id != "star:S01" && id != "planet:S01").ToHashSet(StringComparer.Ordinal);
    view = M3Projection.Project(world, 1, false, visible, bodyHidden, ids);
    Check(view.Systems.Single(s => s.Id == "S01").StarBodyId == null && view.Colonies.Count == 0, "Нет dangling body/star/colony.");
    while (world.Fleets[0].Phase != FleetPhase.InTransit) world = world.AdvanceDay(world.ElapsedDay, _ => all);
    world = world.MoveToSystem(1, "fleet:1", "S16", all);
    view = M3Projection.Project(world, 1, false, visible, bodies, ids);
    Check(view.Fleets.Single().Movement == null, "Скрытая pending destination скрывает весь movement.");
    view = M3Projection.Project(world, 1, false, new HashSet<string>(["S01"]), bodies, ids);
    Check(view.Fleets.Count == 0, "Скрытый transit endpoint исключает флот.");
}

void Unions()
{
    var world = SpaceWorld.CreateFixture(1, 2);
    var all = world.Systems.Select(s => s.Id).ToHashSet();
    var bodies = world.Bodies.Select(b => b.Id).ToHashSet();
    var ids = new Dictionary<int, int> { [1] = 1, [2] = 4 };
    M3SpatialView Project() => M3Projection.Project(world, 1, false, all, bodies, ids);
    world = world.MoveToPoint(1, "fleet:1", "S01", new(0, 0), all);
    using (var json = JsonDocument.Parse(Json(Project())))
    {
        var own = json.RootElement.GetProperty("fleets")[0];
        Check(own.GetProperty("location").GetProperty("kind").GetString() == "system" && own.GetProperty("movement").GetProperty("target").GetProperty("kind").GetString() == "point", "System location/point target.");
        Check(!json.RootElement.GetProperty("fleets")[1].TryGetProperty("movement", out _) && !json.RootElement.GetProperty("colonies")[1].TryGetProperty("economyPlanetId", out _), "Чужие приватные поля отсутствуют.");
        Check(json.RootElement.GetProperty("bodies")[0].TryGetProperty("localPosition", out _) && !json.RootElement.GetProperty("bodies")[0].TryGetProperty("position", out _), "localPosition.");
    }
    world = world.MoveToSystem(1, "fleet:1", "S02", all);
    while (world.Fleets[0].Phase != FleetPhase.InTransit) world = world.AdvanceDay(world.ElapsedDay, _ => all);
    world = world.Stop(1, "fleet:1", all);
    using (var json = JsonDocument.Parse(Json(Project())))
    {
        var fleet = json.RootElement.GetProperty("fleets")[0];
        Check(fleet.GetProperty("location").GetProperty("kind").GetString() == "transit" && fleet.GetProperty("location").GetProperty("fromSystemId").GetString() == "S01", "Transit location.");
        Check(fleet.GetProperty("movement").GetProperty("target").GetProperty("kind").GetString() == "system" && fleet.GetProperty("movement").GetProperty("pendingAction").GetProperty("kind").GetString() == "stop", "System target/pending stop.");
    }
    world = world.MoveToSystem(1, "fleet:1", "S16", all);
    using (var json = JsonDocument.Parse(Json(Project())))
    {
        var pending = json.RootElement.GetProperty("fleets")[0].GetProperty("movement").GetProperty("pendingAction");
        Check(pending.GetProperty("kind").GetString() == "destination" && pending.GetProperty("systemId").GetString() == "S16", "Pending destination.");
    }
    var (session, host, _, _) = Running();
    var serialized = Json(session.View(host.ParticipantId));
    Check(!serialized.Contains(host.Token) && !serialized.Contains(host.ConnectionId) && !serialized.Contains("completedMonths") && !serialized.Contains("synthetic"), "Snapshot без secrets/внутренних metadata.");
}

void Immutable()
{
    var (session, host, _, _) = Running();
    var before = session.View(host.ParticipantId);
    Throws<NotSupportedException>(() => ((IList<M3SystemView>)before.Spatial.Systems).Clear());
    Throws<NotSupportedException>(() => ((IDictionary<string, decimal>)before.Planets[0].Jobs)["miner"] = 5);
    Accept(session, host, new("jobs", "setJobs", PlanetId: 1, Job: "miner", Count: 5));
    Check(before.Planets[0].Jobs["miner"] == 2 && session.View(host.ParticipantId).Planets[0].Jobs["miner"] == 5, "Snapshot независим.");
}

void JoinErrors()
{
    var session = NewSession();
    Throws<ArgumentException>(() => session.Join("\n", "player", null, TimeSpan.Zero));
    Throws<ArgumentException>(() => session.Join("x", "bad", null, TimeSpan.Zero));
    Throws<InvalidOperationException>(() => session.Join("x", "player", "bad", TimeSpan.Zero));
    var host = session.Join("h", "player", null, TimeSpan.Zero);
    Throws<InvalidOperationException>(() => session.Join("h", "player", host.Token, TimeSpan.Zero));
    var observer = session.Join("o", "observer", null, TimeSpan.Zero);
    Throws<InvalidOperationException>(() => session.Join("o2", "observer", null, TimeSpan.Zero));
    Accept(session, host, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
    Import(session, observer, "observer");
    Reject(session, observer, new("ready", "ready", Ready: true), "OUT_OF_SCOPE");
    var second = session.Join("s", "player", null, TimeSpan.Zero);
    var third = session.Join("third", "player", null, TimeSpan.Zero);
    Check(session.View(third.ParticipantId).Participants.Count(p => p.Role == "player") == 3, "Третий игрок принят в лобби.");
    Accept(session, host, new("transfer", "transferHost", TargetParticipantId: second.ParticipantId));
    Reject(session, host, new("transfer-again", "transferHost", TargetParticipantId: second.ParticipantId), "OUT_OF_SCOPE");
    session.Disconnect(observer.ParticipantId, observer.ConnectionId, TimeSpan.Zero);
    Throws<InvalidOperationException>(() => session.Join("o", "observer", observer.Token, TimeSpan.Zero));
    var returned = session.Join("o", "observer", null, TimeSpan.Zero);
    Accept(session, second, new("approve-returned-observer", "approveObserver", TargetParticipantId: returned.ParticipantId));
    Check(session.View(returned.ParticipantId).Participants.Single(p => p.Id == returned.ParticipantId).Role == "observer", "Новый observer одобрен после удаления старого места.");
}

void CacheLimit()
{
    var session = NewSession();
    var host = session.Join("h", "player", null, TimeSpan.Zero);
    for (var i = 0; i < 10000; i++) Reject(session, host, new("r" + i, "unknown"), "INVALID_COMMAND");
    Reject(session, host, new("overflow", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), "RATE_LIMITED");
    Reject(session, host, new("r0", "unknown"), "INVALID_COMMAND");
    Reject(session, host, new("r0", "stepDay"), "ID_CONFLICT");
}

static IReadOnlyDictionary<string, decimal> Amounts(params (string Id, decimal Value)[] amounts) => amounts.ToDictionary(a => a.Id, a => a.Value);
static string Json(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
static M3CommandResult Accept(M3GameSession session, M3JoinGrant grant, M3ClientCommand command, double seconds = 0)
{
    var result = session.Execute(grant.ParticipantId, grant.ConnectionId, command, TimeSpan.FromSeconds(seconds));
    Check(result.Accepted, command.Type + ": " + result.Code + ": " + result.Message);
    return result;
}
static void Reject(M3GameSession session, M3JoinGrant grant, M3ClientCommand command, string code)
{
    var result = session.Execute(grant.ParticipantId, grant.ConnectionId, command, TimeSpan.Zero);
    Check(!result.Accepted && result.Code == code && result.CommandSequence == null, command.Type + ": ожидался " + code + ", получен " + result.Code);
}
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Ожидалось исключение " + typeof(T).Name);
}
