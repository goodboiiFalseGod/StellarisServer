using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

/// <summary>RingSuppliedSeedV1: отдельный Script0; native global interleaving не заявляется.</summary>
public static class GalaxyRings
{
    public static bool Resolve(GalaxyRuntimeCatalog catalog, GalaxyNumericValues numeric, GalaxyLocalBody body, Func<uint> next)
    {
        if (body.Kind == GalaxyBodyKind.Star) return false;
        var explicitFields = body.Source.Entries.Where(e => e.Key == "has_ring").ToArray();
        if (explicitFields.Length > 0)
        {
            var field = explicitFields[^1];
            if (field.Operator != "=" || field.Value.Kind != GalaxySyntaxKind.Scalar || field.Value.Text is not ("yes" or "no"))
                throw new GalaxyStructureException("invalid-ring-field", "has_ring требует yes/no.", field.Location);
            return field.Value.Text == "yes";
        }
        var definition = catalog.Lookup(GalaxyRuntimeFamily.PlanetClass, body.ClassId);
        if (definition.Status != GalaxyLookupStatus.Unique)
            throw new GalaxyStructureException("invalid-ring-class", body.ClassId, body.Source.Location);
        var source = definition.Candidates[0];
        var chances = source.Syntax.Value.Entries.Where(e => e.Key == "chance_of_ring").ToArray();
        // Локальная policy для source pc_broken: отсутствие вероятности трактуем как0,
        // сохраняя общий draw стадии rings; это не утверждение о native class default.
        if (body.ClassId == "pc_broken" && chances.Length == 0) return Test(0, next());
        if (chances.Length != 1 || chances[0].Operator != "=")
            throw new GalaxyStructureException("invalid-ring-chance", body.ClassId + ": chance_of_ring отсутствует/неоднозначен.", source.Location);
        var chance = numeric.Evaluate(chances[0].Value, source.File.Path);
        if (chance < 0 || chance > 1 || decimal.Truncate(chance * 100000) != chance * 100000)
            throw new GalaxyStructureException("invalid-ring-chance", body.ClassId + ": требуется fixed5 0..1.", chances[0].Location);
        return Test(chance, next());
    }

    public static bool Test(decimal chance, uint draw)
    {
        if (draw > int.MaxValue || chance < 0 || chance > 1 || decimal.Truncate(chance * 100000) != chance * 100000)
            throw new ArgumentOutOfRangeException(nameof(chance));
        return draw % 100000 < chance * 100000;
    }
}
