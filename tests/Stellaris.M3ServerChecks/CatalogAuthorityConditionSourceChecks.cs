using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Сверяет native authority helpers с raw source и фактическим M3 startup.
internal static class CatalogAuthorityConditionSourceChecks
{
    private const string SourcePath = "common/scripted_triggers/00_scripted_triggers.txt";

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("catalog-authority-conditions", 1, home.Homeworld, home.Plan);
        var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
        var template = ConstructionOrdinaryTemplate.Get("economic");
        var context = content.Context(startup, template, runtime,
            M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
        var facts = context.SourceFacts ?? throw new InvalidOperationException("Факты реального startup отсутствуют.");
        check(facts.OwnerAuthorityPredicates is { Count: 2 }
            && facts.OwnerAuthorityPredicates.TryGetValue("auth_corporate", out var corporate)
            && corporate == (template.AuthorityId == "auth_corporate")
            && facts.OwnerAuthorityPredicates.TryGetValue("auth_machine_intelligence", out var machine)
            && machine == (template.AuthorityId == "auth_machine_intelligence"),
            "частичный authority snapshot содержит только два native результата текущего шаблона");
        check(facts.Civics is not null && facts.Civics.SetEquals(startup.Plan.Context.Civics),
            "selected civics совпадают с фактическим startup");

        var conditions = content.Content.Conditions;
        check(conditions.ProfileId == "construction-conditions-authority-catalog-452-v4" && conditions.Definitions.Count == 64,
            "импортированы authority helpers в профиль из 64 definitions");
        var source = new ContentSession(root).LoadFile(SourcePath).Root;
        var mega = source.Named("is_megacorp").Single();
        var megaBody = mega.AsBlock().Entries;
        check(mega.Operator == "=" && megaBody.Count == 1 && megaBody[0].Key == "has_authority"
            && megaBody[0].Operator == "=" && megaBody[0].Scalar == "auth_corporate" && megaBody[0].Block is null,
            "native is_megacorp точно проверяет auth_corporate");
        var corporateHelper = source.Named("has_corporate_government").Single();
        var corporateBody = corporateHelper.AsBlock().Entries;
        check(corporateHelper.Operator == "=" && corporateBody.Count == 1 && corporateBody[0].Key == "OR"
            && corporateBody[0].Block is not null, "native corporate government использует одно OR");
        var leaves = corporateBody[0].AsBlock().Entries;
        check(leaves.Count == 2
            && leaves.Any(field => field.Key == "is_megacorp" && field.Operator == "=" && field.Scalar == "yes" && field.Block is null)
            && leaves.Any(field => field.Key == "has_civic" && field.Operator == "=" && field.Scalar == "civic_galactic_sovereign_megacorp" && field.Block is null),
            "native corporate OR содержит megacorp и точный civic literal");

        var machineHelper = source.Named("is_machine_empire").Single();
        var machineBody = machineHelper.AsBlock().Entries;
        check(machineHelper.Operator == "=" && machineBody.Count == 1 && machineBody[0].Key == "has_authority"
            && machineBody[0].Operator == "=" && machineBody[0].Scalar == "auth_machine_intelligence" && machineBody[0].Block is null,
            "native is_machine_empire точно проверяет auth_machine_intelligence");

        foreach (var id in new[] { "is_megacorp", "has_corporate_government", "is_machine_empire" })
        {
            var imported = conditions.Definitions[id];
            check(imported.Kind == "ownerCondition" && imported.Origin.Source == SourcePath,
                "native authority helper импортирован с owner kind/source: " + id);
            check(ConstructionCatalogAssessor.EvaluateOwnerHelper(conditions, id, context).Truth == ConstructionTruth.False,
                "authority helper false в реальном non-corporate startup: " + id);
        }
    }
}
