using System.IO.Compression;
using Stellaris.Core.Content.Galaxy;

internal static class GalaxyCoverageChecks
{
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Ожидалось {typeof(T).Name}");
    }
    internal static void Run(Action<string, Action> check)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "coverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Write(string path, string text)
        {
            var absolute = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, text);
        }
        try
        {
            Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}");
            Write("map/setup_scenarios/test.txt", "setup_scenario = { name = synthetic supports_shape = round } ");
            Write("map/galaxy/test.txt", "round = { stars_min_dist = 1 }");
            Write("common/solar_system_initializers/test.txt", "@scale = 1 test = { class = sc_test init_effect = { add_deposit = d_absent host_has_dlc = \"Synthetic DLC\" } } neighbor = { initializer = test }");
            Write("common/star_classes/test.txt", "sc_test = { } ");
            Write("common/scripted_effects/test.txt", "synthetic_effect = { } ");
            Write("common/inline_scripts/test.txt", "if = { synthetic_effect = yes } if = { }");
            Write("common/defines/test.txt", "NTest = { COUNT = 3 }");
            Write("common/broken/test.txt", "broken = {");
            Directory.CreateDirectory(Path.Combine(root, "dlc"));
            var archivePath = Path.Combine(root, "dlc/synthetic.zip");
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("common/star_classes/extra.txt").Open())) writer.Write("sc_test = { }");
                using (var writer = new StreamWriter(zip.CreateEntry("gfx/ignored.txt").Open())) writer.Write("not a script");
            }
            GalaxyCoverageManifest manifest = null!;
            check("coverage inventory archive provenance, parse gaps and independent states", () =>
            {
                manifest = GalaxyCoverageManifest.Scan(root);
                Equal(9, manifest.Files.Count);
                Equal(1, manifest.Archives.Count);
                Equal(2, manifest.Archives[0].Entries);
                Equal(1, manifest.Archives[0].ScriptFiles);
                Equal(1, manifest.Files.Count(f => !f.Parsed));
                Equal(true, manifest.Files.Single(f => !f.Parsed).Diagnostic!.Contains("common/broken/test.txt", StringComparison.Ordinal));
                Equal("events,prescripted_countries", string.Join(',', manifest.MissingRoots));
                Equal(false, manifest.ResolvedCatalog);
                Equal(false, manifest.VanillaContentComplete);
                Equal("launcher-settings.json", manifest.VersionMarker.Path);
                Equal(64, manifest.VersionMarker.Sha256.Length);
                Equal(true, manifest.Definitions.All(d => d.Executable != GalaxyCoverageLevel.Complete && d.ConditionEvaluable == GalaxyCoverageLevel.NotAssessed));
                Equal(1, manifest.DuplicateIds.Count);
                Equal("common/star_classes:sc_test", manifest.DuplicateIds[0]);
                Equal(true, manifest.Definitions.Any(d => d.Source.StartsWith("dlc/synthetic.zip!", StringComparison.Ordinal)));
                Equal(2, manifest.Definitions.Count(d => d.Kind == GalaxyInventoryEntryKind.InlineTemplateEntry));
                Equal(1, manifest.Definitions.Count(d => d.Kind == GalaxyInventoryEntryKind.Configuration));
                Throws<NotSupportedException>(() => ((IList<GalaxyCoverageFile>)manifest.Files).Clear());
            });
            check("coverage literal links, DLC gates and unclassified construct locations", () =>
            {
                var system = manifest.Definitions.Single(d => d.Id == "test");
                Equal("Synthetic DLC", system.DlcGates.Single().Name);
                Equal(true, system.DlcGates.Single().Line > 0);
                Equal("ambiguous-duplicate", system.References.Single(r => r.Target == "sc_test").Status);
                Equal("unresolved-literal-target", system.References.Single(r => r.Target == "d_absent").Status);
                Equal(GalaxyCoverageLevel.Partial, system.Linked);
                Equal("present-unresolved-profile", manifest.Definitions.Single(d => d.Id == "neighbor").References.Single().Status);
                Equal(1, system.Constructs.Single(c => c.Name == "add_deposit").Count);
                Equal(true, manifest.ToJson().Contains("NotAssessed", StringComparison.Ordinal));
            });
            check("coverage stability detects modifications and new/deleted members", () =>
            {
                Write("common/star_classes/new.txt", "new_star = { }");
                Throws<IOException>(manifest.VerifyStable);
                File.Delete(Path.Combine(root, "common/star_classes/new.txt"));
                manifest.VerifyStable();
                var old = File.ReadAllText(Path.Combine(root, "common/star_classes/test.txt"));
                Write("common/star_classes/test.txt", old + " ");
                Throws<IOException>(manifest.VerifyStable);
                Write("common/star_classes/test.txt", old);
                manifest.VerifyStable();
                File.Delete(Path.Combine(root, "common/broken/test.txt"));
                Throws<IOException>(manifest.VerifyStable);
                Write("common/broken/test.txt", "broken = {");
                manifest.VerifyStable();
            });
            check("coverage missing-root appearance, archive mutation and version gate", () =>
            {
                Directory.CreateDirectory(Path.Combine(root, "events"));
                Throws<IOException>(manifest.VerifyStable);
                Directory.Delete(Path.Combine(root, "events"));
                manifest.VerifyStable();
                using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update)) zip.CreateEntry("common/star_classes/new.txt");
                Throws<IOException>(manifest.VerifyStable);
                Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.1\"}");
                Throws<InvalidDataException>(() => GalaxyCoverageManifest.Scan(root));
            });
        }
        finally
        {
            if (Path.GetDirectoryName(root) == AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) Directory.Delete(root, true);
        }
    }
}
