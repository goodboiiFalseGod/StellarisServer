using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

public sealed record GalaxyNebulaSite(string Id, NativeFixedPoint Position);
public sealed class GalaxyNebula
{
    public string Id { get; }
    public NativeFixedPoint Center { get; }
    public long Radius { get; }
    public string NameKey { get; }
    public IReadOnlyList<string> SystemIds { get; }
    internal GalaxyNebula(string id, NativeFixedPoint center, long radius, string name, IEnumerable<string> systems)
    { Id = id; Center = center; Radius = radius; NameKey = name; SystemIds = Array.AsReadOnly(systems.ToArray()); }
}
public sealed record GalaxyNebulaDiagnostic(string Code, int Ordinal);
public sealed class GalaxyNebulaResult
{
    public IReadOnlyList<GalaxyNebula> Nebulae { get; }
    public IReadOnlyList<GalaxyNebulaDiagnostic> Diagnostics { get; }
    public long PlacementDraws { get; }
    public long NameDraws { get; }
    internal GalaxyNebulaResult(IEnumerable<GalaxyNebula> nebulae, IEnumerable<GalaxyNebulaDiagnostic> diagnostics, long placement, long names)
    { Nebulae = Array.AsReadOnly(nebulae.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.ToArray()); PlacementDraws = placement; NameDraws = names; }
}

/// <summary>Геометрия §18.5; caller задаёт локальные потоки, скрытый global RNG не воспроизводится.</summary>
public static class GalaxyNebulaGeneration
{
    public static GalaxyNebulaResult Generate(IReadOnlyList<GalaxyNebulaSite> sites, int count, long radius,
        long minimumSeparation, IReadOnlyList<string> names, Func<uint> placementDraw, Func<uint> nameDraw)
    {
        ArgumentNullException.ThrowIfNull(sites); ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(placementDraw); ArgumentNullException.ThrowIfNull(nameDraw);
        if (count < 0 || radius < 0 || minimumSeparation < 0 || count > 0 && (radius == 0 || sites.Count == 0))
            throw new ArgumentOutOfRangeException(nameof(count), "Неверные count/radius/separation или пустые systems.");
        var systems = sites.ToArray(); var pool = names.ToList();
        if (systems.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id)) || systems.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != systems.Length ||
            pool.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Нужны unique system IDs и непустые name keys.");
        var result = new List<GalaxyNebula>(); var diagnostics = new List<GalaxyNebulaDiagnostic>();
        var attached = new HashSet<string>(StringComparer.Ordinal); long draws = 0, nameDraws = 0;
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            var budget = 10000;
            while (true)
            {
                draws++; var center = systems[(int)(placementDraw() % (uint)systems.Length)].Position;
                var rejected = result.Any(n => Squared(n.Center, center) < (BigInteger)minimumSeparation * minimumSeparation);
                if (budget < 1)
                { diagnostics.Add(new("placement-budget-exhausted", ordinal)); return new(result, diagnostics, draws, nameDraws); }
                if (rejected) { budget--; continue; }
                var name = "";
                if (pool.Count == 0) diagnostics.Add(new("name-pool-exhausted", ordinal));
                else
                {
                    nameDraws++; var index = (int)(nameDraw() % (uint)pool.Count); name = pool[index];
                    pool[index] = pool[^1]; pool.RemoveAt(pool.Count - 1);
                }
                var members = systems.Where(s => !attached.Contains(s.Id) && Squared(s.Position, center) < (BigInteger)radius * radius).Select(s => s.Id).ToArray();
                attached.UnionWith(members);
                result.Add(new("nebula/" + ordinal.ToString("D4", CultureInfo.InvariantCulture), center, radius, name, members));
                break;
            }
        }
        return new(result, diagnostics, draws, nameDraws);
    }
    private static BigInteger Squared(NativeFixedPoint a, NativeFixedPoint b)
    { var x = (BigInteger)a.X - b.X; var y = (BigInteger)a.Y - b.Y; return x * x + y * y; }
}

/// <summary>Отдельный optional источник: отсутствие сохраняется как часть snapshot.</summary>
internal sealed class GalaxyNebulaNamesSource
{
    internal const string RelativePath = "common/random_names/base/00_random_names.txt";
    private readonly string root;
    public GalaxySourceFile? File { get; }
    private readonly string[] names = [];
    private readonly string[] starNames = [];
    private readonly string? starError;
    private readonly string? error;
    public GalaxyNebulaNamesSource(string root)
    {
        this.root = root; var source = Read(); File = source.File;
        if (source.Bytes is null) { error = starError = "Недоступен random names source."; return; }
        try
        {
            var parsed = GalaxyScriptReader.Parse(new UTF8Encoding(false, true).GetString(source.Bytes), RelativePath);
            string[] Pool(string key)
            {
            var entries = parsed.Entries.Where(e => e.Key == key).ToArray();
            if (entries.Length != 1 || entries[0].Operator != "=" || entries[0].Value.Kind != GalaxySyntaxKind.Block ||
                entries[0].Value.Entries.Any(e => e.Key is not null || e.Operator is not null || e.Value.Kind != GalaxySyntaxKind.Scalar || string.IsNullOrWhiteSpace(e.Value.Text)))
                throw new InvalidDataException("Требуется один ordered scalar pool " + key + ".");
            return entries[0].Value.Entries.Select(e => e.Value.Text!).ToArray();
            }
            try { names = Pool("nebula_names"); } catch (InvalidDataException failure) { error = failure.Message; }
            try { starNames = Pool("star_names"); if (starNames.Length == 0) throw new InvalidDataException("Пустой star_names pool."); }
            catch (InvalidDataException failure) { starError = failure.Message; }
        }
        catch (Exception failure) when (failure is FormatException or IOException or InvalidDataException or ArgumentException)
        { error = starError = failure.Message; }
    }
    public IReadOnlyList<string> RequireNames()
    { if (error is not null) throw new InvalidDataException(error); return Array.AsReadOnly(names); }
    public IReadOnlyList<string> RequireStarNames()
    { if (starError is not null) throw new InvalidDataException(starError); return Array.AsReadOnly(starNames); }
    public void VerifyStable()
    { if (Read().File != File) throw new IOException("Nebula names source изменился после импорта."); }
    private (byte[]? Bytes, GalaxySourceFile? File) Read()
    {
        var path = Path.Combine(root, RelativePath);
        // Проверяем и существующие ancestors отсутствующего optional файла.
        for (FileSystemInfo? part = new FileInfo(path); part is not null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
            if (part.LinkTarget is not null || part.Exists && (part.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse nebula names path запрещён.");
        if (!System.IO.File.Exists(path)) return (null, null);
        var bytes = System.IO.File.ReadAllBytes(path);
        return (bytes, new(RelativePath, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength));
    }
}
