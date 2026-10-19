using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content;

public sealed record ManifestFile(string Path, string Sha256, long Bytes);
public sealed record ParsedSource(string Path, ScriptBlock Root);
public sealed record ContentManifest(string Version, string Profile, IReadOnlyList<ManifestFile> Files, IReadOnlyList<string> Limitations);

public sealed class ContentSession
{
    private readonly string _root;
    private readonly List<ManifestFile> _files = [];
    private readonly Dictionary<string, ParsedSource> _sources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ScriptEntry> _variables = new(StringComparer.Ordinal);
    private readonly string _version;

    public string Version => _version;
    public IReadOnlyCollection<ParsedSource> Sources => _sources.Values;

    public ContentSession(string root)
    {
        _root = System.IO.Path.GetFullPath(root);
        var settings = ReadBytes("launcher-settings.json");
        using var document = JsonDocument.Parse(settings);
        _version = document.RootElement.GetProperty("rawVersion").GetString() ?? throw new InvalidDataException("Не указана версия игры.");
    }

    public void LoadFolder(string relativeFolder)
    {
        var directory = Resolve(relativeFolder);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Нет папки контента: {relativeFolder}");
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.txt", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            LoadFile(System.IO.Path.GetRelativePath(_root, file).Replace('\\', '/'));
        }
    }

    public ParsedSource LoadFile(string relativePath)
    {
        relativePath = relativePath.Replace('\\', '/');
        if (_sources.TryGetValue(relativePath, out var existing))
        {
            return existing;
        }

        var bytes = ReadBytes(relativePath);
        var root = ParadoxParser.Parse(new UTF8Encoding(false, true).GetString(bytes), relativePath);
        var source = new ParsedSource(relativePath, root);
        _sources.Add(relativePath, source);
        foreach (var variable in root.Entries.Where(entry => entry.Key.StartsWith('@')))
        {
            // Локальные переменные разрешаются раньше глобальных; глобальные загружаются отдельно.
            if (relativePath.StartsWith("common/scripted_variables/", StringComparison.Ordinal))
            {
                if (!_variables.TryAdd(variable.Key, variable))
                {
                    throw new InvalidDataException($"Повтор глобальной переменной {variable.Key}: {relativePath}:{variable.Line}");
                }
            }
        }

        return source;
    }

    public ScriptEntry Definition(string folder, string id)
    {
        var candidates = _sources.Values.Where(source => source.Path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.Ordinal))
            .SelectMany(source => source.Root.Named(id)).ToArray();
        if (candidates.Length != 1)
        {
            throw new InvalidDataException($"Ожидалось ровно одно определение {id}, найдено {candidates.Length}. Переопределения не поддержаны в M1.");
        }

        return candidates[0];
    }

    public decimal Number(ScriptEntry entry)
    {
        return Number(entry.AsScalar(), entry.Source, new HashSet<string>(StringComparer.Ordinal));
    }

    public void VerifyStable()
    {
        foreach (var file in _files)
        {
            var current = File.ReadAllBytes(Resolve(file.Path));
            if (current.LongLength != file.Bytes || Convert.ToHexString(SHA256.HashData(current)) != file.Sha256)
            {
                throw new IOException($"Контент изменился во время импорта: {file.Path}. Дождитесь завершения обновления.");
            }
        }
    }

    public ContentManifest Manifest(string profile, IReadOnlyList<string> limitations)
    {
        VerifyStable();
        return new ContentManifest(_version, profile, _files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray(), limitations);
    }

    private decimal Number(string text, string source, HashSet<string> stack)
    {
        if (decimal.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (!text.StartsWith('@') || !stack.Add(text))
        {
            throw new InvalidDataException($"Неподдерживаемое число или цикл переменных: {text} ({source})");
        }

        var local = _sources.TryGetValue(source, out var parsed) ? parsed.Root.Optional(text) : null;
        var variable = local ?? (_variables.TryGetValue(text, out var global) ? global : null);
        if (variable is null)
        {
            throw new InvalidDataException($"Не найдена переменная {text} ({source})");
        }

        if (variable.Operator != "=")
        {
            throw new InvalidDataException($"{variable.Source}:{variable.Line}:{variable.Column}: переменная {text} должна использовать присваивание =.");
        }

        return Number(variable.AsScalar(), variable.Source, stack);
    }

    private byte[] ReadBytes(string relativePath)
    {
        var bytes = File.ReadAllBytes(Resolve(relativePath));
        _files.Add(new ManifestFile(relativePath, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength));
        return bytes;
    }

    private string Resolve(string relativePath)
    {
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Путь контента выходит за пределы установки.", nameof(relativePath));
        }

        return full;
    }
}
