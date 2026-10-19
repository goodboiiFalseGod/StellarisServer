using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Galaxy;

public sealed record GalaxySourceFile(string Path, string Sha256, long Bytes);
public sealed record GalaxyDefinition(string Id, string Path, int Line, ScriptBlock Fields);

/// <summary>Неизменяемый snapshot base-only definitions; порядок задаёт вызывающая сторона.</summary>
public sealed class GalaxyCatalog
{
    private readonly string _installationRoot;
    public IReadOnlyList<GalaxySourceFile> Files { get; }
    public IReadOnlyList<GalaxyDefinition> Scenarios { get; }
    public IReadOnlyList<GalaxyDefinition> Shapes { get; }

    private GalaxyCatalog(string root, List<GalaxySourceFile> files,
        List<GalaxyDefinition> scenarios, List<GalaxyDefinition> shapes)
    {
        _installationRoot = root;
        Files = files.AsReadOnly();
        Scenarios = scenarios.AsReadOnly();
        Shapes = shapes.AsReadOnly();
    }

    public static GalaxyCatalog Import(string installationRoot,
        IEnumerable<string> orderedScenarioFiles, IEnumerable<string> orderedShapeFiles)
    {
        ArgumentNullException.ThrowIfNull(orderedScenarioFiles);
        ArgumentNullException.ThrowIfNull(orderedShapeFiles);
        var root = System.IO.Path.GetFullPath(installationRoot);
        var files = new List<GalaxySourceFile>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        byte[] Read(string path)
        {
            if (!paths.Add(path)) throw new InvalidDataException($"Повтор файла: {path}");
            var bytes = File.ReadAllBytes(ResolveSafe(root, path));
            files.Add(new(path, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength));
            return bytes;
        }

        using (var settings = JsonDocument.Parse(Read("launcher-settings.json")))
        {
            var versions = settings.RootElement.EnumerateObject().Where(property => property.NameEquals("rawVersion")).ToArray();
            if (versions.Length != 1 || versions[0].Value.ValueKind != JsonValueKind.String ||
                versions[0].Value.GetString() != "v4.5.2")
                throw new InvalidDataException("Импорт галактики требует ровно Stellaris 4.5.2.");
        }

        List<GalaxyDefinition> Load(IEnumerable<string> orderedFiles, string folder, bool scenario)
        {
            var result = new List<GalaxyDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in orderedFiles)
            {
                var path = input.Replace('\\', '/');
                if (!path.StartsWith(folder + "/", StringComparison.Ordinal) ||
                    !path.EndsWith(".txt", StringComparison.Ordinal))
                    throw new ArgumentException($"Ожидался относительный txt в {folder}: {path}");
                var parsed = ParadoxParser.Parse(new UTF8Encoding(false, true).GetString(Read(path)), path);
                foreach (var entry in parsed.Entries)
                {
                    RequireAssignment(entry);
                    if (scenario && entry.Key != "setup_scenario")
                        throw new NotSupportedException($"Тип сценария не поддержан: {entry.Key} ({path}:{entry.Line})");
                    var fields = entry.AsBlock();
                    var id = scenario ? Scalar(fields.Required("name")) : entry.Key;
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                        throw new InvalidDataException($"Пустой или повторный ID: {id} ({path}:{entry.Line})");
                    result.Add(new(id, path, entry.Line, fields));
                }
            }
            if (result.Count == 0) throw new InvalidDataException($"Нет определений в {folder}.");
            return result;
        }

        var catalog = new GalaxyCatalog(root, files,
            Load(orderedScenarioFiles, "map/setup_scenarios", true),
            Load(orderedShapeFiles, "map/galaxy", false));
        catalog.VerifyStable();
        return catalog;
    }

    public void VerifyStable()
    {
        foreach (var file in Files)
        {
            var bytes = File.ReadAllBytes(ResolveSafe(_installationRoot, file.Path));
            if (bytes.LongLength != file.Bytes || Convert.ToHexString(SHA256.HashData(bytes)) != file.Sha256)
                throw new IOException($"Источник изменился после импорта: {file.Path}");
        }
    }

    internal static string Scalar(ScriptEntry entry)
    {
        RequireAssignment(entry);
        return entry.AsScalar();
    }

    internal static decimal Number(ScriptEntry entry)
    {
        if (!decimal.TryParse(Scalar(entry), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"Не поддержано literal число {entry.Key}: {entry.Source}:{entry.Line}");
        return value;
    }

    private static void RequireAssignment(ScriptEntry entry)
    {
        if (entry.Operator != "=")
            throw new InvalidDataException($"Ожидалось присваивание =: {entry.Source}:{entry.Line} {entry.Key}");
    }

    private static string ResolveSafe(string root, string relativePath)
    {
        // ContentSession не проверяет junction; используем ограниченный reader локально.
        if (string.IsNullOrWhiteSpace(relativePath) || System.IO.Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new ArgumentException("Ожидался относительный путь.", nameof(relativePath));
        var parts = relativePath.Replace('\\', '/').Split('/');
        if (parts.Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
            part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("Некорректные компоненты и traversal запрещены.", nameof(relativePath));
        var current = root;
        CheckReparse(current);
        foreach (var part in parts)
        {
            current = System.IO.Path.Combine(current, part);
            CheckReparse(current);
        }
        // Проверяем также предков root: сам root может лежать внутри junction.
        for (var parent = Directory.GetParent(root); parent is not null; parent = parent.Parent)
            CheckReparse(parent.FullName);
        return current;
    }

    private static void CheckReparse(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Reparse point не разрешён для импорта: {path}");
    }
}
