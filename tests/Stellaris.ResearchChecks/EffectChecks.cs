using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;

internal static class EffectChecks
{
    public static async Task Run(TechnologyCatalog catalog, string root)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var jobs = new[]
        {
            new JobDefinition("farmer", "farmer", 100, new Dictionary<string, decimal> { ["food"] = 6 }, new Dictionary<string, decimal> { ["energy"] = 1 }),
            new JobDefinition("physicist", "physicist", 100, new Dictionary<string, decimal> { ["physics_research"] = 4 }, new Dictionary<string, decimal> { ["consumer_goods"] = 2 })
        };
        var facts = new ResearchFacts(new Dictionary<string, bool?>
        {
            [ResearchFacts.Key("country", "is_wilderness_empire", "yes")] = false,
            [ResearchFacts.Key("country", "is_tankbound_empire", "yes")] = false,
            [ResearchFacts.Key("country", "has_origin", "origin_synthetic_fertility")] = false
        });
        var ids = new HashSet<string> { "tech_administrative_ai", "tech_powered_exoskeletons", "tech_assembly_pattern", "tech_eco_simulation" };
        var identities = new HashSet<string> { "ethic_fanatic_materialist", "civic_hyperspace_specialty", "origin_default" };
        var country = ResearchCountry.Create(catalog, 1, ids, facts);
        var effects = ResearchEffects.Project(country, economy, jobs, identities);
        Check(effects.AllTechnologySpeedBonus == .15m && effects.AreaTechnologySpeedBonuses["physics"] == .30m
            && effects.AreaResearchMultipliers["physics"] == 1.05m && effects.AreaResearchMultipliers["society"] == .90m,
            "Source identity/all/area и additive no-leader ровно один раз.");
        Check(effects.JobModifiers.Single(item => item.Category == "physicist" && item.Operation == "upkeep").Mult == .05m,
            "Administrative AI применяет researcher upkeep один раз.");
        Check(effects.JobModifiers.Single(item => item.Category == "farmer").Mult == .05m
            && effects.JobModifiers.Single(item => item.Category == "farmer").Operation == "produces",
            "Eco simulation меняет выпуск food, не upkeep farmer.");
        Check(effects.JobBonusWorkforce["farmer"] == .05m && effects.JobBonusWorkforce["physicist"] == 0,
            "Exoskeletons source composite выбирает worker, не specialist.");
        Check(effects.ConstructionModifiers.Single().SourceKey == "planet_building_build_speed_mult"
            && effects.ConstructionModifiers.Single().Value == .25m, "Source assembly pattern building speed.");
        Check(effects.Coverage.Any(item => item == "tech_powered_exoskeletons:deferred-effect:army_damage_mult"), "Военный эффект явно deferred.");
        var second = ResearchEffects.Project(country, economy, jobs, identities);
        Check(second.JobModifiers.SequenceEqual(effects.JobModifiers) && second.JobBonusWorkforce.SequenceEqual(effects.JobBonusWorkforce),
            "Повторная чистая проекция не накапливает бонусы.");
        Check(ResearchEffects.AppliedKeys(country, "tech_eco_simulation", economy, jobs, identities).Contains("planet_farmers_food_produces_mult"),
            "Карточка получает поддержку эффекта без изменения прогресса.");

        TechnologyDefinition Fixture(string id, string body) => new(id, "physics", 100, 1, [], [], false, false, null, null, 1, null, [], "fixture", 1, [])
        { SourceFields = ParadoxParser.Parse(body).Entries, MaximumLevels = -1 };
        var custom = new TechnologyCatalog([
            Fixture("repeat", "modifier = { all_technology_research_speed = 0.1 }"),
            Fixture("swap", "modifier = { all_technology_research_speed = 0.1 } technology_swap = { trigger = { has_country_flag = swap } inherit_effects = no modifier = { country_physics_tech_research_speed = 0.2 } }")
        ], [new(1, 0, null)], 3, .5m, -.25m, categoryParents: catalog.CategoryParents, workforceComposites: catalog.WorkforceComposites);
        var swapFacts = new ResearchFacts(new Dictionary<string, bool?> { [ResearchFacts.Key("country", "has_country_flag", "swap")] = true });
        var swapped = ResearchEffects.Project(ResearchCountry.Create(custom, 1, ["swap"], swapFacts), economy, jobs, new HashSet<string>());
        Check(swapped.AllTechnologySpeedBonus == 0 && swapped.AreaTechnologySpeedBonuses["physics"] == .2m, "inherit_effects=no заменяет базовый modifier.");
        var unknown = ResearchEffects.Project(ResearchCountry.Create(custom, 1, ["swap"], new(new Dictionary<string, bool?>())), economy, jobs, new HashSet<string>());
        Check(unknown.AllTechnologySpeedBonus == 0 && unknown.Coverage.Contains("swap:unknown-effect-swap"), "Unknown swap не становится true или базовым эффектом.");
        var valueSwap = Fixture("value-swap", "modifier = { all_technology_research_speed = 0.05 } technology_swap = { trigger = { has_country_flag = swap } inherit_effects = no modifier = { all_technology_research_speed = 0.20 } }");
        var valueCatalog = new TechnologyCatalog([valueSwap], custom.Tiers.Values, 3, .5m, -.25m);
        var valueCountry = ResearchCountry.Create(valueCatalog, 1, [], swapFacts);
        Check(ResearchEffects.EffectDetails(valueCountry, "value-swap").Single().Value == .20m, "Карточка показывает effective swap value, а не base .05.");
        var unknownDetails = ResearchEffects.EffectDetails(valueCountry.WithFacts(new(new Dictionary<string, bool?>())), "value-swap");
        Check(unknownDetails.Single().Value is null && unknownDetails.Single().Diagnostic == "unknown-effect-swap", "Unknown swap не выдаёт base amount карточке.");
        var noCategories = new TechnologyCatalog([Fixture("unresolved-category", "modifier = { planet_farmers_food_produces_mult = 0.05 }")], custom.Tiers.Values, 3, .5m, -.25m);
        var missing = ResearchEffects.Project(ResearchCountry.Create(noCategories, 1, ["unresolved-category"], swapFacts), economy, jobs, new HashSet<string>());
        Check(missing.JobModifiers.Count == 0 && missing.Coverage.Any(item => item.Contains(":unknown-economic-category:", StringComparison.Ordinal)),
            "Отсутствующая category dependency откладывает зависимый эффект, не ломает View.");
        var inherited = Fixture("inherit", "modifier = { all_technology_research_speed = 0.1 } technology_swap = { trigger = { has_country_flag = swap } inherit_effects = yes modifier = { country_physics_tech_research_speed = 0.2 } }");
        var inheritCatalog = new TechnologyCatalog([inherited], custom.Tiers.Values, 3, .5m, -.25m);
        var inherit = ResearchEffects.Project(ResearchCountry.Create(inheritCatalog, 1, ["inherit"], swapFacts), economy, [], new HashSet<string>());
        Check(inherit.AreaTechnologySpeedBonuses["physics"] == .3m, "inherit_effects=yes дополняет базовый modifier.");
        var repeated = ResearchCountry.Create(custom, 1, ["repeat"], swapFacts).Select("physics", "repeat", 0);
        var flow = ResearchCountry.AreaIds.ToDictionary(area => area, _ => 100m);
        var stocks = ResearchCountry.AreaIds.ToDictionary(area => area, _ => 0m);
        var completed = repeated.AdvanceMonth(flow, stocks, 1).Country;
        Check(completed.Levels["repeat"] == 2 && ResearchEffects.Project(completed, economy, jobs, new HashSet<string>()).AllTechnologySpeedBonus == .2m,
            "Повторяемый эффект умножается на число завершённых уровней.");
        var multipliers = new Dictionary<string, decimal> { ["physics"] = .9m, ["society"] = .8m, ["engineering"] = 1.1m };
        var first = ResearchCountry.Create(custom, 1, [], swapFacts).Select("physics", "repeat", 0).AdvanceMonth(flow, stocks, multipliers);
        Check(first.Country.Areas["physics"].Progress["repeat"] == 90 && first.Reserves["society"] == 100,
            "Раздельные множители областей влияют на active progress, idle RAW stock.");
        Console.WriteLine("ResearchEffects: source effects, identity, swaps, repeatables и per-area month PASS.");
    }
    private static void Check(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
}
