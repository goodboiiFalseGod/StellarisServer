using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;
using System.Security.Cryptography;
using System.Reflection;

// Сверяет actual HUM recipes, runtime species identity и строго доступные source predicates.
internal static class HumSpeciesSourceChecks
{
    private const string ClassPath = "common/species_classes/00_species_classes.txt";
    private const string TriggerPath = "common/scripted_triggers/00_scripted_triggers.txt";
    private const string ShroudTriggerPath = "common/scripted_triggers/08_scripted_triggers_shroud.txt";

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var importer = new StartupFounderSpeciesImporter();
        var recipes = importer.Import(root);
        var manifestSession = new ContentSession(root);
        var classes = manifestSession.LoadFile(ClassPath).Root;
        manifestSession.LoadFolder("common/traits");
        var nativeHum = classes.Named("HUM").Single().AsBlock();
        check(recipes.Sources.Version == "v4.5.2" && recipes.Sources.Profile == "m3-founder-species-HUM-452-v1"
            && recipes.Definitions.Keys.Order(StringComparer.Ordinal).SequenceEqual(new[] { "economic", "military", "scientific" }, StringComparer.Ordinal)
            && recipes.Sources.Files.Any(file => file.Path == ClassPath),
            "manifest фиксирует HUM для трёх реальных ordinary пресетов и native class source");
        check(recipes.Sources.Files.All(file =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            return bytes.LongLength == file.Bytes && SHA256.HashData(bytes).SequenceEqual(Convert.FromHexString(file.Sha256));
        }), "manifest SHA-256 совпадает с actual source bytes для каждого импортированного файла");
        check(nativeHum.Required("archetype").AsScalar() == "BIOLOGICAL"
            && recipes.Definitions.Values.All(definition => definition.ClassId == "HUM" && definition.ArchetypeId == "BIOLOGICAL")
            && recipes.Definitions.Values.All(definition => nativeHum.Named("trait").All(field => definition.Traits.Contains(field.AsScalar()))),
            "native HUM archetype и обязательные class traits сохранены в каждом recipe");
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var selected = recipes.Definitions[templateId];
            check(template.SpeciesClassId == "HUM" && template.Traits.All(selected.Traits.Contains)
                && selected.Traits.SetEquals(nativeHum.Named("trait").Select(field => field.AsScalar()).Concat(template.Traits)),
                templateId + ": выбранные traits дополнены полным native HUM набором");
        }
        manifestSession.VerifyStable();

        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var technologies = new TechnologyImporter().Import(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, technologies.CategoryParents, recipes);
        var source = new ContentSession(root);
        var nativeTriggers = source.LoadFile(TriggerPath).Root;
        var nativeShroudTriggers = source.LoadFile(ShroudTriggerPath).Root;
        var (sourceNode, anyOwnedNode) = nativeTriggers.Entries.SelectMany(entry => FindAnyOwned(entry).Select(node => (entry, node))).First();
        check(sourceNode.Key == "country_has_shackled_robots" && anyOwnedNode.Operator == "="
            && anyOwnedNode.Block?.Entries.Single() is { Key: "has_trait", Scalar: "trait_mechanical" }
            && source.Manifest("hum-native-trigger-evidence", []).Files.Any(file => file.Path == TriggerPath),
            "native country_has_shackled_robots содержит any_owned_species/has_trait с manifest по исходным bytes");
        var biologicalHelper = nativeShroudTriggers.Named("founder_species_is_biological").Single();
        var lithoidHelper = nativeShroudTriggers.Named("founder_species_is_lithoid").Single();
        var shroudManifest = source.Manifest("hum-founder-species-predicates", []);
        var shroudFile = shroudManifest.Files.Single(file => file.Path == ShroudTriggerPath);
        var shroudBytes = File.ReadAllBytes(Path.Combine(root, ShroudTriggerPath.Replace('/', Path.DirectorySeparatorChar)));
        check(shroudFile.Bytes == shroudBytes.LongLength && SHA256.HashData(shroudBytes).SequenceEqual(Convert.FromHexString(shroudFile.Sha256))
            && biologicalHelper.Source == ShroudTriggerPath && biologicalHelper.Block is not null
            && biologicalHelper.Block.Entries.Single().Key == "founder_species"
            && biologicalHelper.Block.Entries.Single().Block?.Entries.Single().Key == "OR"
            && biologicalHelper.Block.Entries.Single().Block?.Entries.Single().Block?.Entries.Select(entry => entry.Key)
                .SequenceEqual(new[] { "uplift_is_archetype", "is_archetype" }) == true,
            "native founder_species_is_biological сохранён с полной source provenance и decoded manifest SHA");

        var world = profile.CreateWorld();
        var countries = new Dictionary<string, (EntityId Country, EntityId Planet, EntityId Species)>();
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(templateId, 123);
            var startup = M3StartupColony.Create("hum-species-" + templateId, 1, home.Homeworld, home.Plan);
            var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
            var country = world.CreateCountry();
            var planet = world.CreatePlanet(country);
            profile.InitializeColony(world, country, planet, templateId, home.Plan);
            var founder = world.GetFounderSpecies(country) ?? throw new InvalidOperationException("Actual founder species отсутствует.");
            var groups = world.GetOwnedSpeciesSnapshot(country) ?? throw new InvalidOperationException("Actual owner groups не связаны с species.");
            check(groups.Count == 1 && groups[0].Id == founder.Id,
                templateId + ": owner species snapshot построен из population groups и содержит actual founder entity");
            var state = world.StateHash();
            var fork = world.Fork();
            check(fork.StateHash() == state && fork.GetFounderSpecies(country)?.Id == founder.Id
                && fork.GetOwnedSpeciesSnapshot(country)?.Single().Definition.Traits.SetEquals(founder.Definition.Traits) == true,
                templateId + ": Fork сохраняет actual entity identity и полный генотип");
            check(Convert.FromHexString(fork.StateHash()).SequenceEqual(Convert.FromHexString(state)),
                templateId + ": canonical state hash совпадает по decoded SHA bytes");

            var other = world.CreateCountry();
            var otherPlanet = world.CreatePlanet(other);
            var beforeForeign = world.StateHash();
            var rejected = false;
            try { world.AddGroup(otherPlanet, 1, 1, founder.Id); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && world.StateHash() == beforeForeign,
                templateId + ": чужой species отвергнут до создания группы и изменения состояния");

            var context = content.Context(startup, ConstructionOrdinaryTemplate.Get(templateId), runtime,
                M3ConstructionContent.FromBinding.ActingCountry(false), ownerSpecies: groups, ownerFounderSpeciesId: founder.Id,
                ownerCountryEntityId: country);
            check(context.SourceFacts?.OwnerSpecies?.Single().Id == founder.Id
                && context.SourceFacts.OwnerFounderSpeciesId == founder.Id,
                templateId + ": construction context получает actual group species и founder identity");
            countries.Add(templateId, (country, planet, founder.Id));
        }
        var hash = world.StateHash();
        var forkAll = world.Fork();
        check(Convert.FromHexString(forkAll.StateHash()).SequenceEqual(Convert.FromHexString(hash)),
            "общий Fork сохраняет три изолированных HUM founder species и canonical hash");
        var firstSpecies = forkAll.GetFounderSpecies(countries["economic"].Country)!;
        check(forkAll.GetOwnedSpeciesSnapshot(countries["scientific"].Country)?.All(item => item.OwnerCountry == countries["scientific"].Country) == true
            && firstSpecies.OwnerCountry == countries["economic"].Country,
            "species identities и group ownership изолированы между тремя странами");
        var crossOwnerRejected = false;
        try
        {
            _ = BuildContext(countries["economic"], world.GetOwnedSpeciesSnapshot(countries["scientific"].Country),
                world.GetFounderSpecies(countries["scientific"].Country)?.Id);
        }
        catch (ArgumentException) { crossOwnerRejected = true; }
        check(crossOwnerRejected && world.StateHash() == hash,
            "context отклоняет species snapshot другой страны до создания мутации мира");

        // generic profile without recipe обязан оставить species context неизвестным.
        var genericProfile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, technologies.CategoryParents);
        var genericWorld = genericProfile.CreateWorld();
        var genericCountry = genericWorld.CreateCountry();
        var genericPlanet = genericWorld.CreatePlanet(genericCountry);
        var genericHome = startups.Select("economic", 123);
        genericProfile.InitializeColony(genericWorld, genericCountry, genericPlanet, "economic", genericHome.Plan);
        check(genericWorld.GetFounderSpecies(genericCountry) is null && genericWorld.GetOwnedSpeciesSnapshot(genericCountry) is null,
            "generic профиль без recipe сохраняет unbound species snapshot как Unknown");

        var authorityImporter = new ConstructionDefinitionImporter();
        var ownerConditions = await authorityImporter.ImportAuthorityConditionCatalogAsync(root);
        var nativeSpeciesCondition = ToConstructionField(anyOwnedNode);
        check(nativeSpeciesCondition.Key == "any_owned_species" && nativeSpeciesCondition.Origin.Source == TriggerPath
            && nativeSpeciesCondition.Origin.Line == anyOwnedNode.Line && nativeSpeciesCondition.Children.Single().Key == "has_trait"
            && nativeSpeciesCondition.Children.Single().Scalar == "trait_mechanical",
            "source AST conversion сохраняет exact any_owned_species/has_trait и provenance");
        var evaluated = ConstructionCatalogAssessor.EvaluateOwnerSourceCondition([nativeSpeciesCondition],
            BuildContext(countries["economic"]), ownerConditions);
        check(evaluated.Truth == ConstructionTruth.False,
            "native any_owned_species/has_trait false для HUM founder с actual group binding");
        var missingBinding = ConstructionCatalogAssessor.EvaluateOwnerSourceCondition([nativeSpeciesCondition],
            BuildContext(countries["economic"], supplySpecies: false), ownerConditions);
        check(missingBinding.Truth == ConstructionTruth.Unknown,
            "тот же native AST остаётся Unknown без owner species snapshot/founder binding");
        var biologicalFields = biologicalHelper.Block!.Entries.Select(ToConstructionField).ToArray();
        var lithoidFields = lithoidHelper.Block!.Entries.Select(ToConstructionField).ToArray();
        check(biologicalFields.Length == biologicalHelper.Block.Entries.Count
            && biologicalFields.Single().Children.Single().Children.Count == 2
            && biologicalFields.Single().Origin.Source == ShroudTriggerPath,
            "полное founder species biological AST рекурсивно перенесено без потери source origin");
        check(ConstructionCatalogAssessor.EvaluateOwnerSourceCondition(biologicalFields,
            BuildContext(countries["economic"]), ownerConditions).Truth == ConstructionTruth.True,
            "native founder_species_is_biological true для actual HUM founder species");
        check(ConstructionCatalogAssessor.EvaluateOwnerSourceCondition(biologicalFields,
            BuildContext(countries["economic"], supplySpecies: false), ownerConditions).Truth == ConstructionTruth.Unknown,
            "native biological helper остаётся Unknown без founder species binding");
        check(ConstructionCatalogAssessor.EvaluateOwnerSourceCondition(lithoidFields,
            BuildContext(countries["economic"]), ownerConditions).Truth == ConstructionTruth.Unknown,
            "native lithoid helper остаётся Unknown при неподдержанном uplift_is_archetype");
        source.VerifyStable();

        ConstructionContext BuildContext((EntityId Country, EntityId Planet, EntityId Species) binding,
            IReadOnlyList<PopulationSpeciesSnapshot>? ownerSpecies = null, EntityId? founderSpeciesId = null, bool supplySpecies = true)
        {
            var home = startups.Select("economic", 123);
            var startup = M3StartupColony.Create("hum-species-context", 1, home.Homeworld, home.Plan);
            var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
            if (supplySpecies)
            {
                ownerSpecies ??= world.GetOwnedSpeciesSnapshot(binding.Country);
                founderSpeciesId ??= world.GetFounderSpecies(binding.Country)?.Id;
            }
            return content.Context(startup, ConstructionOrdinaryTemplate.Get("economic"), runtime,
                M3ConstructionContent.FromBinding.ActingCountry(false), ownerSpecies: ownerSpecies,
                ownerFounderSpeciesId: founderSpeciesId, ownerCountryEntityId: binding.Country);
        }

        static IEnumerable<ScriptEntry> FindAnyOwned(ScriptEntry entry)
        {
            if (entry.Key == "any_owned_species") yield return entry;
            if (entry.Block is null) yield break;
            foreach (var child in entry.Block.Entries)
            foreach (var found in FindAnyOwned(child)) yield return found;
        }

        static ConstructionField ToConstructionField(ScriptEntry entry)
        {
            var constructor = typeof(ConstructionField).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, [typeof(string), typeof(string), typeof(string), typeof(IEnumerable<ConstructionField>),
                    typeof(ConstructionOrigin), typeof(IEnumerable<ConstructionOrigin>)], modifiers: null)
                ?? throw new MissingMethodException("ConstructionField source constructor is unavailable.");
            var children = entry.Block?.Entries.Select(ToConstructionField).ToArray() ?? [];
            return (ConstructionField)constructor.Invoke([entry.Key, entry.Operator, entry.Scalar, children,
                new ConstructionOrigin(entry.Source, entry.Line, entry.Column), Array.Empty<ConstructionOrigin>()]);
        }
    }
}
