using Stellaris.Core.Content;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Construction;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Сверяет классификацию storage только с реальным native resource source.
internal static class ResourceStorageSourceChecks
{
    private const string ResourcePath = "common/strategic_resources/00_strategic_resources.txt";

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var catalog = await new PlanetEconomyImporter().ImportAsync(root);
        var source = new ContentSession(root).LoadFile(ResourcePath).Root;
        var header = File.ReadAllText(System.IO.Path.Combine(root, ResourcePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        var documentedFlags = header.Split('\n').Where(line => line.Contains("fixed_max_amount = yes/no", StringComparison.Ordinal)
            || line.Contains("special_max_amount = yes/no", StringComparison.Ordinal)).ToArray();
        check(documentedFlags.Length == 2 && documentedFlags.All(line => line.Contains("default no", StringComparison.Ordinal)),
            "native resource header documents fixed/special flags and their no defaults");

        var manifest = catalog.Sources.Single(file => file.RelativePath == ResourcePath);
        CheckClassification("influence", PlanetEconomyMaximumKind.Fixed);
        CheckClassification("minor_artifacts", PlanetEconomyMaximumKind.Special);
        CheckClassification("energy", PlanetEconomyMaximumKind.Generic);
        CheckClassification("food", PlanetEconomyMaximumKind.Generic);
        CheckClassification("alloys", PlanetEconomyMaximumKind.Generic);
        CheckDynamic("food");
        CheckDynamic("alloys");
        await CheckFacultyCapacity();

        async Task CheckFacultyCapacity()
        {
            var content = await M3ConstructionContent.ImportAsync(root, catalog);
            var startups = await M3StartupCatalog.ImportAsync(root);
            var profile = new M3BaselineEconomyProfile(catalog, new StartupPopulationResourceImporter().Import(root),
                new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
            var definition = catalog.Buildings["building_archaeostudies_faculty"];
            var raw = new ContentSession(root).LoadFile(definition.Provenance.RelativePath).Root.Named(definition.Id).Single().AsBlock();
            var direct = raw.Named("country_modifier").Single().AsBlock();
            var baseAddition = decimal.Parse(direct.Named("country_resource_max_minor_artifacts_add").Single().AsScalar(), System.Globalization.CultureInfo.InvariantCulture);
            var storageBranch = raw.Named("triggered_country_modifier").Single(block => block.AsBlock().Named("country_resource_max_minor_artifacts_add").Any()).AsBlock();
            var perk = storageBranch.Named("potential").Single().AsBlock().Named("owner").Single().AsBlock().Named("has_ascension_perk").Single();
            check(baseAddition == 2000 && storageBranch.Named("country_resource_max_minor_artifacts_add").Single().AsScalar() == "3000"
                && perk.Operator == "=" && perk.Scalar == "ap_archaeoengineers", "faculty сохраняет native +2000 и условные +3000, не fixed итог");
            foreach (var templateId in new[] { "economic", "scientific", "military" })
            {
                var home = startups.Select(templateId, 123);
                var startup = M3StartupColony.Create("faculty-capacity-source-" + templateId, 1, home.Homeworld, home.Plan);
                var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
                var template = ConstructionOrdinaryTemplate.Get(templateId);
                var context = content.Context(startup, template, runtime,
                    M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
                var capacity = profile.ResolveCountryResourceCapacityAdditions(definition.Id, context, content.Content.Conditions);
                check(context.SourceFacts?.OwnerAscensionPerks is { Count: 0 } && capacity.Count == 1
                    && capacity["minor_artifacts"] == baseAddition, templateId + ": реальный owner без perks даёт только native special вклад faculty");
                var silo = profile.ResolveCountryResourceCapacityAdditions("building_resource_silo", context, content.Content.Conditions);
                check(!silo.ContainsKey("minor_artifacts") && !silo.ContainsKey("influence") && silo["energy"] == 5000,
                    templateId + ": generic silo не увеличивает special/fixed cap");
                var quote = content.Content.Quote(definition.Id, context, 0, 0);
                check(quote.Quote is null && quote.Code == "definition-economy-scope-unresolved",
                    templateId + ": storage consumer не объявляет country fields полностью consumed и не открывает faculty");
                var world = profile.CreateWorld();
                var country = world.CreateCountry();
                var planet = world.CreatePlanet(country);
                profile.InitializeColony(world, country, planet, templateId, home.Plan);
                profile.RefreshInstalledCountryResourceCapacities(world, planet, runtime, context, content.Content.Conditions);
                check(world.GetEffectiveResourceCapacity(country, "minor_artifacts") == catalog.Resources["minor_artifacts"].SourceMaximum,
                    templateId + ": неустановленное faculty не меняет actual owner cap");
            }
            var unresolved = false;
            try { _ = profile.ResolveCountryResourceCapacityAdditions(definition.Id, null, content.Content.Conditions); }
            catch (InvalidDataException exception) when (exception.Message == "faculty-country-capacity-unresolved") { unresolved = true; }
            check(unresolved, "отсутствие actual context не подменяется нулевым/ложным potential faculty");
        }

        void CheckClassification(string id, PlanetEconomyMaximumKind expected)
        {
            var definition = source.Named(id).Single();
            var resource = catalog.Resources[id];
            var fixedFlag = definition.AsBlock().Optional("fixed_max_amount");
            var specialFlag = definition.AsBlock().Optional("special_max_amount");
            var native = expected switch
            {
                PlanetEconomyMaximumKind.Fixed => fixedFlag is { Operator: "=", Scalar: "yes", Block: null } && specialFlag is null,
                PlanetEconomyMaximumKind.Special => specialFlag is { Operator: "=", Scalar: "yes", Block: null } && fixedFlag is null,
                _ => fixedFlag is null && specialFlag is null
            };
            check(native && resource.MaximumKind == expected && resource.StoragePolicy == PlanetEconomyStoragePolicy.SourceCapped,
                $"{id} сохраняет native maximum classification {expected} без смены storage policy");
        }

        void CheckDynamic(string id)
        {
            var definition = source.Named(id).Single();
            var raw = definition.AsBlock().Named("dynamic_capacity").Single();
            var resource = catalog.Resources[id];
            check(raw.Block is not null && resource.DynamicCapacity is { } dynamic
                && Matches(dynamic, raw, manifest.Sha256)
                && dynamic.Provenance.RelativePath == ResourcePath
                && dynamic.Provenance.Sha256 == manifest.Sha256,
                $"{id} сохраняет raw dynamic_capacity дерево, leaf locations и manifest hash");
        }
    }

    private static bool Matches(PlanetEconomyCondition condition, ScriptEntry raw, string sha256)
        => condition.Key == raw.Key && condition.Operator == raw.Operator && condition.Value == raw.Scalar
            && condition.Provenance.RelativePath == ResourcePath && condition.Provenance.Sha256 == sha256
            && condition.Provenance.Line == raw.Line && condition.Provenance.Column == raw.Column
            && condition.Children.Count == (raw.Block?.Entries.Count ?? 0)
            && condition.Children.Zip(raw.Block?.Entries ?? []).All(pair => Matches(pair.First, pair.Second, sha256));
}
