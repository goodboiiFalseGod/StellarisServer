using Stellaris.Core.Research;
using Stellaris.Core.Scripting;

static void Check(bool condition, string message) { if (!condition) { throw new Exception(message); } }
static TechnologyDefinition Tech(string id, string area = "physics", decimal cost = 15, decimal weight = 1, ScriptBlock? rules = null,
    ScriptBlock? potential = null, int tier = 1, string[]? prerequisites = null) => new(id, area, cost, tier, Array.AsReadOnly(new[] { "computing" }),
    Array.AsReadOnly(prerequisites ?? []), false, false, potential, null, weight, rules, Array.AsReadOnly(Array.Empty<string>()), "synthetic", 1, Array.AsReadOnly(Array.Empty<string>()));
var facts = new ResearchFacts(new Dictionary<string, bool?>());
var definitions = new[] { Tech("a"), Tech("b"), Tech("c"), Tech("d"), Tech("s", "society"), Tech("e", "engineering") };
var catalog = new TechnologyCatalog(definitions, [new(0, 0, null), new(1, 0, null), new(2, 6, null)], 3, .5m, -.25m);
var country = ResearchCountry.Create(catalog, 42, [], facts);
Check(country.Areas.Count == 3 && country.Areas["physics"].Choices.Count == 3 && country.Areas["physics"].Choices.Distinct().Count() == 3, "Три направления, без повторов.");
Check(country.Seed == ResearchCountry.Create(catalog, 42, [], facts).Seed && country.Areas["physics"].Choices.SequenceEqual(ResearchCountry.Create(catalog, 42, [], facts).Areas["physics"].Choices), "Детерминированная выдача.");
var production = new Dictionary<string, decimal> { ["physics"] = 10, ["society"] = 10, ["engineering"] = 10 };
var reserve = new Dictionary<string, decimal> { ["physics"] = 12, ["society"] = 12, ["engineering"] = 12 };
var idle = country.AdvanceMonth(production, reserve, 1m);
Check(idle.Reserves.Values.All(value => value == 22) && idle.Country.Seed == country.Seed, "Накопление без выбора и без переброса.");
Check(country.AdvanceMonth(production, reserve, .75m).Reserves.Values.All(value => value == 22), "Без проекта сохраняется RAW science flow, без штрафа скорости.");
var chosen = country.Areas["physics"].Choices[0];
var active = country.Select("physics", chosen, country.Revision);
var month = active.AdvanceMonth(production, reserve, 1m);
Check(month.Completed.SequenceEqual(new[] { chosen }) && month.Country.Researched.Contains(chosen) && month.Reserves["physics"] == 2, "Избыток теряется; только старый резерв расходуется.");
Check(month.Country.Areas["physics"].ActiveTechnologyId is null && month.Country.Areas["physics"].Progress[chosen] == 15 && !month.Country.Areas["physics"].Choices.Contains(chosen), "Стоимость ограничивает прогресс, автоматического выбора нет.");
Check(!country.Researched.Contains(chosen) && country.Areas["physics"].ActiveTechnologyId is null, "Исходная страна неизменна.");
var slow = active.AdvanceMonth(production, reserve.ToDictionary(pair => pair.Key, _ => 0m), .75m);
Check(slow.Country.Areas["physics"].Progress[chosen] == 7.5m && slow.Country.Seed == active.Seed, "Множитель лидера и устойчивые предложения.");
var other = active.Areas["physics"].Choices.First(id => id != chosen);
var switched = slow.Country.Select("physics", other, slow.Country.Revision).Select("physics", chosen, slow.Country.Revision + 1);
Check(switched.Areas["physics"].Progress[chosen] == 7.5m, "Переключение сохраняет прогресс.");
try { _ = country.Select("physics", chosen, 999); throw new Exception("Нет stale отказа."); } catch (InvalidOperationException exception) when (exception.Message == "STALE_REVISION") { }
var unknown = Tech("unknown", potential: ParadoxParser.Parse("has_unimplemented_fact = yes"));
var prerequisite = Tech("prerequisite", prerequisites: ["a"]);
var tiered = Tech("tiered", tier: 2);
var weighted = Tech("weighted", rules: ParadoxParser.Parse("factor = 2 modifier = { factor = 3 has_technology = a }"));
var extended = new TechnologyCatalog(definitions.Concat([unknown, prerequisite, tiered, weighted]), catalog.Tiers.Values, 3, .5m, -.25m);
var assessed = ResearchCountry.Create(extended, 2, [], facts);
Check(assessed.Assess("unknown").Gates.Contains("potential-unknown") && !assessed.Assess("prerequisite").Available && !assessed.Assess("tiered").Available, "Unknown/prerequisite/tier закрыты.");
Check(assessed.Assess("weighted").Weight == 2 && assessed.Assess("weighted", new HashSet<string> { "weighted" }).Weight == 1, "Source и previous-option веса.");
Check(ResearchCountry.Create(extended, 2, ["a"], facts).Assess("weighted").Weight == 6, "Адаптивный source weight.");
var counts = new Dictionary<string, int> { ["light"] = 0, ["heavy"] = 0 };
var fair = new TechnologyCatalog([Tech("light", weight: 1), Tech("heavy", weight: 3)], catalog.Tiers.Values, 1, .5m, -.25m);
for (ulong seed = 0; seed < 4000; seed++) { counts[ResearchCountry.Create(fair, seed, [], facts).Areas["physics"].Choices[0]]++; }
Check(counts["heavy"] > 2700 && counts["heavy"] < 3300, "Взвешенная частота 1:3.");
var repeat = Tech("repeat", cost: 10) with { MaximumLevels = 2, CostPerLevel = 5 };
var repeatCatalog = new TechnologyCatalog([repeat], catalog.Tiers.Values, 3, .5m, -.25m);
var repeated = ResearchCountry.Create(repeatCatalog, 1, [], facts).Select("physics", "repeat", 0);
var repeatMonth = repeated.AdvanceMonth(production, reserve, 1);
Check(repeatMonth.Country.Levels["repeat"] == 1 && repeatMonth.Country.CurrentCost("repeat") == 15
    && repeatMonth.Country.Areas["physics"].Progress["repeat"] == 0 && repeatMonth.Country.Areas["physics"].Choices.Contains("repeat"), "Повторяемый следующий уровень, стоимость и сброс.");
var finalLevel = repeatMonth.Country.Select("physics", "repeat", repeatMonth.Country.Revision).AdvanceMonth(production, reserve, 1);
Check(finalLevel.Country.Levels["repeat"] == 2 && !finalLevel.Country.Areas["physics"].Choices.Contains("repeat"), "Конечное число уровней.");
var groupDefs = new[] { Tech("group-a") with { WeightGroups = Array.AsReadOnly(new[] { "group" }), GroupWeightMultipliers = new Dictionary<string,decimal> { ["group"] = 0 } },
    Tech("group-b") with { WeightGroups = Array.AsReadOnly(new[] { "group" }), GroupWeightMultipliers = new Dictionary<string,decimal> { ["group"] = 0 } } };
Check(ResearchCountry.Create(new TechnologyCatalog(groupDefs, catalog.Tiers.Values, 3, .5m, -.25m), 1, [], facts).Areas["physics"].Choices.Count == 1, "Source group изменяет оставшуюся выдачу.");
Check(month.Country.Areas["physics"].LastCompletedTechnologyId == chosen && month.Country.AdvanceMonth(production, reserve, 1).Country.Areas["physics"].LastCompletedTechnologyId == chosen, "Уведомление сохраняется.");
var simultaneous = country;
foreach (var area in ResearchCountry.AreaIds) { simultaneous = simultaneous.Select(area, simultaneous.Areas[area].Choices[0], simultaneous.Revision); }
Check(simultaneous.AdvanceMonth(production, reserve, 1).Completed.Count == 3, "Три одновременных завершения.");
var scopedDefinition = Tech("scoped", potential: ParadoxParser.Parse("years_passed > 5 any_owned_planet = { num_districts = { type = district_generator value >= 2 } owner = { has_technology = a } }"));
var scopedCatalog = new TechnologyCatalog([Tech("a"), scopedDefinition], catalog.Tiers.Values, 3, .5m, -.25m);
var planetFacts = new ResearchFacts(new Dictionary<string,bool?>(), new Dictionary<string,decimal> { [ResearchFacts.NumericKey("planet", "num_districts", "district_generator")] = 2 });
var scopedFacts = new ResearchFacts(new Dictionary<string,bool?>(), new Dictionary<string,decimal> { [ResearchFacts.NumericKey("country","years_passed")] = 6 },
    new Dictionary<string,IReadOnlyList<ResearchFacts>> { [ResearchFacts.NumericKey("country","any_owned_planet")] = Array.AsReadOnly(new[] { planetFacts }) });
Check(ResearchCountry.Create(scopedCatalog, 1, ["a"], scopedFacts).Assess("scoped").Available, "Реальные scoped collections, owner и числовые сравнения.");
Check(!ResearchCountry.Create(scopedCatalog, 1, [], scopedFacts).Assess("scoped").Available, "Owner prerequisite не подменяется фактом планеты.");
var emptyPlanets = new ResearchFacts(new Dictionary<string,bool?>(), scopedFacts.Numbers,
    new Dictionary<string,IReadOnlyList<ResearchFacts>> { [ResearchFacts.NumericKey("country","any_owned_planet")] = Array.AsReadOnly(Array.Empty<ResearchFacts>()) });
Check(ResearchCountry.Create(scopedCatalog, 1, ["a"], emptyPlanets).Assess("scoped").Gates.Contains("potential-false"), "Пустая известная коллекция отличается от неизвестной.");
Check(ResearchCountry.Create(scopedCatalog, 1, ["a"], new ResearchFacts(new Dictionary<string,bool?>(), scopedFacts.Numbers)).Assess("scoped").Gates.Contains("potential-unknown"), "Отсутствующий scope unknown.");
var mutableCategories = new List<string> { "computing" };
var frozenCatalog = new TechnologyCatalog([Tech("frozen") with { Categories = mutableCategories }], catalog.Tiers.Values, 3, .5m, -.25m);
mutableCategories.Clear();
Check(frozenCatalog.Technologies["frozen"].Categories.Count == 1, "Каталог копирует коллекции вызывающего кода.");
var conditionalCost = Tech("conditional") with { Cost = null, CostRules = ParadoxParser.Parse("factor = 100 modifier = { factor = 0.5 has_technology = a }") };
var costCatalog = new TechnologyCatalog([Tech("a"), conditionalCost], catalog.Tiers.Values, 3, .5m, -.25m);
Check(ResearchCountry.Create(costCatalog, 1, [], facts).CurrentCost("conditional") == 100
    && ResearchCountry.Create(costCatalog, 1, ["a"], facts).CurrentCost("conditional") == 50, "Условная исходная стоимость.");
var invalidCost = conditionalCost with { CostRules = ParadoxParser.Parse("factor > 10") };
Check(ResearchCountry.Create(new TechnologyCatalog([invalidCost], catalog.Tiers.Values, 3, .5m, -.25m), 1, [], facts).CurrentCost("conditional") is null, "Неизвестная форма стоимости не даёт успех.");
var offeredTech = Tech("offered", rules: ParadoxParser.Parse("modifier = { factor = 0 has_country_flag = suppress_draw }"), potential: ParadoxParser.Parse("is_regular_empire = yes"));
ResearchFacts OfferFacts(bool suppressed, bool regular) => new(new Dictionary<string,bool?> { [ResearchFacts.Key("country","has_country_flag","suppress_draw")] = suppressed,
    [ResearchFacts.Key("country","is_regular_empire","yes")] = regular });
var offered = ResearchCountry.Create(new TechnologyCatalog([offeredTech], catalog.Tiers.Values, 3, .5m, -.25m), 1, [], OfferFacts(false, true));
var suppressed = offered.WithFacts(OfferFacts(true, true));
Check(!suppressed.Assess("offered").Available && suppressed.Select("physics", "offered", suppressed.Revision).Areas["physics"].ActiveTechnologyId == "offered", "Вес 0 не запрещает уже выданную карту.");
try { var forbidden = offered.WithFacts(OfferFacts(false, false)); _ = forbidden.Select("physics","offered",forbidden.Revision); throw new Exception("Potential false принят."); }
catch (InvalidOperationException exception) when (exception.Message == "RESEARCH_UNAVAILABLE") { }
var crossedCatalog = new TechnologyCatalog([Tech("unlock", cost: 10), Tech("crossed", "society", prerequisites: ["unlock"])], catalog.Tiers.Values, 3, .5m, -.25m);
var crossed = ResearchCountry.Create(crossedCatalog, 1, [], facts);
Check(crossed.Areas["society"].Choices.Count == 0, "Междисциплинарная зависимость первоначально закрыта.");
var crossedMonth = crossed.Select("physics","unlock",0).AdvanceMonth(production,reserve,1);
Check(crossedMonth.Country.Areas["society"].Choices.SequenceEqual(new[] { "crossed" }), "Завершение в другом направлении заполняет пустые предложения.");
Check(suppressed.Seed == offered.Seed && suppressed.Revision == offered.Revision && suppressed.Areas["physics"].Choices.SequenceEqual(offered.Areas["physics"].Choices), "Обновление фактов не перебрасывает непустые предложения.");
var factUnlocked = Tech("fact-unlocked", potential: ParadoxParser.Parse("is_regular_empire = yes"));
var emptyOffers = ResearchCountry.Create(new TechnologyCatalog([factUnlocked], catalog.Tiers.Values, 3, .5m, -.25m), 1, [], OfferFacts(false, false));
var legalRefresh = emptyOffers.WithFacts(OfferFacts(false, true), fillEmptyChoices: false);
Check(legalRefresh.Revision == emptyOffers.Revision && legalRefresh.Seed == emptyOffers.Seed && legalRefresh.Areas["physics"].Choices.Count == 0
    && legalRefresh.Eligibility("fact-unlocked").Available, "Select-path обновляет legality без изменения предложений/revision/seed.");
Check(emptyOffers.WithFacts(OfferFacts(false,true)).Areas["physics"].Choices.SequenceEqual(new[] { "fact-unlocked" }), "Событие обновления фактов заполняет пустое направление.");
if (args.Length > 0)
{
    var real = new TechnologyImporter().Import(args[0]);
    await EffectChecks.Run(real, args[0]);
    var exportIndex = Array.IndexOf(args, "--export-icons");
    if (exportIndex >= 0)
    {
        var exportPath = Path.GetFullPath(args[exportIndex + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
        File.WriteAllText(exportPath, System.Text.Json.JsonSerializer.Serialize(real.Technologies.ToDictionary(pair => pair.Key, pair => pair.Value.Icon), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }
    Check(real.Technologies.Count > 500 && real.Alternatives == 3 && real.PreviousOptionMultiplier == .5m && real.NoLeaderPenalty == -.25m, "Реальный полный каталог и defines.");
    Check(real.Technologies["tech_basic_science_lab_2"].Cost == 1500 && real.Technologies["tech_food_processing_2"].Cost == 4000
        && real.Technologies["tech_mineral_purification_2"].Prerequisites.Contains("tech_colonial_centralization"), "Source costs/prerequisites.");
    Check(real.Technologies["tech_repeatable_improved_tile_mineral_output"].MaximumLevels == -1
        && real.Technologies["tech_repeatable_improved_tile_mineral_output"].GroupWeightMultipliers["repeatable"] == .01m
        && real.Technologies["tech_repeatable_improved_tile_mineral_output"].EffectDetails.Single(effect => effect.Key == "planet_jobs_minerals_produces_mult").Value == .05m, "Source repeated group и численные эффекты.");
    Check(real.Technologies.Values.All(tech => tech.Icon is { Sha256.Length: 64, ByteLength: > 0 } icon && icon.RelativePath.StartsWith("gfx/interface/icons/technologies/", StringComparison.Ordinal)), "Source icons с hash и размером.");
    Console.WriteLine($"Реальный источник: {real.Technologies.Count} технологий, {real.Manifest!.Files.Count} файлов.");
    Console.WriteLine("Отсутствующая исходная стоимость: " + string.Join(", ", real.Technologies.Values.Where(tech => tech.Cost is null && tech.CostRules is null).Select(tech => tech.Id)));
    var template = Stellaris.Core.Construction.ConstructionOrdinaryTemplate.Get("scientific");
    var realFacts = new Dictionary<string,bool?>(StringComparer.Ordinal);
    void Fact(string predicate, string argument, bool value) => realFacts[ResearchFacts.Key("country", predicate, argument)] = value;
    foreach (var pair in new Dictionary<string,bool> { ["is_ai"] = false, ["is_gestalt"] = false, ["is_machine_empire"] = false,
        ["is_hive_empire"] = false, ["is_robot_empire"] = false, ["is_wilderness_empire"] = false, ["is_nomadic_empire"] = false,
        ["is_nomadic"] = false, ["is_regular_empire"] = true, ["is_fallen_empire"] = false, ["country_uses_food"] = true,
        ["is_low_tech_start"] = false, ["is_materialist"] = true, ["is_spiritualist"] = false })
    { Fact(pair.Key,"yes",pair.Value); Fact(pair.Key,"no",!pair.Value); }
    void Visit(ScriptBlock? block)
    {
        if (block is null) { return; }
        foreach (var entry in block.Entries)
        {
            if (entry.Scalar is { } argument)
            {
                bool? value = entry.Key switch { "has_ethic" => template.Ethics.Contains(argument), "has_valid_civic" or "has_civic" => template.Civics.Contains(argument),
                    "has_origin" => template.OriginId == argument, "has_authority" => template.AuthorityId == argument, _ => null };
                if (value is { } known) { Fact(entry.Key, argument, known); }
            }
            Visit(entry.Block);
        }
    }
    foreach (var tech in real.Technologies.Values) { Visit(tech.Potential); Visit(tech.WeightRules); Visit(tech.StartingPotential); foreach(var trigger in tech.SwapTriggers) { Visit(trigger); } }
    var realCountry = ResearchCountry.Create(real, 1, template.StartingTechnologies, new(realFacts));
    Console.WriteLine("Начальная выдача scientific с ограниченными фактами audit fixture: " + string.Join(", ", ResearchCountry.AreaIds.Select(area => area + "=" + real.Technologies.Values.Count(tech => tech.Area == area && realCountry.Assess(tech.Id).Available))));
    Console.WriteLine("Основные gates: " + string.Join(", ", real.Technologies.Keys.SelectMany(id => realCountry.Assess(id).Gates).GroupBy(gate => gate.Split(':')[0]).OrderByDescending(group => group.Count()).Select(group => group.Key + "=" + group.Count())));
    Console.WriteLine($"Стартовые source flags: {real.Technologies.Values.Count(tech => tech.Starting)}, ordinary known starts: {real.AssessStarting(new(realFacts)).Count(item => item.Available)}, принятый template: {template.StartingTechnologies.Count}; icons: {real.Technologies.Values.Count(tech => tech.Icon is not null)}.");
    foreach (var tech in real.Technologies.Values.Where(tech => tech.Area == "society" && tech.Tier == 1 && tech.Prerequisites.All(realCountry.Researched.Contains)
        && !realCountry.Assess(tech.Id).Gates.Contains("potential-false")).Take(12))
    { Console.WriteLine(tech.Id + ": " + string.Join(", ", realCountry.MissingFacts(tech.Id))); }
}
Console.WriteLine("ResearchChecks: все проверки пройдены.");
