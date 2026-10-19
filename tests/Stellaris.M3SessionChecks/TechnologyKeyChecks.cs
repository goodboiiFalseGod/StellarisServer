using System.Text;
using System.Text.RegularExpressions;
using Stellaris.Session.M3;

internal static class TechnologyKeyChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "Stellaris-TechnologyKeys-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var resource = typeof(M3TechnologyKeys).Assembly.GetManifestResourceStream("Stellaris.Session.M3.LocalizationPaths")!;
            using var reader = new StreamReader(resource);
            var names = Regex.Matches(reader.ReadToEnd(), "\"([A-Za-z0-9_]+(?:/[A-Za-z0-9_]+)*_l_\\{language\\}\\.yml)\"")
                .Select(match => match.Groups[1].Value).ToArray();
            foreach (var language in new[] { "english", "russian" })
            {
                var directory = Path.Combine(root, "localisation", language);
                Directory.CreateDirectory(directory);
                foreach (var name in names)
                {
                    var path = Path.Combine(directory, name.Replace("{language}", language, StringComparison.Ordinal));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path,
                        "l_" + language + ":\n tech_synthetic:0 \"PRIVATE_TRANSLATION_SENTINEL\"\n tech_synthetic_desc:0 \"OTHER_PRIVATE_TEXT\"\n", new UTF8Encoding(true));
                }
                File.WriteAllText(Path.Combine(directory, "RINFIX_l_" + language + ".yml"),
                    "l_" + language + ":\n forbidden_rinfix_key:0 \"EXCLUDED_TEXT\"\n");
            }
            var keys = M3TechnologyKeys.Import(root);
            if (!keys.SetEquals(new[] { "tech_synthetic", "tech_synthetic_desc" }))
            { throw new Exception("Ключи штатного профиля смешались с переводами или RINFIX."); }
            var missing = Path.Combine(root, "localisation", "english", names[0].Replace("{language}", "english", StringComparison.Ordinal));
            File.Delete(missing);
            try { _ = M3TechnologyKeys.Import(root); }
            catch (InvalidDataException) { return; }
            throw new Exception("Неполный штатный профиль ключей был принят.");
        }
        finally
        {
            // Абсолютный fresh GUID directory принадлежит только этому тесту.
            if (Directory.Exists(root)) { Directory.Delete(root, true); }
        }
    }
}
