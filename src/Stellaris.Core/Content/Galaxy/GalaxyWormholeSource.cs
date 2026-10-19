using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using Stellaris.Core.Galaxy;

namespace Stellaris.Core.Content.Galaxy;

/// <summary>Опциональная source-проверка активного game_start.31; legacy остаётся доступен.</summary>
public sealed class GalaxyWormholeSource
{
    public const string EventPath = "events/game_start.txt";
    public const string OnActionPath = "common/on_actions/00_on_actions.txt";
    public const string BypassPath = "common/bypass/00_bypasses.txt";
    private readonly string root;
    private readonly IReadOnlyList<GalaxySourceFile> files;
    private readonly IReadOnlyDictionary<string, bool> membership;
    private readonly IReadOnlyDictionary<string, long?> sourceLengths;
    private readonly IReadOnlySet<string> validatedPaths;
    private readonly string? error;
    private static readonly string[] SourcePaths = [EventPath, OnActionPath, BypassPath];
    public IReadOnlyList<GalaxySourceFile> Files => files;
    public GalaxyWormholeSource(string installationRoot)
    {
        root = Path.GetFullPath(installationRoot);
        var list = new List<GalaxySourceFile>(); var present = new Dictionary<string, bool>(StringComparer.Ordinal);
        var lengths = new Dictionary<string, long?>(StringComparer.Ordinal); var validated = new HashSet<string>(StringComparer.Ordinal); string? failure = null;
        foreach (var relative in SourcePaths)
        {
            var path = Path.Combine(root, relative);
            try
            {
                present[relative] = File.Exists(path);
                if (!present[relative]) { lengths[relative] = null; failure ??= "Missing source: " + relative; continue; }
                EnsureNoReparse(path);
                var sourceLength = new FileInfo(path).Length; lengths[relative] = sourceLength;
                if (sourceLength > 16 * 1024 * 1024) { failure ??= "Source exceeds 16 MiB: " + relative; continue; }
                var bytes = File.ReadAllBytes(path);
                if (bytes.LongLength > 16 * 1024 * 1024) { failure ??= "Source exceeds 16 MiB: " + relative; continue; }
                list.Add(new(relative, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength));
                var syntax = GalaxyScriptReader.Parse(new UTF8Encoding(false, true).GetString(bytes), relative);
                if (relative == EventPath) ValidateEvent(syntax);
                else if (relative == OnActionPath) ValidateOnAction(syntax);
                else ValidateBypass(syntax);
                validated.Add(relative);
            }
            catch (Exception ex) when (ex is IOException or FormatException or DecoderFallbackException or InvalidDataException or ArgumentException or InvalidOperationException)
            { failure ??= ex.Message; }
        }
        files = Array.AsReadOnly(list.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray()); membership = new System.Collections.ObjectModel.ReadOnlyDictionary<string, bool>(present);
        sourceLengths = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long?>(lengths); validatedPaths = validated.ToFrozenSet(StringComparer.Ordinal); error = failure;
    }
    public void Require()
    {
        if (error is not null || files.Count != 3) throw new GalaxyStructureException("invalid-wormhole-source", error ?? "Incomplete wormhole source closure.", new(EventPath, 1, 1));
    }
    public void VerifyStable()
    {
        foreach (var relative in SourcePaths)
        {
            var path = Path.Combine(root, relative);
            EnsureNoReparse(path);
            var exists = File.Exists(path);
            if (membership[relative] != exists) throw new IOException("Wormhole source membership changed: " + relative);
            if (!exists) continue;
            var length = new FileInfo(path).Length;
            if (sourceLengths[relative] != length) throw new IOException("Wormhole source size changed: " + relative);
        }
        foreach (var source in files)
        {
            var path = Path.Combine(root, source.Path); var bytes = File.ReadAllBytes(path);
            if (bytes.LongLength != source.Bytes || Convert.ToHexString(SHA256.HashData(bytes)) != source.Sha256) throw new IOException("Wormhole source changed: " + source.Path);
            if (validatedPaths.Contains(source.Path))
            {
                try
                {
                    var syntax = GalaxyScriptReader.Parse(new UTF8Encoding(false, true).GetString(bytes), source.Path);
                    if (source.Path == EventPath) ValidateEvent(syntax);
                    else if (source.Path == OnActionPath) ValidateOnAction(syntax);
                    else ValidateBypass(syntax);
                }
                catch (Exception ex) when (ex is FormatException or DecoderFallbackException or InvalidDataException or ArgumentException or InvalidOperationException)
                { throw new IOException("Wormhole source no longer parses: " + source.Path, ex); }
            }
        }
    }
    private static void EnsureNoReparse(string path)
    {
        for (FileSystemInfo? part = new FileInfo(path); part is not null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
            if (part.LinkTarget is not null || part.Exists && (part.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse wormhole source запрещён.");
    }
    private static void ValidateEvent(GalaxySyntaxValue root)
    {
        var events = root.Entries.Where(e => e.Key == "event" && e.Value.Kind == GalaxySyntaxKind.Block && Scalar(e.Value, "id") == "game_start.31").ToArray();
        if (events.Length != 1 || events[0].Operator != "=") throw new InvalidDataException("Expected unique active event game_start.31.");
        var evt = events[0].Value;
        if (evt.Entries.Count != 4 || evt.Entries.Any(e => e.Operator != "=") ||
            !evt.Entries.Select(e => e.Key).SequenceEqual(new[] { "id", "hide_window", "is_triggered_only", "immediate" }) ||
            Scalar(evt, "hide_window") != "yes" || Scalar(evt, "is_triggered_only") != "yes") throw new InvalidDataException("game_start.31 header fields changed.");
        var immediate = UniqueBlock(evt, "immediate");
        if (immediate.Entries.Count != 2 || immediate.Entries[0].Key != "get_galaxy_setup_value" || immediate.Entries[1].Key != "while") throw new InvalidDataException("game_start.31 immediate order/fields changed.");
        var setup = UniqueBlock(immediate, "get_galaxy_setup_value");
        if (Scalar(setup, "setting") != "num_wormhole_pairs" || Scalar(setup, "which") != "local_wormholes_num" || Scalar(setup, "scale_by") != "5") throw new InvalidDataException("game_start.31 setup scaling changed.");
        if (setup.Entries.Count != 3 || setup.Entries.Any(e => e.Operator != "=") || !setup.Entries.Select(e => e.Key).SequenceEqual(new[] { "setting", "which", "scale_by" })) throw new InvalidDataException("game_start.31 setup fields changed.");
        var loops = new[] { UniqueBlock(immediate, "while") };
        if (loops.Length != 1 || Scalar(loops[0], "count") != "local_wormholes_num") throw new InvalidDataException("game_start.31 attempt loop changed.");
        if (loops[0].Entries.Count != 2 || loops[0].Entries[0].Key != "count" || loops[0].Entries[0].Operator != "=") throw new InvalidDataException("game_start.31 attempt fields changed.");
        var random = new[] { UniqueBlock(loops[0], "random_system") };
        if (random[0].Entries.Count != 2 || random[0].Entries[0].Key != "limit" || random[0].Entries[1].Key != "random_system" || random[0].Entries.Any(e => e.Operator != "="))
            throw new InvalidDataException("game_start.31 first endpoint active fields changed.");
        if (!Exclusions(random[0], allowDistance: false)) throw new InvalidDataException("game_start.31 first endpoint selection changed.");
        var second = new[] { UniqueBlock(random[0], "random_system") };
        if (!Exclusions(second[0], allowDistance: true)) throw new InvalidDataException("game_start.31 second endpoint selection changed.");
        var secondLimit = UniqueBlock(second[0], "limit");
        var distance = new[] { UniqueBlock(secondLimit, "distance") };
        if (distance[0].Entries.Count != 2 || Scalar(distance[0], "source") != "prev" || Number(distance[0], "min_distance", ">=") != "500") throw new InvalidDataException("game_start.31 distance predicate changed.");
        var spawn = new[] { UniqueBlock(second[0], "spawn_natural_wormhole") };
        var previous = new[] { UniqueBlock(second[0], "prev") };
        var prevSpawn = new[] { UniqueBlock(previous[0], "spawn_natural_wormhole") };
        if (previous[0].Entries.Count != 1 || spawn.Concat(prevSpawn).Any(s => s.Entries.Count != 3 || s.Entries.Any(e => e.Operator != "=") || Scalar(s, "bypass_type") != "wormhole" || Scalar(s, "random_pos") != "yes" || Scalar(s, "orbit_angle") != "360") || Scalar(second[0], "link_wormholes") != "prev")
            throw new InvalidDataException("game_start.31 paired spawn changed.");
        if (second[0].Entries.Count != 4 || second[0].Entries.Any(e => e.Operator != "=") ||
            second[0].Entries[0].Key != "limit" || second[0].Entries[1].Key != "prev" || second[0].Entries[2].Key != "spawn_natural_wormhole" || second[0].Entries[3].Key != "link_wormholes" ||
            secondLimit.Entries.Count != 2 || secondLimit.Entries.Any(e => e.Operator is not ("=" or ">=")) || secondLimit.Entries[0].Key != "NOR" || secondLimit.Entries[1].Key != "distance" ||
            second[0].Entries[3].Value.Kind != GalaxySyntaxKind.Scalar || second[0].Entries[3].Value.Text != "prev") throw new InvalidDataException("game_start.31 active fields/order changed.");
    }
    private static bool Exclusions(GalaxySyntaxValue random, bool allowDistance)
    {
        GalaxySyntaxValue limit; try { limit = UniqueBlock(random, "limit"); } catch { return false; }
        GalaxySyntaxValue nor; try { nor = UniqueBlock(limit, "NOR"); } catch { return false; }
        if (limit.Entries.Count != (allowDistance ? 2 : 1) || limit.Entries[0].Operator != "=" || limit.Entries.Any(e => e.Operator is not ("=" or ">=")) || limit.Entries[0].Key != "NOR") return false;
        var entries = nor.Entries;
        return entries.Count == 3 && entries.All(e => e.Operator == "=" && e.Value.Kind == GalaxySyntaxKind.Scalar && (e.Key == "has_star_flag" ? e.Value.Text == "empire_cluster" : e.Value.Text == "yes")) &&
            entries.Select(e => e.Key).Order(StringComparer.Ordinal).SequenceEqual(new[] { "has_natural_wormhole", "has_star_flag", "starting_system" }.Order(StringComparer.Ordinal));
    }
    private static void ValidateOnAction(GalaxySyntaxValue root)
    {
        var action = UniqueBlock(root, "on_game_start"); var events = UniqueBlock(action, "events");
        var hooks = events.Entries.Where(e => e.Value.Kind == GalaxySyntaxKind.Scalar && e.Value.Text == "game_start.31").ToArray();
        if (hooks.Length != 1 || hooks[0].Key is not null || hooks[0].Operator is not null) throw new InvalidDataException("on_game_start game_start.31 hook changed.");
    }
    private static void ValidateBypass(GalaxySyntaxValue root)
    {
        var item = UniqueBlock(root, "wormhole");
        if (Scalar(item, "connection_type") != "one_to_one" || Scalar(item, "always_active") != "yes" || Scalar(item, "requires_exploration") != "yes" || Scalar(item, "icon_frame") != "12") throw new InvalidDataException("wormhole bypass policy changed.");
        var potential = UniqueBlock(item, "potential"); if (Scalar(potential, "always") != "yes") throw new InvalidDataException("wormhole bypass potential changed.");
        var prereq = UniqueBlock(item, "prerequisites"); if (prereq.Entries.Count != 1 || prereq.Entries[0].Key is not null || prereq.Entries[0].Operator is not null || prereq.Entries[0].Value.Text != "tech_wormhole_stabilization") throw new InvalidDataException("wormhole technology prerequisite changed.");
    }
    private static GalaxySyntaxValue[] Blocks(GalaxySyntaxValue node, string key) => node.Entries.Where(e => e.Key == key && e.Operator == "=" && e.Value.Kind == GalaxySyntaxKind.Block).Select(e => e.Value).ToArray();
    private static GalaxySyntaxValue Block(GalaxySyntaxValue node, string key) => Blocks(node, key).Single();
    private static GalaxySyntaxValue UniqueBlock(GalaxySyntaxValue node, string key)
    {
        var entries = node.Entries.Where(e => e.Key == key).ToArray();
        if (entries.Length != 1 || entries[0].Operator != "=" || entries[0].Value.Kind != GalaxySyntaxKind.Block) throw new InvalidDataException("Expected one active " + key + " block.");
        return entries[0].Value;
    }
    private static string? Scalar(GalaxySyntaxValue node, string key)
    {
        var entries = node.Entries.Where(e => e.Key == key).ToArray();
        if (entries.Length != 1 || entries[0].Operator != "=" || entries[0].Value.Kind != GalaxySyntaxKind.Scalar) return null;
        return entries[0].Value.Text;
    }
    private static string? Number(GalaxySyntaxValue node, string key, string op)
    {
        var entries = node.Entries.Where(e => e.Key == key).ToArray();
        return entries.Length == 1 && entries[0].Operator == op && entries[0].Value.Kind == GalaxySyntaxKind.Scalar ? entries[0].Value.Text : null;
    }
}
