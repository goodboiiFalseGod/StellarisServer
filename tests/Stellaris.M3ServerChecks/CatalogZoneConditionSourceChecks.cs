using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class CatalogZoneConditionSourceChecks
{
    private const string HelpersPath = "common/scripted_triggers/01_scripted_triggers_buildings.txt";
    private const string HasZonePath = "common/scripted_triggers/00_scripted_triggers.txt";
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("catalog-zone-conditions", 1, home.Homeworld, home.Plan);
        var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
        var context = content.Context(startup, ConstructionOrdinaryTemplate.Get("economic"), runtime,
            M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic), usesConsumerGoods: null);
        var zoneIds = M3ZoneGraph.Current(startup, runtime).Select(zone => zone.ZoneId).ToHashSet(StringComparer.Ordinal);
        check(context.SourceFacts?.InstalledZones is { } installed && installed.SetEquals(zoneIds),
            "контекст helper содержит ровно зоны M3ZoneGraph.Current");
        var source = new ContentSession(root);
        var helpers = source.LoadFile(HelpersPath).Root.Entries
            .Where(entry => entry.Key.StartsWith("has_any_", StringComparison.Ordinal) && entry.Key.EndsWith("_zone", StringComparison.Ordinal)
                || entry.Key is "has_mixed_industrial_zone" or "has_volatile_motes_zone").ToArray();
        var helperMap = helpers.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        var hasZone = source.LoadFile(HasZonePath).Root.Named("has_zone").Single();
        helperMap.Add("has_zone", hasZone);
        check(helpers.Length == 24 && helperMap.Count == 25
            && helpers.Count(entry => entry.Key.StartsWith("has_any_", StringComparison.Ordinal)) == 22,
            "в source найдены 22 has_any helper и два named helper");
        var body = hasZone.AsBlock().Entries;
        var count = body.SingleOrDefault(entry => entry.Key == "num_zones");
        var countFields = count?.Block?.Entries;
        check(hasZone.Operator == "=" && body.Count == 1 && count is { Operator: "=", Scalar: null }
            && countFields is { Count: 2 }
            && countFields.Count(field => field.Key == "type" && field.Operator == "=" && field.Scalar == "$zone$" && field.Block is null) == 1
            && countFields.Count(field => field.Key == "value" && field.Operator == ">" && field.Scalar == "0" && field.Block is null) == 1,
            "native has_zone использует num_zones(type=$zone$, value>0)");
        var conditions = content.Content.Conditions;
        check(conditions.Definitions.Count == 64, "текущий authority conditions profile сохраняет zone closure среди 64 определений");
        check(conditions.ProfileId == "construction-conditions-authority-catalog-452-v4"
            && helpers.All(entry => conditions.Definitions.TryGetValue(entry.Key, out var imported)
                && imported.Kind == "planetCondition" && imported.Origin.Source == HelpersPath)
            && conditions.Definitions.TryGetValue("has_zone", out var importedHasZone)
                && importedHasZone.Kind == "planetCondition" && importedHasZone.Origin.Source == HasZonePath,
            "conditions profile импортирует все native zone helpers и has_zone из их файлов");
        var expected = new RawHelperEvaluator(helperMap, zoneIds, context.OwnerExists,
            context.OwnerFacts[ConstructionOwnerPredicate.UsesConsumerGoods]);
        foreach (var helper in helpers)
        {
            var truth = expected.Helper(helper.Key);
            check(ConstructionCatalogAssessor.EvaluatePlanetHelper(conditions, helper.Key, context).Truth == truth,
                $"helper {helper.Key} совпадает с raw source и startup: {truth}");
        }
        var factoryOwner = helpers.Single(entry => entry.Key == "has_any_factory_zone").AsBlock().Entries
            .Single(entry => entry.Key == "owner");
        check(context.OwnerFacts[ConstructionOwnerPredicate.UsesConsumerGoods] is null
            && expected.Fields(factoryOwner.AsBlock().Entries, ownerScope: true, zone: null, depth: 0) == ConstructionTruth.Unknown,
            "owner country_uses_consumer_goods у factory остаётся Unknown без runtime-факта");
    }
    private sealed class RawHelperEvaluator(IReadOnlyDictionary<string, ScriptEntry> helpers, IReadOnlySet<string> zones,
        bool? ownerExists, bool? usesConsumerGoods)
    {
        public ConstructionTruth Helper(string id) => helpers.TryGetValue(id, out var entry) && entry.Block is { } body
            ? Fields(body.Entries, false, null, 0) : ConstructionTruth.Unknown;
        public ConstructionTruth Fields(IReadOnlyList<ScriptEntry> fields, bool ownerScope, string? zone, int depth)
            => Combine(fields.Select(field => Field(field, ownerScope, zone, depth + 1)), false);
        private ConstructionTruth Field(ScriptEntry field, bool ownerScope, string? zone, int depth)
        {
            if (depth > 64 || field.Operator != "=") return ConstructionTruth.Unknown;
            if (field.Block is { } block)
            {
                return field.Key switch
                {
                    "AND" => Fields(block.Entries, ownerScope, zone, depth + 1),
                    "OR" => Combine(block.Entries.Select(child => Field(child, ownerScope, zone, depth + 1)), true),
                    "hidden_trigger" => Fields(block.Entries, ownerScope, zone, depth + 1),
                    "owner" when !ownerScope => ownerExists == true ? Fields(block.Entries, true, zone, depth + 1) : ConstructionTruth.Unknown,
                    "has_zone" when !ownerScope => HasZone(block, depth + 1),
                    _ => ConstructionTruth.Unknown
                };
            }
            if (field.Key == "exists" && !ownerScope && field.Scalar == "owner")
                return ownerExists is { } exists ? (exists ? ConstructionTruth.True : ConstructionTruth.False) : ConstructionTruth.Unknown;
            if (field.Key == "country_uses_consumer_goods" && ownerScope && (field.Scalar is "yes" or "no"))
                return usesConsumerGoods is { } uses ? (uses == (field.Scalar == "yes") ? ConstructionTruth.True : ConstructionTruth.False) : ConstructionTruth.Unknown;
            if ((field.Scalar is "yes" or "no") && helpers.ContainsKey(field.Key))
            {
                var result = HelperAt(field.Key, zone, depth + 1);
                return field.Scalar == "no" ? Negate(result) : result;
            }
            return ConstructionTruth.Unknown;
        }
        private ConstructionTruth HasZone(ScriptBlock arguments, int depth)
        {
            var zone = arguments.Entries.SingleOrDefault(entry => entry.Key == "zone");
            return arguments.Entries.Count == 1 && zone is { Operator: "=", Scalar: { Length: > 0 }, Block: null }
                ? HelperAt("has_zone", zone.Scalar, depth + 1) : ConstructionTruth.Unknown;
        }
        private ConstructionTruth HelperAt(string id, string? zone, int depth)
        {
            if (depth > 64 || !helpers.TryGetValue(id, out var entry) || entry.Block is not { } body) return ConstructionTruth.Unknown;
            if (id == "has_zone") return zone is null ? ConstructionTruth.Unknown
                : zones.Contains(zone) ? ConstructionTruth.True : ConstructionTruth.False;
            return Fields(body.Entries, false, zone, depth + 1);
        }
        private static ConstructionTruth Combine(IEnumerable<ConstructionTruth> values, bool any)
        {
            var all = values.ToArray();
            if (any) return all.Contains(ConstructionTruth.True) ? ConstructionTruth.True
                : all.Contains(ConstructionTruth.Unknown) ? ConstructionTruth.Unknown : ConstructionTruth.False;
            return all.Contains(ConstructionTruth.False) ? ConstructionTruth.False
                : all.Contains(ConstructionTruth.Unknown) ? ConstructionTruth.Unknown : ConstructionTruth.True;
        }
        private static ConstructionTruth Negate(ConstructionTruth truth) => truth switch
        {
            ConstructionTruth.True => ConstructionTruth.False,
            ConstructionTruth.False => ConstructionTruth.True,
            _ => ConstructionTruth.Unknown
        };
    }
}
