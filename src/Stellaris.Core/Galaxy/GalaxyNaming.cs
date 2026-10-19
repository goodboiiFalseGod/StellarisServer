using System.Globalization;

namespace Stellaris.Core.Galaxy;

public sealed record GalaxyNamingResult(IReadOnlyList<GalaxyWorldSystem> Systems, IReadOnlyList<GalaxyWorldBody> Bodies,
    IReadOnlyList<string> Diagnostics, long Draws);

/// <summary>Детерминированная политика представления; нативный allocator имён не заявляется.</summary>
public static class GalaxyNaming
{
    public static GalaxyNamingResult Assign(IReadOnlyList<GalaxyWorldSystem> systems, IReadOnlyList<GalaxyWorldBody> bodies,
        IReadOnlyList<string> sourceNames, Func<uint> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (sourceNames.Count == 0 || sourceNames.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Для генерации имён требуется непустой star_names pool.");
        var explicitNames = systems.Select(s => s.Name).Concat(bodies.Select(b => b.Name))
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToHashSet(StringComparer.Ordinal);
        var pool = sourceNames.Distinct(StringComparer.Ordinal).Where(n => !explicitNames.Contains(n)).ToList();
        var used = new HashSet<string>(explicitNames, StringComparer.Ordinal);
        used.UnionWith(sourceNames);
        var namedSystems = new List<GalaxyWorldSystem>(); var namedBodies = new List<GalaxyWorldBody>();
        var diagnostics = new List<string>(); long draws = 0; var fallbackOrdinal = 0;
        foreach (var system in systems)
        {
            var local = bodies.Where(b => b.SystemId == system.Id).ToArray();
            var primary = local.FirstOrDefault(b => b.Kind == GalaxyBodyKind.Star);
            var explicitSystem = Present(system.Name) ?? Present(primary?.Name);
            var key = explicitSystem;
            if (key is null && pool.Count > 0)
            {
                var index = (int)(next() % (uint)pool.Count); draws++; key = pool[index];
                pool[index] = pool[^1]; pool.RemoveAt(pool.Count - 1);
            }
            if (key is null)
            {
                do { key = "Система " + (++fallbackOrdinal).ToString(CultureInfo.InvariantCulture); } while (!used.Add(key));
                diagnostics.Add("name-pool-exhausted:" + system.Id);
            }
            used.Add(key);
            var systemFallback = explicitSystem ?? Readable(key);
            namedSystems.Add(system with { Name = systemFallback, NameKey = key, NameSuffix = "" });
            var resolved = new Dictionary<string, GalaxyWorldBody>(StringComparer.Ordinal);
            var siblings = new Dictionary<(string Parent, GalaxyBodyKind Kind), int>(); var starOrdinal = 0;
            foreach (var body in local)
            {
                var group = (body.ParentId ?? "", body.Kind);
                var ordinal = siblings.GetValueOrDefault(group) + 1; siblings[group] = ordinal;
                var literal = Present(body.Name); var bodyKey = literal; var suffix = "";
                var fallback = systemFallback;
                if (body.Kind == GalaxyBodyKind.Star) starOrdinal++;
                if (bodyKey is null)
                {
                    bodyKey = key;
                    if (body.Kind == GalaxyBodyKind.Star)
                        suffix = starOrdinal == 1 ? "" : " " + Letters(starOrdinal, true);
                    else if (body.Kind == GalaxyBodyKind.Planet) suffix = " " + Roman(ordinal);
                    else
                    {
                        if (body.ParentId is null || !resolved.TryGetValue(body.ParentId, out var parent))
                            throw new InvalidDataException("Луна без ранее созданного родителя: " + body.Id);
                        bodyKey = parent.NameKey!;
                        suffix = parent.NameSuffix + " " + Satellite(ordinal, parent.SatelliteNamingPolicy);
                        fallback = parent.Name![..(parent.Name!.Length - parent.NameSuffix.Length)];
                    }
                }
                var named = body with { Name = (literal ?? fallback) + suffix, NameKey = bodyKey, NameSuffix = suffix };
                resolved.Add(body.Id, named); namedBodies.Add(named);
            }
        }
        return new(Array.AsReadOnly(namedSystems.ToArray()), Array.AsReadOnly(namedBodies.ToArray()),
            Array.AsReadOnly(diagnostics.ToArray()), draws);
    }
    private static string? Present(string? name) => string.IsNullOrWhiteSpace(name) ? null : name;
    private static string Readable(string key) => key.Replace('_', ' ');
    private static string Satellite(int ordinal, string? policy) => policy switch {
        null or "lower_case_latin_letters" => Letters(ordinal, false),
        "upper_case_latin_letters" => Letters(ordinal, true),
        // Оба Roman tokens в 4.5.2 дают один enum 1.
        "lower_case_roman_numerals" or "upper_case_roman_numerals" => Roman(ordinal),
        "arabic_numerals" => ordinal.ToString(CultureInfo.InvariantCulture),
        _ => throw new InvalidDataException("Неизвестный active satellite_naming_policy: " + policy) };
    private static string Letters(int ordinal, bool upper)
    {
        var value = "";
        while (ordinal > 0) { ordinal--; value = (char)((upper ? 'A' : 'a') + ordinal % 26) + value; ordinal /= 26; }
        return value;
    }
    private static string Roman(int ordinal)
    {
        var value = "";
        foreach (var (number, token) in new (int, string)[] { (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
            while (ordinal >= number) { value += token; ordinal -= number; }
        return value;
    }
}
