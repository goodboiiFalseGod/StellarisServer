using System.Security.Cryptography;
using System.Text.Json;

namespace Stellaris.Core.Assets;

public sealed class M3AssetManifestBuilder
{
    public const string PROFILE_ID = "m3-static-452-v12";
    public const long MAX_FILE_BYTES = 64L * 1024 * 1024;
    private const string MAMMALIAN = "gfx/models/ships/mammalian_01/";
    private const string STARBASE = "gfx/models/ships/starbases/";
    private const string PLANETS = "gfx/models/planets/";
    private readonly Func<CancellationToken, Task>? _afterFirstPass;
    private static readonly IReadOnlyList<string> _profilePaths = Array.AsReadOnly(new[]
    {
        MAMMALIAN + "_mammalian_01_ships_entities.asset", MAMMALIAN + "_mammalian_01_ships_meshes.gfx",
        STARBASE + "_starbase_entities.asset", STARBASE + "_starbase_meshes.gfx",
        PLANETS + "_planetary_entities.asset", PLANETS + "_planetary_meshes.gfx", PLANETS + "_star_entities.asset", PLANETS + "_star_meshes.gfx",
        "common/ship_sizes/00_starbases.txt", "common/section_templates/starbase.txt",
        "interface/resources.gfx", "interface/icons.gfx", "interface/galaxy_view.gfx",
        MAMMALIAN + "mammalian_01_corvette_S1.mesh", STARBASE + "mammalian_01_outpost_frame.mesh", STARBASE + "mammalian_01_outpost.mesh", PLANETS + "planet_clouded.mesh", PLANETS + "base_star.mesh",
        MAMMALIAN + "mammalian_01_corvette_diffuse.dds", MAMMALIAN + "mammalian_01_corvette_normal.dds", MAMMALIAN + "mammalian_01_corvette_specular.dds",
        MAMMALIAN + "mammalian_01_stations_diffuse.dds", MAMMALIAN + "mammalian_01_stations_normal.dds", MAMMALIAN + "mammalian_01_stations_specular.dds",
        MAMMALIAN + "mammalian_01_battleship_diffuse.dds", MAMMALIAN + "mammalian_01_battleship_normal.dds", MAMMALIAN + "mammalian_01_battleship_specular.dds",
        PLANETS + "continental_01_diffuse.dds", PLANETS + "continental_01_normals.dds", PLANETS + "continental_01_spec.dds",
        PLANETS + "poles_1_diffuse.dds", PLANETS + "poles_1_normal.dds", PLANETS + "poles_1_specular.dds",
        PLANETS + "clouds_medium_diffuse.dds", PLANETS + "clouds_medium_normal.dds", PLANETS + "clouds_medium_specular.dds", PLANETS + "clouds_medium_shadow_diffuse.dds",
        "gfx/models/nonormal.dds", "gfx/models/nospec.dds", "gfx/interface/icons/resources/energy.dds", "gfx/interface/icons/resources/minerals.dds", "gfx/interface/icons/resources/food.dds",
        "gfx/interface/icons/fleet_size_icon.dds", "gfx/interface/icons/planet.dds",
        "gfx/worldgfx/star_g_class.txt", "gfx/worldgfx/star_a_class.txt", "gfx/worldgfx/star_b_class.txt", "gfx/worldgfx/star_f_class.txt", "gfx/worldgfx/star_k_class.txt", "gfx/worldgfx/star_m_class.txt", "gfx/worldgfx/star_t_class.txt", "gfx/worldgfx/star_neutron.txt", "gfx/worldgfx/star_pulsar.txt", "gfx/worldgfx/star_black_hole.txt", "gfx/worldgfx/lava_noise.dds", "gfx/worldgfx/lava_d.dds", "gfx/worldgfx/stone_d.dds", "common/planet_classes/00_planet_classes.txt",
        "gfx/worldgfx/default.txt", "gfx/worldgfx/stars.dds", "gfx/map/sky_core.dds", "gfx/map/sky_mid.dds", "gfx/map/sky_rim.dds", "interface/main.gfx", "interface/outliner.gfx", "interface/government_view.gfx",
        "gfx/interface/main/topbar_background.dds", "gfx/interface/tiles/outliner_tile.dds", "gfx/interface/tiles/green_bg_tile.dds", "gfx/interface/buttons/outliner_entry.dds", "gfx/interface/main/starsystem_panel.dds",
        "gfx/map/center.dds", "gfx/map/dust.dds", "gfx/map/galaxycolor.dds"
    }.Concat(PlanetAssetProfile.Paths).Distinct(StringComparer.Ordinal).ToArray());

    public static IReadOnlyList<string> ProfilePaths => _profilePaths;

    public static string? ResolvePlanetImageKey(M3AssetManifest manifest, string category, string id)
    {
        var key = category + "." + id;
        return PlanetAssetProfile.Bindings.TryGetValue(key, out var binding)
            && manifest.Files.Any(file => file.RelativePath == binding.Definition)
            && manifest.Files.Any(file => file.RelativePath == binding.Texture) ? key : null;
    }

    public M3AssetManifestBuilder()
    {
    }

    internal M3AssetManifestBuilder(Func<CancellationToken, Task> afterFirstPass)
    {
        ArgumentNullException.ThrowIfNull(afterFirstPass);
        _afterFirstPass = afterFirstPass;
    }

    public async Task<M3AssetManifest> BuildAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = ResolveRoot(gameRoot);
        await CheckVersionAsync(root, cancellationToken);
        var first = await ReadPassAsync(root, cancellationToken);
        // Внутренний seam вызывается после освобождения всех handles первого прохода.
        if (_afterFirstPass is not null)
        {
            await _afterFirstPass(cancellationToken);
        }
        await CheckVersionAsync(root, cancellationToken);
        var second = await ReadPassAsync(root, cancellationToken);
        await CheckVersionAsync(root, cancellationToken);
        for (var index = 0; index < first.Count; index++)
        {
            if (first[index].ByteLength != second[index].ByteLength || first[index].Sha256 != second[index].Sha256)
            {
                throw new AssetManifestException($"Источник изменился при построении профиля: {first[index].RelativePath}.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new M3AssetManifest(first);
    }

    public async Task<M3AssetManifest> BuildAsync(string gameRoot, GeneratedWorldVisualRequirements requirements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        cancellationToken.ThrowIfCancellationRequested();
        var root = ResolveRoot(gameRoot); await CheckVersionAsync(root, cancellationToken);
        var graphHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var graphLengths = new Dictionary<string, long>(StringComparer.Ordinal);
        async Task<byte[]> ReadSource(string path, CancellationToken token)
        {
            await using var source = await OpenAsync(root, path, MAX_FILE_BYTES, token);
            var length = source.Length; using var destination = new MemoryStream();
            var chunk = new byte[65536]; int count;
            while ((count = await source.ReadAsync(chunk, token)) != 0)
            {
                if (destination.Length + count > MAX_FILE_BYTES) throw new AssetManifestException($"Источник превышает лимит: {path}.");
                destination.Write(chunk, 0, count);
            }
            if (source.Length != length || destination.Length != length) throw new AssetManifestException($"Источник изменился: {path}.");
            var bytes = destination.ToArray(); graphHashes.Add(path, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()); graphLengths.Add(path, bytes.LongLength); return bytes;
        }
        bool Exists(string path)
        {
            try { ResolveFile(root, path); return true; }
            catch (AssetManifestException) { return false; }
        }
        var bindings = await GeneratedWorldVisualSourceCatalog.ResolveAsync(requirements, ReadSource, Exists, cancellationToken);
        var objectBindings = await GeneratedWorldVisualSourceCatalog.ResolveObjectsAsync(requirements, ReadSource, Exists, cancellationToken);
        var objectDefinitionPaths = requirements.ObjectIds is null ? Array.Empty<string>() : GeneratedWorldVisualSourceCatalog.ObjectDefinitionPathsFor(requirements.ObjectIds).ToArray();
        var definitionLimit = GeneratedWorldVisualSourceCatalog.DefinitionPaths.Concat(objectDefinitionPaths)
            .Where(graphLengths.ContainsKey).Sum(path => graphLengths[path]);
        if (definitionLimit > 16 * 1024 * 1024) throw new GeneratedWorldVisualException("definition-limit", null, "Combined generated object/class source definitions exceed16MiB.");
        var paths = ProfilePaths.Concat(GeneratedWorldVisualSourceCatalog.DefinitionPaths)
            .Concat(objectDefinitionPaths)
            .Concat(bindings.SelectMany(b => b.Dependencies)).Concat(objectBindings.SelectMany(b => b.Dependencies))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var first = await ReadPassAsync(root, cancellationToken, paths);
        if (first.Any(file => graphHashes.TryGetValue(file.RelativePath, out var hash) && hash != file.Sha256))
            throw new AssetManifestException("Источник изменился после разрешения generated graph.");
        if (_afterFirstPass is not null) await _afterFirstPass(cancellationToken);
        await CheckVersionAsync(root, cancellationToken);
        var second = await ReadPassAsync(root, cancellationToken, paths);
        await CheckVersionAsync(root, cancellationToken);
        if (first.Where((file, i) => file.ByteLength != second[i].ByteLength || file.Sha256 != second[i].Sha256).Any())
            throw new AssetManifestException("Generated источник изменился при построении профиля.");
        cancellationToken.ThrowIfCancellationRequested();
        return new M3AssetManifest(first, requirements);

    }

    private static string ResolveRoot(string root)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new AssetManifestException("Корень установки не указан.");
            }
            var result = Path.GetFullPath(root);
            var current = new DirectoryInfo(result);
            while (current is not null)
            {
                if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new AssetManifestException("Корень установки отсутствует либо содержит reparse point.");
                }
                current = current.Parent;
            }
            return Path.TrimEndingDirectorySeparator(result);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new AssetManifestException("Невозможно проверить корень установки.");
        }
    }

    private static string ResolveFile(string root, string relativePath)
    {
        AssetManifestCanonical.ValidateRelativePath(relativePath);
        var path = root;
        var segments = relativePath.Split('/');
        try
        {
            // Корень проверяется заново при каждом чтении, включая второй проход.
            ResolveRoot(root);
            for (var index = 0; index < segments.Length; index++)
            {
                path = Path.Combine(path, segments[index]);
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || (index < segments.Length - 1 && (attributes & FileAttributes.Directory) == 0) || (index == segments.Length - 1 && (attributes & FileAttributes.Directory) != 0))
                {
                    throw new AssetManifestException($"Недопустимый тип пути/reparse point: {relativePath}.");
                }
            }
            var full = Path.GetFullPath(path);
            var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new AssetManifestException($"Путь вне установки: {relativePath}.");
            }
            return full;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new AssetManifestException($"Файл отсутствует либо недоступен: {relativePath}.");
        }
    }

    private static async Task<FileStream> OpenAsync(string root, string relativePath, long limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveFile(root, relativePath);
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > limit)
            {
                await stream.DisposeAsync();
                throw new AssetManifestException($"Файл превышает лимит: {relativePath}.");
            }
            return stream;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AssetManifestException($"Файл недоступен для чтения: {relativePath}.");
        }
    }

    private static async Task CheckVersionAsync(string root, CancellationToken cancellationToken)
    {
        const string relativePath = "launcher-settings.json";
        try
        {
            await using var stream = await OpenAsync(root, relativePath, 1024 * 1024, cancellationToken);
            using var metadata = new MemoryStream();
            var buffer = new byte[16384];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (metadata.Length + read > 1024 * 1024)
                {
                    throw new AssetManifestException("Metadata превышает лимит: launcher-settings.json.");
                }
                metadata.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(metadata.GetBuffer().AsMemory(0, checked((int)metadata.Length)));
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Count(property => property.Name == "rawVersion") != 1 || !document.RootElement.TryGetProperty("rawVersion", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "v4.5.2")
            {
                throw new AssetManifestException("Несовместимая версия: launcher-settings.json.");
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new AssetManifestException("Повреждённая либо недоступная metadata: launcher-settings.json.");
        }
    }

    private static async Task<List<AssetManifestFile>> ReadPassAsync(string root, CancellationToken cancellationToken, IReadOnlyList<string>? paths = null)
    {
        paths ??= _profilePaths;
        var files = new List<AssetManifestFile>(paths.Count);
        foreach (var relativePath in paths)
        {
            try
            {
                await using var stream = await OpenAsync(root, relativePath, MAX_FILE_BYTES, cancellationToken);
                var length = stream.Length;
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[65536];
                long consumed = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    consumed += read;
                    if (consumed > MAX_FILE_BYTES)
                    {
                        throw new AssetManifestException($"Файл превышает лимит: {relativePath}.");
                    }
                    hasher.AppendData(buffer, 0, read);
                }
                var hash = hasher.GetHashAndReset();
                if (stream.Length != length || consumed != length)
                {
                    throw new AssetManifestException($"Источник изменился при чтении: {relativePath}.");
                }
                files.Add(new AssetManifestFile(relativePath, length, Convert.ToHexString(hash).ToLowerInvariant(), Path.GetExtension(relativePath)[1..]));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new AssetManifestException($"Ошибка чтения профиля: {relativePath}.");
            }
        }
        return files;
    }
}
