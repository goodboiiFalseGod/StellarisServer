using Stellaris.Core.Content.Galaxy;

internal static class GalaxyCatalogReadyChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("runtime reader preserves tagged blocks, arrays, operators, templates and math", () =>
        {
            var ast = GalaxyScriptReader.Parse("\uFEFFx = { color = hsv { .1 .2 .3 } { a b } value >= @[2 + 3] [[!OPTION] rgb = rgb { 1 2 3 } ] text = \"a\\\"b\" }", "grammar.txt");
            var fields = ast.Entries.Single().Value.Entries;
            Equal(GalaxySyntaxKind.TaggedBlock, fields[0].Value.Kind);
            Equal("hsv", fields[0].Value.Text);
            Equal(3, fields[0].Value.Entries.Count);
            Equal(GalaxySyntaxKind.Block, fields[1].Value.Kind);
            Equal(null, fields[1].Key);
            Equal(">=", fields[2].Operator);
            Equal("@[2 + 3]", fields[2].Value.Text);
            Equal(GalaxySyntaxKind.Conditional, fields[3].Value.Kind);
            Equal("[[!OPTION]", fields[3].Value.Text);
            Equal("a\"b", fields[4].Value.Text);
            Equal(true, fields[4].Value.Quoted);
        });
        check("runtime reader rejects malformed syntax with source location", () =>
        {
            foreach (var input in new[] { "x = {", "x = \"unfinished", "x = @[2", "[[BAD body ]", "x = [bad]", "x = }" })
            {
                try { GalaxyScriptReader.Parse(input, "bad.txt"); }
                catch (FormatException error) when (error.Message.StartsWith("bad.txt:", StringComparison.Ordinal)) { continue; }
                throw new Exception("Ожидался diagnostic для " + input);
            }
            var ast = GalaxyScriptReader.Parse("# comment\r\nx = 1\r\ny = 2", "lines.txt");
            Equal(3, ast.Entries[1].Location.Line);
            Equal(1, ast.Entries[1].Location.Column);
        });
        check("runtime catalog preserves order, duplicates, variables, aggregators, DLC and inline closure", () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "galaxy-catalog-check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                void Write(string path, string content)
                {
                    var target = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllText(target, content);
                }
                Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}");
                Write("common/solar_system_initializers/z.txt", "duplicate = {} missing = { initializer = absent inline_script = absent_inline } ");
                Write("common/solar_system_initializers/a.txt", "@size = 7\nfirst = { size = @size class = sc_test planet = { class = pc_test } inline_script = { script = test PARAM = abc } NOT = { host_has_dlc = \"Pack\" } local_has_dlc = \"Pack\" } duplicate = {}");
                Write("common/star_classes/a.txt", "sc_test = {} rl_stars = { stars = { sc_test } }");
                Write("common/planet_classes/a.txt", "pc_test = {} random_list = { name = rl_planets planets = { pc_test absent } }");
                Write("common/inline_scripts/test.txt", "[[PARAM] set_flag = $PARAM$ ] inline_script = cycle");
                Write("common/inline_scripts/cycle.txt", "inline_script = test");
                Write("common/scripted_variables/a.txt", "@size = 99 @global = @[2 + 3] @duplicate_global = 1");
                Write("common/scripted_variables/z.txt", "@duplicate_global = 2");
                var supplied = new List<string> { "Pack" };
                var entitlements = new GalaxyEntitlements(supplied, []); supplied.Clear();
                var catalog = GalaxyRuntimeCatalog.Import(root, entitlements);
                Equal(true, catalog.AllFilesParsed);
                Equal(false, catalog.ConditionsExecutable);
                Equal("@size", catalog.Definitions[0].Id);
                Equal("first", catalog.Initializers[0].Id);
                Equal(1, catalog.Variables.Count);
                Equal(4, catalog.GlobalVariables.Count);
                Equal(1, catalog.PlanetRandomLists.Count);
                Equal(1, catalog.StarRandomLists.Count);
                Equal(2, catalog.InlineScripts.Count);
                Equal(GalaxyLookupStatus.Ambiguous, catalog.Lookup(GalaxyRuntimeFamily.Initializer, "duplicate").Status);
                Equal(GalaxyLookupStatus.Unique, catalog.Lookup(GalaxyRuntimeFamily.Variable, "@size", "common/solar_system_initializers/a.txt").Status);
                Equal("7", catalog.Lookup(GalaxyRuntimeFamily.Variable, "@size", "common/solar_system_initializers/a.txt").Candidates.Single().Syntax.Value.Text);
                Equal("99", catalog.Lookup(GalaxyRuntimeFamily.Variable, "@size", "other.txt").Candidates.Single().Syntax.Value.Text);
                Equal(GalaxyLookupStatus.Ambiguous, catalog.Lookup(GalaxyRuntimeFamily.Variable, "@duplicate_global", "other.txt").Status);
                Equal("@[2 + 3]", catalog.Lookup(GalaxyRuntimeFamily.GlobalVariable, "@global").Candidates.Single().Syntax.Value.Text);
                Equal(true, catalog.DlcGates.Single(g => g.Field == "host_has_dlc").Entitled);
                Equal(false, catalog.DlcGates.Single(g => g.Field == "local_has_dlc").Entitled);
                Equal(true, catalog.References.Any(r => r.Target == "absent" && r.Status == "unresolved-literal"));
                Equal(true, catalog.References.Any(r => r.Target == "@size" && r.Status == "resolved"));
                Equal(true, catalog.Files.All(f => f.Sha256.Length == 64));
                Equal(2, catalog.Initializers[0].Location.Line);
                var second = GalaxyRuntimeCatalog.Import(root, entitlements);
                Equal(string.Join("|", catalog.Definitions.Select(d => d.File.Path + ":" + d.Id)), string.Join("|", second.Definitions.Select(d => d.File.Path + ":" + d.Id)));
                void Changed(Action mutation, Action restore)
                {
                    mutation();
                    try
                    {
                        try { catalog.VerifyStable(); }
                        catch (IOException) { return; }
                        throw new Exception("Изменение source membership не обнаружено.");
                    }
                    finally { restore(); }
                }
                Changed(() => Write("common/planet_classes/addition.txt", "pc_added = {}"),
                    () => File.Delete(Path.Combine(root, "common/planet_classes/addition.txt")));
                Changed(() => File.Delete(Path.Combine(root, "common/inline_scripts/cycle.txt")),
                    () => Write("common/inline_scripts/cycle.txt", "inline_script = test"));
                Changed(() => Write("common/inline_scripts/absent_inline.txt", "value = 1"),
                    () => File.Delete(Path.Combine(root, "common/inline_scripts/absent_inline.txt")));
                var missingRoot = Path.Combine(root, "missing-root-fixture");
                Write("missing-root-fixture/launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}");
                foreach (var folder in new[] { "solar_system_initializers", "star_classes", "planet_classes" })
                    Directory.CreateDirectory(Path.Combine(missingRoot, "common", folder));
                var missingCatalog = GalaxyRuntimeCatalog.Import(missingRoot, entitlements);
                Equal(true, missingCatalog.Diagnostics.Any(d => d.Code == "missing-root"));
                Directory.CreateDirectory(Path.Combine(missingRoot, "common/scripted_variables"));
                var appearanceDetected = false;
                try { missingCatalog.VerifyStable(); }
                catch (IOException) { appearanceDetected = true; }
                Equal(true, appearanceDetected);
                foreach (var marker in new[] { "{\"rawVersion\":\"v4.5.2\",\"rawVersion\":\"v4.5.2\"}", "{\"rawVersion\":452}" })
                {
                    Write("launcher-settings.json", marker);
                    try
                    {
                        try { GalaxyRuntimeCatalog.Import(root, entitlements); }
                        catch (InvalidDataException) { continue; }
                        throw new Exception("Некорректный version marker принят.");
                    }
                    finally { Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}"); }
                }
                Write("common/planet_classes/a.txt", "pc_changed = {}");
                try { catalog.VerifyStable(); }
                catch (IOException) { return; }
                throw new Exception("Изменение source не обнаружено.");
            }
            finally { Directory.Delete(root, true); }
        });
    }

    public static void RunVanilla(string root, Action<string, Action> check)
    {
        check("runtime catalog loads all vanilla 4.5.2 three families", () =>
        {
            var catalog = GalaxyRuntimeCatalog.Import(root, new GalaxyEntitlements([], []));
            var blockers = catalog.Diagnostics.Where(d => d.Code is "parse-error" or "missing-root" or "invalid-top-level" or "invalid-aggregator").ToArray();
            if (blockers.Length != 0) throw new Exception(string.Join(Environment.NewLine, blockers.Select(d => d.Message)));
            Equal(360, catalog.Initializers.Count); Equal(45, catalog.StarClasses.Count);
            Equal(69, catalog.PlanetClasses.Count); Equal(9, catalog.PlanetRandomLists.Count); Equal(88, catalog.Variables.Count);
            Equal(40, catalog.Files.Count(f => f.Path.StartsWith("common/solar_system_initializers/", StringComparison.Ordinal)));
            Equal(2, catalog.Files.Count(f => f.Path.StartsWith("common/star_classes/", StringComparison.Ordinal)));
            Equal(12, catalog.Files.Count(f => f.Path.StartsWith("common/planet_classes/", StringComparison.Ordinal)));
            Equal(23, catalog.Files.Count(f => f.Path.StartsWith("common/scripted_variables/", StringComparison.Ordinal)));
            Equal(2908, catalog.GlobalVariables.Count);
            Equal(0, catalog.References.Count(r => r.Status == "unresolved-literal"));
            Console.WriteLine($"Runtime catalog: files={catalog.Files.Count}, initializers={catalog.Initializers.Count}, stars={catalog.StarClasses.Count} (randomizers={catalog.StarRandomLists.Count}), planets={catalog.PlanetClasses.Count}, aggregators={catalog.PlanetRandomLists.Count}, variables={catalog.Variables.Count}, globals={catalog.GlobalVariables.Count}, inline={catalog.InlineScripts.Count}, unresolved={catalog.References.Count(r => r.Status == "unresolved-literal")}, gates={catalog.DlcGates.Count}");
        });
    }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}");
    }
}
