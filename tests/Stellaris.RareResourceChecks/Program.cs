using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;
using Stellaris.Core.Research;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
Dictionary<string, decimal> Amounts(params (string, decimal)[] values) => values.ToDictionary(x => x.Item1, x => x.Item2);
var rare = new[] { "volatile_motes", "exotic_gases", "rare_crystals", "sr_zro", "sr_dark_matter", "sr_living_metal", "nanites" };
ResourceDefinition[] Resources() => new[] { new ResourceDefinition("minerals", 15000), new ResourceDefinition("alloys", 15000), new ResourceDefinition("energy", 15000) }
    .Concat(rare.Select(id => new ResourceDefinition(id, id == "nanites" ? null : 15000, id == "nanites" ? ResourceStoragePolicy.Unspecified : ResourceStoragePolicy.SourceCapped))).ToArray();
CategoryDefinition[] Categories() => [new("planet_jobs", null), new("planet_jobs_specialist", "planet_jobs"), new("planet_metallurgists", "planet_jobs_specialist"),
    new("child_metallurgists", "planet_metallurgists"), new("planet_artisans", "planet_jobs_specialist"), new("runtime-foundry", null), new("runtime-child", null)];
EconomyWorld World(bool naniteProducer = false) => new(Resources(),
    [new("foundry", "runtime-foundry", 100, naniteProducer ? Amounts(("nanites", 1)) : Amounts(("alloys", 3)), Amounts(("minerals", 6)), "planet_metallurgists"),
     new("child", "runtime-child", 100, Amounts(("alloys", 3)), Amounts(("minerals", 6)), "child_metallurgists")], Categories());

// Unit fixtures не являются выдачей запасов/технологий в игровом сценарии.
var world = World(); var country = world.CreateCountry(); var planet = world.CreatePlanet(country);
world.AddGroup(planet, 200, 1); world.SetJobs(planet, "foundry", 1); world.SetJobs(planet, "child", 1);
world.SetBalance(country, "minerals", 1000); world.SetJobBonusWorkforceMultiplier(planet, "foundry", .5m);
Modifier[] modifiers = [new("planet_metallurgists", "exotic_gases", "produces", .1m, 1), new("planet_metallurgists", "minerals", "upkeep", .5m, 0), new("planet_jobs", "alloys", "produces", 999, .2m), new("runtime-foundry", "alloys", "produces", 1, .4m), new("runtime-foundry", "minerals", "upkeep", 1, .4m)];
world.SetModifiers(planet, modifiers);
var report = world.PreviewPlanetReport(planet);
Check(report.Produced["exotic_gases"] == .3m, "Add должен действовать только на точную source category; Mult не создаёт канал у child.");
Check(report.JobUpkeep["minerals"] == 21.75m, "Effective WF масштабирует synthetic tech и source upkeep; source-category Add не наследуется child.");
Check(report.Produced["alloys"] == 13.2m, "Exact synthetic tech Add/Mult и native ancestor Mult должны примениться ровно один раз, ancestor Add не наследуется.");
world.SetModifiers(planet, modifiers);
Check(world.PreviewPlanetReport(planet).Produced["alloys"] == 13.2m, "Refresh накопил tech/source modifiers.");
world.SetInstalledJobEconomyEffects(planet, new(.3m, 0));
Check(world.PreviewPlanetReport(planet).Produced["exotic_gases"] == .345m && world.PreviewPlanetReport(planet).JobUpkeep["minerals"] == 21.75m, "Output multiplier повторён или попал в upkeep.");
world.SetInstalledJobEconomyEffects(planet, new(0, 0));
world.AdvanceMonth(); Check(world.GetBalance(country, "nanites") == 0 && world.GetBalance(country, "exotic_gases") == .3m, "Unknown-cap zero flow остановил обычный месяц.");
Check(!world.GetResourceCapacities().ContainsKey("nanites") && world.GetStoragePolicy("nanites") == ResourceStoragePolicy.Unspecified, "Nanite cap подменён числом.");
world.SetBalance(country, "nanites", 0);
try { world.SetBalance(country, "nanites", 1); throw new Exception("Setter обошёл credit gate."); }
catch (ResourceStorageUnresolvedException e) { Check(e.ResourceId == "nanites", "Typed setter diagnostic."); }
var blocked = World(true); var blockedCountry = blocked.CreateCountry(); var blockedPlanet = blocked.CreatePlanet(blockedCountry);
blocked.SetBalance(blockedCountry, "minerals", 100); blocked.AddGroup(blockedPlanet, 100, 1); blocked.SetJobs(blockedPlanet, "foundry", 1);
var before = blocked.StateHash();
try { blocked.AdvanceMonth(); throw new Exception("Positive nanites credit должен отказать."); }
catch (ResourceStorageUnresolvedException) { Check(blocked.StateHash() == before, "Частичный month settlement при unknown cap."); }
Check(blocked.Fork().StateHash() == before, "Fork потерял unknown policy/source category.");
var blockedAccounts = new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = blocked.GetBalances(blockedCountry) };
var blockedLedger = new ConstructionLedger(new("blocked", ConstructionAuthorityMode.Individualist, false, null, null), 1, blockedAccounts, ["slot"]);
var blockedAggregate = new ConstructionAggregate(blocked, new Dictionary<int, EntityId> { [1] = blockedCountry }, [new("blocked", 1, blockedPlanet)], [blockedLedger]);
var blockedMonth = blockedAggregate.Advance("month", 0, "phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, []);
Check(!blockedMonth.Accepted && blockedMonth.Code == "resource-storage-unresolved:nanites" && ReferenceEquals(blockedMonth.State, blockedAggregate) && blockedMonth.State.EconomyHash() == blockedAggregate.EconomyHash(), "Aggregate потерял typed diagnostic или атомарность.");
var noOpQuote = new ConstructionQuote("source", "policy", "free", ConstructionKind.Building, 1, Amounts(("nanites", 0)), Amounts(), Amounts(), []);
var noOpEnqueue = blockedAggregate.Enqueue("enqueue-zero", 1, 0, "blocked", 0, "zero", "slot", noOpQuote);
Check(noOpEnqueue.Accepted, "Zero nanite cost отказал.");
var noOpCancel = noOpEnqueue.State.CancelPaid("cancel-zero", 1, 1, "blocked", 1, "zero");
Check(noOpCancel.Accepted && noOpCancel.State.GetBalance(1, "nanites") == 0, "Zero refund потребовал unknown cap.");
var candidate = ConstructionEconomyCandidate.Stage(blocked, noOpEnqueue.State.Ledgers["blocked"], blockedPlanet, "candidate", 1, 1, "phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, [new("zero", .5m)]);
Check(!candidate.Accepted && candidate.Code == "resource-storage-unresolved:nanites" && ReferenceEquals(candidate.Economy, blocked) && ReferenceEquals(candidate.Ledger, noOpEnqueue.State.Ledgers["blocked"]), "Candidate wrapper потерял typed diagnostic/rollback.");
var equalCategory = new EconomyWorld(Resources(), [new("same", "planet_metallurgists", 100, Amounts(), Amounts(), "planet_metallurgists")], Categories());
var equalCountry = equalCategory.CreateCountry(); var equalPlanet = equalCategory.CreatePlanet(equalCountry);
equalCategory.AddGroup(equalPlanet, 100, 1); equalCategory.SetJobs(equalPlanet, "same", 1);
equalCategory.SetModifiers(equalPlanet, [new("planet_metallurgists", "exotic_gases", "produces", .1m, .5m)]);
Check(equalCategory.PreviewPlanetReport(equalPlanet).Produced["exotic_gases"] == .15m, "Совпадение synthetic/source categories удвоило Add или Mult.");

ConstructionBankSnapshot Bank(decimal nanites = 0) => new(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = Amounts(("minerals", 2000), ("nanites", nanites)) },
    new Dictionary<string, ConstructionStorage> { ["minerals"] = new(false, 15000), ["nanites"] = new(false, null) });
ConstructionLiveRuntime Runtime() => new("C", 1, [new("A", ConstructionKind.Building, "zone", "zone_industrial"), new("B", ConstructionKind.Building, "zone", "zone_industrial")], []);
ConstructionQuote Quote(string id = "producer", string resource = "minerals", decimal cost = 600, int? limit = 1) => new("source", "policy", id, ConstructionKind.Building, 1, Amounts((resource, cost)), Amounts(("energy", 3)), Amounts(), [], limit);
var runtime = Runtime(); var bank = Bank(); var quote = Quote();
var first = runtime.Enqueue("one", 1, 0, "order", "A", quote, bank); Check(first.Accepted && first.ResourceDeltas.Single().Amount == -600, "Платная постановка limit1.");
Check(first.State.Enqueue("one", 1, 0, "order", "A", quote, bank).Code == "duplicate", "Retry продублировал reservation.");
Check(first.State.Enqueue("two", 1, 1, "other", "B", quote, bank).Code == "planet-limit-reached", "Pending limit не учитывается между slots.");
var paused = first.State.AdvanceDay("paused", 1, 0, new Dictionary<string, decimal?>(), new Dictionary<string, bool?> { ["order"] = true }, bank);
Check(paused.Accepted && paused.State.Enqueue("three", 1, paused.State.Revision, "other", "B", quote, bank).Code == "planet-limit-reached", "Paused reservation потеряна.");
var cancelled = first.State.Cancel("cancel", 1, 1, "order", bank); Check(cancelled.Accepted && cancelled.ResourceDeltas.Single().Amount == 600, "Cancel refund.");
Check(cancelled.State.Enqueue("again", 1, 2, "other", "B", quote, bank).Accepted, "Cancel не освободил limit.");
var completed = first.State.AdvanceDay("complete", 1, 1, new Dictionary<string, decimal?> { ["order"] = 0 }, new Dictionary<string, bool?> { ["order"] = true }, bank);
Check(completed.Accepted && completed.State.Installed["A"].DefinitionId == "producer", "Completion отверг собственную reservation.");
Check(completed.State.Enqueue("after", 1, 2, "other", "B", quote, bank).Code == "planet-limit-reached", "Installed limit потерян.");
var naniteOrder = runtime.Enqueue("nanite", 1, 0, "n", "A", Quote("nanite-cost", "nanites", 2, null), Bank(3));
Check(naniteOrder.Accepted && naniteOrder.ResourceDeltas.Single().Amount == -2, "Safe debit требует upper cap.");
var refund = naniteOrder.State.Cancel("n-cancel", 1, 1, "n", Bank(1));
Check(!refund.Accepted && refund.Code == "resource-storage-unresolved:nanites" && ReferenceEquals(refund.State, naniteOrder.State) && refund.ResourceDeltas.Count == 0, "Unknown-cap refund потерял stock/order.");
Check(runtime.Enqueue("poor", 1, 0, "n", "A", Quote("nanite-cost", "nanites", 2, null), Bank(0)).Code == "insufficient-resources", "Insufficient debit.");

if (args is not ["--game-root", var root]) throw new ArgumentException("Нужен --game-root для source-backed проверки.");
var economy = await new PlanetEconomyImporter().ImportAsync(root);
Check(rare.All(economy.Resources.ContainsKey), "Все семь resource IDs должны присутствовать в каталоге.");
Check(rare.Where(id => id != "nanites").All(id => economy.Resources[id].SourceMaximum == 15000 && economy.Resources[id].StoragePolicy == PlanetEconomyStoragePolicy.SourceCapped), "Source caps шести ресурсов.");
Check(economy.Resources["nanites"].SourceMaximum is null && economy.Resources["nanites"].StoragePolicy == PlanetEconomyStoragePolicy.Unspecified, "Nanites source policy guessed.");
Check(economy.Sources.Any(s => s.RelativePath == PlanetEconomyImporter.RareCompositeSource), "Composite source closure отсутствует.");
var importer = new ConstructionDefinitionImporter(); var conditions = await importer.ImportRareProducerConditionCatalogAsync(root);
Check((await importer.ImportCapitalConditionCatalogAsync(root)).Definitions.Count == 8 && conditions.Definitions.Count == 10, "Legacy condition catalog изменён.");
var ids = new[] { "building_refinery", "building_chemical_plant", "building_crystal_plant" };
var catalogs = new List<ConstructionCatalogProjection>();
foreach (var id in ids) catalogs.Add(await importer.ImportOrdinaryBuildingCatalogAsync(root, economy.Buildings[id]));
var content = new ConstructionLiveContent(catalogs, economy, conditions);
var facts = Enum.GetValues<ConstructionOwnerPredicate>().ToDictionary(id => id, id => (bool?)(id is ConstructionOwnerPredicate.Regular or ConstructionOwnerPredicate.UsesFood));
var context = new ConstructionContext(true, facts, false, "pc_continental", [], [], ["tech_exotic_gases", "tech_volatile_motes", "tech_rare_crystals"], ids.ToDictionary(id => id, _ => 0m), new(validCivics: [], ownerFlags: []));
var assessments = new List<ConstructionInstalledCapacityObject>();
foreach (var id in ids)
{
    var catalog = content.Catalogs[id];
    Check(catalog.Files.Any(f => f.RelativePath == PlanetEconomyImporter.RareCompositeSource), "Raw construction composite dependency missing.");
    var assessed = ConstructionCatalogAssessor.Assess(catalog, id, context, conditions);
    var leaves = assessed.Contributions.Where(c => c.Kind == "planet_modifier").SelectMany(c => c.Amounts).ToArray();
    var terms = economy.Buildings[id].Terms.Where(t => PlanetEconomyImporter.IsRareIndustrialComposite(t.SourceExpression ?? "")).ToArray();
    Check(leaves.Length == 2 && terms.Length == 2 && leaves.All(x => terms.Any(t => t.TargetId == x.Resource && t.Value == x.Value && t.Provenance.RelativePath == x.Origin.Source && t.Provenance.Line == x.Origin.Line)) && leaves.All(x => x.Value == .1m), "Две projections composite/leaf provenance не совпали.");
    var result = content.Quote(id, context, 0, 0, new Dictionary<string, bool>());
    Check(result.Quote is { PlanetLimit: 1 } && result.Quote.Costs["minerals"] == (id == "building_refinery" ? 500 : 600) && result.Quote.InfrastructureUpkeep["energy"] == 3, "Source producer quote: " + result.Code);
    assessments.Add(new(id, assessed));
}
var unknownContext = new ConstructionContext(true, facts, false, "pc_continental", [], [], context.Technologies, ids.ToDictionary(id => id, _ => 0m), new(validCivics: null));
var unknownAssessment = ConstructionCatalogAssessor.Assess(content.Catalogs[ids[0]], ids[0], unknownContext, conditions);
Check(unknownAssessment.Contributions.Any(c => c.Kind == "triggered_planet_modifier" && c.Condition.Truth == ConstructionTruth.Unknown), "Unknown civic context превращён в false.");
var catalyticContext = new ConstructionContext(true, facts, false, "pc_continental", [], [], context.Technologies, ids.ToDictionary(id => id, _ => 0m), new(validCivics: ["civic_catalytic_processing"]));
var catalyticAssessment = ConstructionCatalogAssessor.Assess(content.Catalogs[ids[0]], ids[0], catalyticContext, conditions);
var catalyticAmounts = catalyticAssessment.Contributions.Where(c => c.Kind == "triggered_planet_modifier" && c.Condition.Truth == ConstructionTruth.True).SelectMany(c => c.Amounts).ToArray();
Check(catalyticAmounts.Length == 2 && catalyticAmounts.All(x => x.Resource.EndsWith("_food_upkeep_add") && x.Value == .5m), "Доказанный catalytic civic не выбрал source food branch.");
var unknownLimit = new ConstructionContext(true, facts, false, "pc_continental", [], [], context.Technologies, null, context.SourceFacts);
Check(content.Quote(ids[0], unknownLimit, 0, 0).Code == "planet-limit-context-unresolved", "Неизвестный installed count угадан нулём.");
var occupiedContext = new ConstructionContext(true, facts, false, "pc_continental", [], [], context.Technologies, ids.ToDictionary(id => id, _ => 1m), context.SourceFacts);
Check(content.Quote(ids[0], occupiedContext, 0, 0).Code == "planet-limit-reached", "Installed source limit не участвует в quote.");
var installedWorld = World(); var installedCountry = installedWorld.CreateCountry(); var installedPlanet = installedWorld.CreatePlanet(installedCountry);
installedWorld.AddGroup(installedPlanet, 100, 1); installedWorld.SetJobs(installedPlanet, "foundry", 1); installedWorld.SetJobBonusWorkforceMultiplier(installedPlanet, "foundry", .5m);
installedWorld.SetModifiers(installedPlanet, [new("runtime-foundry", "alloys", "produces", 1, .4m), new("runtime-foundry", "minerals", "upkeep", 1, .4m)]);
var account = new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = installedWorld.GetBalances(installedCountry) };
var ledger = new ConstructionLedger(new("C", ConstructionAuthorityMode.Individualist, false, null, null), 1, account, []);
var aggregate = new ConstructionAggregate(installedWorld, new Dictionary<int, EntityId> { [1] = installedCountry }, [new("C", 1, installedPlanet)], [ledger]);
var installed = aggregate.ReplaceInstalledCapacity("install", 0, "C", assessments);
if (!installed.Accepted)
{
    // Диагностика внутреннего stage только при отказе, без обхода runtime gate.
    try { typeof(ConstructionInstalledCapacitySnapshot).GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, ["C", 1L, assessments.ToArray()]); }
    catch (System.Reflection.TargetInvocationException error) { Console.Error.WriteLine(error.InnerException); }
}
Check(installed.Accepted, "Source producer effects stage: " + installed.Code);
var actual = installed.State.ForkEconomy().PreviewPlanetReport(installedPlanet);
Check(actual.Produced["volatile_motes"] == .15m && actual.Produced["exotic_gases"] == .15m && actual.Produced["rare_crystals"] == .15m, "Реальные source producer effects/effectiveWF не создали rare channels.");
Check(actual.JobUpkeep["minerals"] == 17.85m && actual.Produced["alloys"] == 8.4m, "Synthetic tech production/upkeep потеряны рядом с тремя source producers.");
var refreshed = installed.State.ReplaceInstalledCapacity("refresh", 1, "C", assessments);
Check(refreshed.Accepted && refreshed.State.ForkEconomy().PreviewPlanetReport(installedPlanet).JobUpkeep["minerals"] == 17.85m
    && refreshed.State.ForkEconomy().PreviewPlanetReport(installedPlanet).Produced["exotic_gases"] == .15m, "Installed refresh накопил synthetic/source effects.");
// Source technology effects проверяются canonical compiler, без второй формулы/engine.
JobDefinition[] industrialJobs = [new("foundry", "runtime-foundry", 100, Amounts(("alloys", 3)), Amounts(("minerals", 6)), "planet_metallurgists"),
    new("artisan", "planet_artisans", 100, Amounts(("consumer_goods", 6)), Amounts(("minerals", 6)), "planet_artisans")];
var techWorld = new EconomyWorld(Resources().Append(new ResourceDefinition("consumer_goods", 15000)).ToArray(), industrialJobs, Categories());
var techCountry = techWorld.CreateCountry(); var techPlanet = techWorld.CreatePlanet(techCountry);
techWorld.AddGroup(techPlanet, 200, 1); techWorld.SetJobs(techPlanet, "foundry", 1); techWorld.SetJobs(techPlanet, "artisan", 1); techWorld.SetJobBonusWorkforceMultiplier(techPlanet, "foundry", .5m);
var techAccounts = new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = techWorld.GetBalances(techCountry) };
var techLedger = new ConstructionLedger(new("T", ConstructionAuthorityMode.Individualist, false, null, null), 1, techAccounts, []);
var techAggregate = new ConstructionAggregate(techWorld, new Dictionary<int, EntityId> { [1] = techCountry }, [new("T", 1, techPlanet)], [techLedger]);
var techInstalled = techAggregate.ReplaceInstalledCapacity("install", 0, "T", assessments);
Check(techInstalled.Accepted, "Tech test source installation."); techWorld = techInstalled.State.ForkEconomy();
var unchangedHash = techWorld.StateHash(); var closure = techWorld.GetEffectiveJobResourceClosure(techPlanet);
Check(closure["foundry"].Produced.Contains("exotic_gases") && closure["artisan"].Produced.Contains("volatile_motes") && !closure["foundry"].Upkeep.Contains("exotic_gases") && techWorld.StateHash() == unchangedHash, "Readonly effective closure неверна.");
var technologyCatalog = new TechnologyImporter().Import(root);
var techResearch = ResearchCountry.Create(technologyCatalog, 1, ["tech_consumer_good_refinement_1", "tech_advanced_metallurgy_1"], new ResearchFacts(new Dictionary<string, bool?>()));
var effects = ResearchEffects.Project(techResearch, economy, industrialJobs, new HashSet<string>(), closure);
Check(!ResearchEffects.Project(techResearch, economy, industrialJobs, new HashSet<string>()).JobModifiers.Any(x => x.Resource == "exotic_gases"), "Optional null изменил прежний base-only caller.");
try
{
    ResearchEffects.Project(techResearch, economy, industrialJobs, new HashSet<string>(), new Dictionary<string, JobResourceClosure> { ["foundry"] = new(["fake-resource"], []) });
    throw new Exception("Unknown closure resource принят.");
}
catch (ArgumentException) { Check(true, "Unknown closure resource rejected."); }
foreach (var job in industrialJobs)
    foreach (var resource in new[] { "volatile_motes", "exotic_gases", "rare_crystals" })
        Check(effects.JobModifiers.Single(x => x.Category == job.Category && x.Resource == resource && x.Operation == "produces").Mult == .05m, "Canonical category tech пропустил rare channel/удвоил modifier.");
techWorld.SetModifiers(techPlanet, effects.JobModifiers);
var techReport = techWorld.PreviewPlanetReport(techPlanet);
Check(techReport.Produced["exotic_gases"] == .2625m && techReport.Produced["alloys"] == 4.725m && techReport.Produced["consumer_goods"] == 6.3m && techReport.JobUpkeep["minerals"] == 18.75m, "Source .05 output tech должен масштабировать base+rare ровно один раз, upkeep неизменен.");
var repeated = ResearchEffects.Project(techResearch, economy, industrialJobs, new HashSet<string>(), techWorld.GetEffectiveJobResourceClosure(techPlanet));
techWorld.SetModifiers(techPlanet, repeated.JobModifiers);
Check(techWorld.PreviewPlanetReport(techPlanet).Produced["exotic_gases"] == .2625m, "Research refresh накопил rare multiplier.");
Check(ResearchEffects.AppliedKeys(techResearch, "tech_advanced_metallurgy_1", economy, industrialJobs, new HashSet<string>(), closure).Contains("planet_metallurgists_produces_mult"), "AppliedKeys расходится с canonical closure projection.");
techWorld.SetInstalledJobEconomyEffects(techPlanet, null);
var removed = techWorld.GetEffectiveJobResourceClosure(techPlanet);
Check(!removed["foundry"].Produced.Contains("exotic_gases") && closure["foundry"].Produced.Contains("exotic_gases"), "Snapshot мутирует или старый research overlay попал в closure.");
var afterRemoval = ResearchEffects.Project(techResearch, economy, industrialJobs, new HashSet<string>(), removed);
Check(!afterRemoval.JobModifiers.Any(x => x.Resource == "exotic_gases"), "Removed producer сохранил ghost research rare channel.");
Console.WriteLine($"PASS {assertions} checks; source composites/limits/storage/rollback. Natural acquisition проверяет Session owner.");
