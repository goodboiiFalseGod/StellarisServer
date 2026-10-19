using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Проверяет привязку inline-параметров на реальном каталоге и обычном startup.
internal static class CatalogInlineSourceChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("catalog-inline-source", 1, home.Homeworld, home.Plan);
        var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
        var template = ConstructionOrdinaryTemplate.Get("economic");
        var context = content.Context(startup, template, runtime,
            M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
        check(context.SourceFacts?.OwnerAscensionPerks is { Count: 0 },
            "actual M3 startup has a known empty owner ascension-perk snapshot");

        const string id = "building_baol_organic_plant";
        const string target = "planet_farmers_food_produces_add";
        const string rawKey = "planet_$JOB$_$RESOURCE$_produces_add";
        var term = economy.Buildings[id].Terms.Single(item => item.Kind == PlanetEconomyTermKind.PlanetModifier
            && item.TargetId == target && item.Value == .25m);
        check(term.TargetId == target && term.Value == .25m,
            "Baol inline parameters bind farmer food production +.25");
        check(term.SourceKey == rawKey,
            "Baol inline target retains raw macro SourceKey");
        check(term.Provenance.RelativePath == "common/inline_scripts/buildings/planet_job_resource_produces_add.txt",
            "Baol inline target provenance points to source inline script");
        var gateText = string.Join(" ", content.RuntimeGates.GetValueOrDefault(id, ""),
            content.RuntimeGateEvidence.GetValueOrDefault(id, ""));
        check(!gateText.Contains("$JOB$", StringComparison.Ordinal)
            && !gateText.Contains("$RESOURCE$", StringComparison.Ordinal),
            "Baol has no M3 runtime gate from unresolved job/resource target macros");

        var catalog = content.Content.Catalogs[id];
        var assessment = ConstructionCatalogAssessor.Assess(catalog, id, context, content.Content.Conditions);
        check(assessment.Contributions.SelectMany(contribution => contribution.Amounts)
                .Any(amount => amount.Resource == target && amount.Value == term.Value),
            "Baol actual startup assessment retains farmer food production +.25");
        check(catalog.Definitions[id].Fields.Any(field => field.Key == "prerequisites"
                && field.Children.Any(child => child.Scalar == "tech_secrets_baol"))
            && !context.Technologies!.Contains("tech_secrets_baol")
            && assessment.Prerequisites.Truth == ConstructionTruth.False,
            "Baol source technology prerequisite remains unavailable in actual starting context");

        const string sourcePath = "common/buildings/03_resource_buildings.txt";
        var perkBranches = catalog.Definitions[id].Fields
            .Where(field => field.Key == "triggered_planet_modifier")
            .Where(field => Flatten(field.Children).Any(child => child.Key == "has_ascension_perk"
                && child.Scalar == "ap_archaeoengineers"))
            .ToArray();
        check(perkBranches.Length == 2 && perkBranches.All(field => field.Origin.Source == sourcePath),
            "Baol Archaeoengineering perk branches are retained from native building source");
        foreach (var branch in perkBranches)
        {
            var targetAmount = Flatten(branch.Children).Single(field => field.Key.StartsWith("planet_farmers_", StringComparison.Ordinal)
                && field.Key.EndsWith("_produces_add", StringComparison.Ordinal));
            var contribution = assessment.Contributions.Single(item => item.Origin.Source == sourcePath
                && item.Origin.Line == branch.Origin.Line && item.Amounts.Any(amount => amount.Resource == targetAmount.Key));
            check(contribution.Condition.Truth == ConstructionTruth.False,
                "Baol native ascension-perk contribution is false in the actual starting context: " + targetAmount.Key);
        }
    }

    private static IEnumerable<ConstructionField> Flatten(IEnumerable<ConstructionField> fields)
        => fields.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));
}
