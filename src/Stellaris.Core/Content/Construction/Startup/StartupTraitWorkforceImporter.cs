using System.Collections.ObjectModel;
using Stellaris.Core.Construction;
using Stellaris.Core.Content;

namespace Stellaris.Core.Content.Construction.Startup;

public sealed record StartupTraitWorkforcePlan(IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> BonusesByTemplate, ContentManifest Sources);

/// <summary>Импортирует только source trait Workforce bonuses, нужные трём organic M3 presets.</summary>
public sealed class StartupTraitWorkforceImporter
{
    private const string Suffix = "_jobs_bonus_workforce_mult";
    private const string CategorySuffix = "_cat_bonus_workforce_mult";
    private static readonly string[] Templates = ["economic", "scientific", "military"];

    public StartupTraitWorkforcePlan Import(string gameRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        var source = new ContentSession(gameRoot);
        if (source.Version != "v4.5.2") { throw new InvalidDataException("Trait Workforce import требует Stellaris 4.5.2."); }
        source.LoadFolder("common/traits");
        var result = new Dictionary<string, IReadOnlyDictionary<string, decimal>>(StringComparer.Ordinal);
        foreach (var templateId in Templates)
        {
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var traitId in template.Traits.Order(StringComparer.Ordinal))
            {
                var trait = source.Definition("common/traits", traitId).AsBlock();
                var modifiers = trait.Named("modifier").ToArray();
                if (modifiers.Length > 1 || modifiers.Any(entry => entry.Block is null))
                {
                    throw new InvalidDataException($"Unsupported modifier shape in selected trait {traitId}.");
                }
                if (modifiers.Length == 0) { continue; }
                foreach (var entry in modifiers[0].AsBlock().Entries)
                {
                    if (!entry.Key.Contains("bonus_workforce_mult", StringComparison.Ordinal)) { continue; }
                    var value = source.Number(entry);
                    if (value < 0) { throw new InvalidDataException($"Negative source trait Workforce bonus: {traitId}/{entry.Key}."); }
                    IEnumerable<string> targets;
                    if (entry.Key.EndsWith(Suffix, StringComparison.Ordinal))
                    {
                        var target = entry.Key[..^Suffix.Length];
                        targets = target switch
                        {
                            "technician" => ["technician"],
                            "researcher" => ["physicist", "biologist", "engineer"],
                            "soldier" => ["soldier"],
                            _ => throw new InvalidDataException($"Unsupported source trait job bonus {traitId}/{entry.Key}.")
                        };
                    }
                    else if (entry.Key == "worker_and_simple_drone_cat_bonus_workforce_mult")
                    {
                        targets = ["technician", "miner", "farmer", "clerk"];
                    }
                    else if (entry.Key.EndsWith(CategorySuffix, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Unsupported source trait category bonus {traitId}/{entry.Key}.");
                    }
                    else { throw new InvalidDataException($"Unknown source trait Workforce key {traitId}/{entry.Key}."); }
                    foreach (var target in targets) { rates[target] = checked(rates.GetValueOrDefault(target) + value); }
                }
            }
            result.Add(templateId, new ReadOnlyDictionary<string, decimal>(rates));
        }
        var manifest = source.Manifest("m3-start-trait-workforce-452-v1", new[]
        {
            "Только числовые workforce modifiers выбранных traits трёх обычных presets; прочие черты traits не исполняются.",
            "researcher применяется к трём импортированным обычным research jobs; worker category — к source worker jobs."
        });
        return new(new ReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>(result), manifest);
    }
}
