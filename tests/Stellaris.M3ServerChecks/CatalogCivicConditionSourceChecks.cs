using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Сверяет native civic-only helpers с раздельными наборами civics реального startup.
internal static class CatalogCivicConditionSourceChecks
{
    private static readonly (string Path, string[] Ids)[] Groups =
    [
        ("common/scripted_triggers/00_scripted_triggers.txt", ["is_idyllic_bloom_empire", "is_death_cult_empire", "is_memorialist_empire", "is_relentless_industrialist_empire", "is_toxic_baths_empire", "is_memory_vault_empire", "is_astrometeorologist_empire", "is_storm_callers_empire"]),
        ("common/scripted_triggers/02_scripted_triggers_machine_age.txt", ["is_natural_design_empire", "is_guided_sapience_empire"]),
        ("common/scripted_triggers/08_scripted_triggers_shroud.txt", ["is_entropy_drinkers_empire", "is_twisted_experimenters_empire", "is_tankbound_empire"]),
        ("common/scripted_triggers/08_scripted_triggers_infernals.txt", ["is_world_forger_empire", "is_galvanic_empire"])
    ];

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("catalog-civic-conditions", 1, home.Homeworld, home.Plan);
        var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
        var context = content.Context(startup, ConstructionOrdinaryTemplate.Get("economic"), runtime,
            M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
        var facts = context.SourceFacts ?? throw new InvalidOperationException("Факты реального startup отсутствуют.");
        var template = ConstructionOrdinaryTemplate.Get("economic");
        check(facts.Civics is not null && facts.ValidCivics is not null
            && facts.Civics.SetEquals(startup.Plan.Context.Civics)
            && facts.ValidCivics.SetEquals(template.Civics),
            "факты selected и valid civics совпадают со startup/template раздельно");

        var conditions = content.Content.Conditions;
        check(conditions.ProfileId == "construction-conditions-authority-catalog-452-v4" && conditions.Definitions.Count == 64,
            "текущий authority conditions profile сохраняет civic closure среди 64 definitions");
        var source = new ContentSession(root);
        var ids = Groups.SelectMany(group => group.Ids).ToArray();
        check(ids.Length == 15 && ids.Distinct(StringComparer.Ordinal).Count() == 15 && Groups.Length == 4,
            "native inventory содержит 15 helpers из четырёх source files");
        var selectedHelpers = 0;
        var validHelpers = 0;
        foreach (var (path, groupIds) in Groups)
        {
            var parsed = source.LoadFile(path).Root;
            foreach (var id in groupIds)
            {
                var native = parsed.Named(id).Single();
                var imported = conditions.Definitions[id];
                check(imported.Kind == "ownerCondition" && imported.Origin.Source == path,
                    "native helper импортирован с правильным owner kind/source: " + id);
                var body = native.AsBlock().Entries;
                check(native.Operator == "=" && body.Count == 1 && body[0].Key == "OR" && body[0].Operator == "=" && body[0].Block is not null,
                    "native helper имеет одно OR тело: " + id);
                var leaves = body[0].AsBlock().Entries;
                check(leaves.Count != 0 && leaves.All(field => field.Operator == "=" && field.Block is null
                        && field.Scalar is { Length: > 0 } && (field.Key is "has_civic" or "has_valid_civic")),
                    "native OR состоит из literal civic predicates: " + id);
                var predicates = leaves.Select(field => field.Key).Distinct(StringComparer.Ordinal).ToArray();
                check(predicates.Length == 1, "native helper использует один тип civic membership: " + id);
                if (predicates[0] == "has_civic") selectedHelpers++;
                else validHelpers++;
                var expected = leaves.Any(field => field.Key == "has_civic"
                        ? facts.Civics!.Contains(field.Scalar!) : facts.ValidCivics!.Contains(field.Scalar!));
                var actual = ConstructionCatalogAssessor.EvaluateOwnerHelper(conditions, id, context).Truth;
                check(!expected && actual == ConstructionTruth.False,
                    "native source и evaluator подтверждают отсутствие civic helper: " + id);
            }
        }
        check(selectedHelpers == 3 && validHelpers == 12,
            "15 native helpers разделены на 3 has_civic и 12 has_valid_civic");

        var tankboundSource = source.LoadFile("common/scripted_triggers/08_scripted_triggers_shroud.txt").Root.Named("is_tankbound_empire").Single();
        var tankboundOr = tankboundSource.AsBlock().Entries;
        var tankboundLeaves = tankboundOr.Single().AsBlock().Entries;
        check(tankboundSource.Operator == "=" && tankboundOr.Count == 1 && tankboundOr[0].Key == "OR"
            && tankboundLeaves.Count == 2 && tankboundLeaves.All(leaf => leaf.Key == "has_valid_civic" && leaf.Operator == "=" && leaf.Block is null)
            && tankboundLeaves.Select(leaf => leaf.Scalar).ToHashSet(StringComparer.Ordinal).SetEquals(["civic_tankbound", "civic_tankbound_corporate"]),
            "native is_tankbound_empire — OR двух точных has_valid_civic literals");
        check(conditions.Definitions.TryGetValue("is_tankbound_empire", out var importedTankbound)
            && importedTankbound.Kind == "ownerCondition" && importedTankbound.Origin.Source == "common/scripted_triggers/08_scripted_triggers_shroud.txt",
            "tankbound helper импортирован из native shroud source");

        var district = source.LoadFile("common/districts/00_urban_districts.txt").Root.Named("district_city").Single();
        var tankboundModifier = district.AsBlock().Named("triggered_planet_modifier").Single(modifier =>
        {
            var fields = modifier.AsBlock();
            return fields.Named("modifier").Any(effect => effect.Block is { } effectBlock
                    && effectBlock.Named("job_production_overseer_add").Any(job => job.Operator == "=" && job.Scalar == "100" && job.Block is null))
                && fields.Named("potential").Any(potential => potential.Block is { } potentialBlock
                    && potentialBlock.Named("owner").Any(owner => owner.Block is { } ownerBlock
                        && ownerBlock.Named("is_tankbound_empire").Any(helper => helper.Operator == "=" && helper.Scalar == "yes" && helper.Block is null)));
        });
        check(tankboundModifier.Key == "triggered_planet_modifier", "native district_city overseer +100 использует is_tankbound_empire");
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var actualHome = startups.Select(templateId, 123);
            var actualStartup = M3StartupColony.Create("catalog-tankbound-" + templateId, 1, actualHome.Homeworld, actualHome.Plan);
            var actualRuntime = content.Create(actualStartup, profile.CreateConstructionContributions(actualStartup));
            var actualContext = content.Context(actualStartup, ConstructionOrdinaryTemplate.Get(templateId), actualRuntime,
                M3ConstructionContent.FromBinding.ActingCountry(actualStartup.Plan.Context.IsNomadic));
            var actualFacts = actualContext.SourceFacts ?? throw new InvalidOperationException("Факты реального startup отсутствуют.");
            check(actualFacts.ValidCivics is not null && actualFacts.ValidCivics.SetEquals(ConstructionOrdinaryTemplate.Get(templateId).Civics)
                && ConstructionCatalogAssessor.EvaluateOwnerHelper(conditions, "is_tankbound_empire", actualContext).Truth == ConstructionTruth.False,
                "tankbound helper false по valid civics реального шаблона " + templateId);
        }
    }
}
