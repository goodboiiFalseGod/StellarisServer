using System.Collections.Frozen;
using Stellaris.Core.Construction;
using Stellaris.Core.Economy;

namespace Stellaris.Core.Content.Construction.Startup;

public sealed class StartupFounderSpeciesCatalog
{
    public IReadOnlyDictionary<string, PopulationSpeciesDefinition> Definitions { get; }
    public ContentManifest Sources { get; }
    internal StartupFounderSpeciesCatalog(Dictionary<string, PopulationSpeciesDefinition> definitions, ContentManifest sources)
    { Definitions = definitions.ToFrozenDictionary(StringComparer.Ordinal); Sources = sources; }
}

/// <summary>Source recipe видов трёх пресетов; actual species создаётся отдельно в мире.</summary>
public sealed class StartupFounderSpeciesImporter
{
    public StartupFounderSpeciesCatalog Import(string gameRoot)
    {
        var source = new ContentSession(gameRoot);
        if (source.Version != "v4.5.2") throw new InvalidDataException("Founder species import requires Stellaris 4.5.2.");
        var classes = source.LoadFile("common/species_classes/00_species_classes.txt").Root;
        source.LoadFolder("common/traits");
        var result = new Dictionary<string, PopulationSpeciesDefinition>(StringComparer.Ordinal);
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            if (template.SpeciesClassId != "HUM") throw new InvalidDataException("Founder species selection is not approved.");
            var definition = classes.Named(template.SpeciesClassId).Single().AsBlock();
            var archetype = definition.Required("archetype");
            if (archetype.Operator != "=" || archetype.AsScalar() != "BIOLOGICAL") throw new InvalidDataException("HUM source archetype changed.");
            var mandatory = definition.Named("trait").ToArray();
            if (mandatory.Length == 0 || mandatory.Any(trait => trait.Operator != "=" || trait.Scalar is null || trait.Block is not null))
                throw new InvalidDataException("HUM class traits are unresolved.");
            var traits = mandatory.Select(trait => trait.AsScalar()).Concat(template.Traits).ToHashSet(StringComparer.Ordinal);
            foreach (var trait in traits) _ = source.Definition("common/traits", trait).AsBlock();
            result.Add(templateId, new(template.SpeciesClassId, archetype.AsScalar(), traits));
        }
        return new(result, source.Manifest("m3-founder-species-HUM-452-v1",
            ["HUM выбран человеком для трёх ordinary пресетов; native class traits плюс выбранные traits. Gene modification и дополнительные виды не применяются."]));
    }
}
