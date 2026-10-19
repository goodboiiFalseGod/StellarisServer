using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Сверяет четыре native tradition helpers и их полный импорт на реальных M3 шаблонах.
internal static class CatalogTraditionHelperSourceChecks
{
    private static readonly (string Path, string[] Ids)[] Groups =
    [
        ("common/scripted_triggers/05_scripted_triggers_biogenesis.txt", ["has_cloning_tradition", "has_genetic_stage_1_tradition", "has_purity_heightened_attributes_tradition"]),
        ("common/scripted_triggers/08_scripted_triggers_shroud.txt", ["has_psi_corps_tradition"])
    ];

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var technologies = new TechnologyImporter().Import(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, technologies.CategoryParents);
        var conditions = content.Content.Conditions;
        var ids = Groups.SelectMany(group => group.Ids).ToArray();
        check(ids.Length == 4 && ids.Distinct(StringComparer.Ordinal).Count() == 4
            && conditions.ProfileId == "construction-conditions-authority-catalog-452-v4" && conditions.Definitions.Count == 64,
            "authority profile v4 содержит ровно четыре новых tradition helper среди 64 definitions");
        var source = new ContentSession(root);
        foreach (var (path, helperIds) in Groups)
        {
            var raw = source.LoadFile(path);
            check(conditions.Files.Any(file => file.RelativePath == path), "manifest включает native helper source " + path);
            foreach (var id in helperIds)
            {
                var native = raw.Root.Named(id).Single();
                var imported = conditions.Definitions[id];
                check(imported.Kind == "ownerCondition" && imported.Origin.Source == path && imported.Origin.Line == native.Line,
                    "helper сохранён с owner kind и точным source origin: " + id);
                check(native.Operator == "=" && native.Block is not null && native.Block.Entries.Count > 0
                    && native.Block.Entries.Count == imported.Fields.Count
                    && native.Block.Entries.Select((entry, index) => Matches(entry, imported.Fields[index])).All(match => match),
                    "полное упорядоченное native тело и provenance импортированы: " + id);
            }
        }
        var biogenesis = source.LoadFile(Groups[0].Path).Root;
        var shroud = source.LoadFile(Groups[1].Path).Root;
        var stage1 = biogenesis.Named("has_genetic_stage_1_tradition").Single().AsBlock().Entries;
        var purity = biogenesis.Named("has_purity_heightened_attributes_tradition").Single().AsBlock().Entries;
        check(stage1.Single().Key == "OR" && stage1.Single().AsBlock().Entries.Count == 3
            && purity.Any(field => field.Key == "has_country_flag" && field.Scalar == "genetic_ascension_stage_1_purity")
            && purity.Any(field => field.Key == "has_genetic_stage_1_tradition" && field.Scalar == "yes")
            && shroud.Named("has_psi_corps_tradition").Single().AsBlock().Entries.Single().Key == "OR",
            "native helper closure сохраняет OR, country flag и вложенную stage-1 зависимость");

        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(templateId, 123);
            var startup = M3StartupColony.Create("catalog-tradition-" + templateId, 1, home.Homeworld, home.Plan);
            var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var context = content.Context(startup, template, runtime,
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
            var facts = context.SourceFacts ?? throw new InvalidOperationException("Факты actual M3 startup отсутствуют.");
            check(facts.OwnerHeldTraditions is { Count: 0 } && facts.OwnerFlags is { Count: 0 },
                templateId + ": M3 передаёт известные пустые held-traditions/owner-flags snapshots");
            foreach (var id in ids)
                check(ConstructionCatalogAssessor.EvaluateOwnerHelper(conditions, id, context).Truth == ConstructionTruth.False,
                    templateId + ": native tradition helper false в реальном startup: " + id);
        }
        source.VerifyStable();
    }

    private static bool Matches(ScriptEntry raw, Stellaris.Core.Content.Construction.ConstructionField imported) =>
        raw.Key == imported.Key && raw.Operator == imported.Operator && raw.Scalar == imported.Scalar
        && raw.Source == imported.Origin.Source && raw.Line == imported.Origin.Line && raw.Column == imported.Origin.Column
        && (raw.Block?.Entries.Count ?? 0) == imported.Children.Count
        && (raw.Block is null || raw.Block.Entries.Select((entry, index) => Matches(entry, imported.Children[index])).All(match => match));
}
