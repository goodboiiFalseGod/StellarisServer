using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Stellaris.Session.M3;

/// <summary>Только проверенные ключи штатного профиля; оригинальные переводы не сохраняются и не публикуются.</summary>
public static class M3TechnologyKeys
{
    public static IReadOnlySet<string> Import(string gameRoot)
    {
        using var profile = typeof(M3TechnologyKeys).Assembly.GetManifestResourceStream("Stellaris.Session.M3.LocalizationPaths")
            ?? throw new InvalidDataException("Отсутствует профиль ключей локализации.");
        using var reader = new StreamReader(profile, new UTF8Encoding(false, true));
        var names = Regex.Matches(reader.ReadToEnd(), "\"([A-Za-z0-9_]+(?:/[A-Za-z0-9_]+)*_l_\\{language\\}\\.yml)\"")
            .Select(match => match.Groups[1].Value).ToArray();
        if (names.Length != 232 || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        { throw new InvalidDataException("Неверный штатный профиль локализации."); }
        var root = Path.GetFullPath(gameRoot);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var sources = new List<(string Path, byte[] Hash)>();
        long total = 0;
        foreach (var language in new[] { "english", "russian" })
        {
            foreach (var name in names)
            {
                var path = Path.Combine(root, "localisation", language, name.Replace("{language}", language, StringComparison.Ordinal));
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > 16 * 1024 * 1024 || (total = checked(total + info.Length)) > 64 * 1024 * 1024)
                { throw new InvalidDataException("Штатный источник ключей локализации отсутствует или превышает бюджет."); }
                // ReadAllBytes bounded через лимит потока: рост файла после проверки длины также ограничен.
                using var stream = File.OpenRead(path);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int read;
                while ((read = stream.Read(chunk)) != 0)
                {
                    if (buffer.Length + read > info.Length || buffer.Length + read > 16 * 1024 * 1024)
                    { throw new InvalidDataException("Источник ключей изменился при чтении."); }
                    buffer.Write(chunk, 0, read);
                }
                var bytes = buffer.ToArray();
                if (bytes.LongLength != info.Length) { throw new InvalidDataException("Источник ключей изменился при чтении."); }
                sources.Add((path, SHA256.HashData(bytes)));
                var text = new UTF8Encoding(false, true).GetString(bytes);
                if (!Regex.IsMatch(text.TrimStart('\uFEFF'), "^\\s*l_" + language + ":", RegexOptions.CultureInvariant))
                { throw new InvalidDataException("Неверный заголовок штатного источника ключей."); }
                foreach (Match match in Regex.Matches(text, "^[ \\t]*([A-Za-z0-9_.-]+):[0-9]*[ \\t]*\"", RegexOptions.Multiline | RegexOptions.CultureInvariant))
                {
                    keys.Add(match.Groups[1].Value);
                    if (keys.Count > 250000) { throw new InvalidDataException("Превышен бюджет ключей локализации."); }
                }
            }
        }
        foreach (var source in sources)
        {
            using var stream = File.OpenRead(source.Path);
            if (!SHA256.HashData(stream).AsSpan().SequenceEqual(source.Hash))
            { throw new InvalidDataException("Штатный источник ключей изменился во время импорта."); }
        }
        return keys.ToFrozenSet(StringComparer.Ordinal);
    }
}
