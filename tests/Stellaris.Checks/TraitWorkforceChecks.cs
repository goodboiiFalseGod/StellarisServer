using System.Security.Cryptography;
using System.Text;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;

namespace Stellaris.Checks;

public static class TraitWorkforceChecks
{
    private static readonly JobDefinition[] JOBS =
    {
        new("technician", "worker", 100, Amounts(("energy", 6)), Amounts(("food", 2))),
        new("miner", "worker", 100, Amounts(("minerals", 4)), Amounts()),
        new("farmer", "worker", 100, Amounts(("food", 6)), Amounts())
    };

    public static void Run()
    {
        DirectCompositeAndVariables();
        RejectInvalidTraits();
        RejectInvalidMappings();
        WorldIntegration();
    }

    public static void RunReal(string root)
    {
        var session = new ContentSession(root);
        Check(session.Version.TrimStart('v') == "4.5.2", "Реальные traits требуют v4.5.2.");
        var jobs = new NeutralEconomyImporter(session).Import(100).Jobs;
        var importer = new TraitWorkforceImporter(session);
        var separate = new[] { ("trait_ingenious", "technician"), ("trait_industrious", "miner"), ("trait_agrarian", "farmer") };
        foreach (var (trait, expectedJob) in separate)
        {
            var result = importer.Import(new[] { trait }, jobs);
            foreach (var job in jobs)
            {
                Equal(job.Id == expectedJob ? 0.15m : 0, result.JobMultipliers[job.Id], $"{trait}/{job.Id}: реальный бонус");
            }
        }
        var combined = importer.Import(new[] { "trait_ingenious", "trait_industrious", "trait_agrarian", "trait_syncretic_proles" }, jobs);
        foreach (var job in jobs)
        {
            Equal(0.25m, combined.JobMultipliers[job.Id], $"Реальные составные бонусы {job.Id}");
        }
        var robot = importer.Import(new[] { "trait_robot_efficient_processors" }, jobs);
        foreach (var job in jobs)
        {
            Equal(0.05m, robot.JobMultipliers[job.Id], "Общий бонус извлечён; доступность роботического вида не заявляется");
        }
        Reject<InvalidDataException>(() => importer.Import(new[] { "trait_aquatic" }, jobs));
        Reject<InvalidDataException>(() => importer.Import(new[] { "trait_weak" }, jobs));
        Check(combined.Manifest.Files.Any(file => file.Path == "common/static_modifiers/24_static_modifiers_jobs.txt"), "Манифест включает реальные составные модификаторы");
        Check(combined.Coverage.Count > 0, "Реальная проекция публикует границы покрытия");
        var direct = importer.Import(new[] { "trait_ingenious" }, jobs);
        var world = CreateWorld(JOBS);
        var country = world.CreateCountry();
        world.SetBalance(country, "food", 100);
        var planet = world.CreatePlanet(country);
        world.AddGroup(planet, 100, 1);
        world.SetJobs(planet, "technician", 1);
        world.SetJobBonusWorkforceMultiplier(planet, "technician", direct.JobMultipliers["technician"]);
        world.AdvanceMonth();
        Equal(6.9m, world.GetPlanetReport(planet).Produced["energy"], "Реальный trait_ingenious: W=100, base=6");
        Equal(2.3m, world.GetPlanetReport(planet).Upkeep["food"], "Синтетическое содержание с настоящим бонусом");
        Check(Replay(combined.JobMultipliers, false) == Replay(combined.JobMultipliers, true), "Реальные bonuses: 120 месяцев serial/parallel");
        session.VerifyStable();
        Console.WriteLine($"Traits v4.5.2: .15/.25, robot .05, aquatic/weak rejection; {combined.Manifest.Files.Count} SHA-256 источников.");
    }

    private static void DirectCompositeAndVariables()
    {
        using var fixture = new Fixture();
        fixture.Write("common/scripted_variables/global.txt", "@global=0.05");
        fixture.Traits("@local=0.15 direct={allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult=@global pop_technician_bonus_workforce_mult=@local army_damage_mult=0.2 pop_physicist_bonus_workforce_mult=0.4 pop_cat_ruler_bonus_workforce_mult=0.3}} grouped={allowed_archetypes={BIOLOGICAL} modifier={worker_bundle_bonus_workforce_mult=0.1}}");
        fixture.Mappings("@factor=0.5 worker_bundle_bonus_workforce_mult={custom_tooltip=ignored pop_cat_worker_bonus_workforce_mult=1 nested_bonus_workforce_mult=@factor} nested_bonus_workforce_mult={pop_technician_bonus_workforce_mult=2}");
        var result = fixture.Import("direct", "grouped");
        Equal(0.4m, result.JobMultipliers["technician"], "Общий + прямой + worker + рекурсивный коэффициент");
        Equal(0.15m, result.JobMultipliers["miner"], "Worker и общий бонус miner");
        Equal(0.15m, result.JobMultipliers["farmer"], "Worker и общий бонус farmer");
        Check(result.Traits.Count == 2 && result.Coverage.Count > 0, "Trait IDs и исключённые эффекты опубликованы");
        Check(result.Manifest.Files.Any(file => file.Path == "common/static_modifiers/24_static_modifiers_jobs.txt"), "Static24 попал в манифест");
        foreach (var file in result.Manifest.Files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(fixture.Root, file.Path));
            Check(file.Sha256 == Convert.ToHexString(SHA256.HashData(bytes)) && file.Bytes == bytes.LongLength, "Манифест совпадает с источниками");
        }
        var none = fixture.Import();
        Check(none.JobMultipliers.Values.All(value => value == 0), "Пустой набор признаков нейтрален");
    }

    private static void RejectInvalidTraits()
    {
        using var fixture = new Fixture();
        fixture.Traits("ok={allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult=0.1}}");
        Reject<ArgumentException>(() => new TraitWorkforceImporter(new ContentSession(fixture.Root)).Import(null!, JOBS));
        Reject<ArgumentException>(() => fixture.Import(""));
        Reject<ArgumentException>(() => fixture.Import("ok", "ok"));
        Reject<InvalidDataException>(() => fixture.Import("missing"));
        foreach (var body in new[]
        {
            "modifier={pop_bonus_workforce_mult=0.1}",
            "allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult=-0.1}",
            "allowed_archetypes={BIOLOGICAL} modifier={unknown_bonus_workforce_mult=0.1}",
            "allowed_archetypes={BIOLOGICAL} modifier={pop_unknown_job_bonus_workforce_mult=0.1}",
            "allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult>0.1}",
            "allowed_archetypes={BIOLOGICAL} modifier!={pop_bonus_workforce_mult=0.1}",
            "allowed_archetypes={BIOLOGICAL} triggered_pop_group_modifier={potential={always=yes} pop_bonus_workforce_mult=0.1}",
            "allowed_archetypes={BIOLOGICAL} triggered_pop_group_modifier={modifier={pop_bonus_workforce_mult=0.1}}",
            "allowed_archetypes={BIOLOGICAL} inline_script=some_script",
            "allowed_archetypes={BIOLOGICAL} auto_mod=yes modifier={pop_bonus_workforce_mult=0.1}",
            "allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult=@missing}",
            "allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult={value=0.1}}"
        })
        {
            fixture.Traits("bad={" + body + "}");
            Reject<InvalidDataException>(() => fixture.Import("bad"));
        }
        fixture.Traits("@cycle=@cycle bad={allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult=@cycle}}");
        Reject<InvalidDataException>(() => fixture.Import("bad"));
        fixture.Traits("bad={allowed_archetypes={BIOLOGICAL}} bad={allowed_archetypes={BIOLOGICAL}}");
        Reject<InvalidDataException>(() => fixture.Import("bad"));
    }

    private static void RejectInvalidMappings()
    {
        using var fixture = new Fixture();
        fixture.Traits("bad={allowed_archetypes={BIOLOGICAL} modifier={bundle_bonus_workforce_mult=0.1}}");
        foreach (var mapping in new[]
        {
            "bundle_bonus_workforce_mult={bundle_bonus_workforce_mult=1}",
            "bundle_bonus_workforce_mult={other_bonus_workforce_mult=1} other_bonus_workforce_mult={bundle_bonus_workforce_mult=1}",
            "bundle_bonus_workforce_mult={unknown_bonus_workforce_mult=1}",
            "bundle_bonus_workforce_mult={pop_technician_bonus_workforce_mult=-1}",
            "bundle_bonus_workforce_mult={pop_technician_bonus_workforce_mult>1}",
            "bundle_bonus_workforce_mult!={pop_technician_bonus_workforce_mult=1}",
            "bundle_bonus_workforce_mult={pop_technician_bonus_workforce_mult=@missing}",
            "bundle_bonus_workforce_mult={pop_technician_bonus_workforce_mult=1} bundle_bonus_workforce_mult={pop_miner_bonus_workforce_mult=1}"
        })
        {
            fixture.Mappings(mapping);
            Reject<InvalidDataException>(() => fixture.Import("bad"));
        }
    }

    private static void WorldIntegration()
    {
        using var fixture = new Fixture();
        fixture.Traits("bonus={allowed_archetypes={BIOLOGICAL} modifier={pop_bonus_workforce_mult=0.2}}");
        var projection = fixture.Import("bonus");
        var world = CreateWorld(JOBS);
        var country = world.CreateCountry();
        world.SetBalance(country, "food", 100);
        var planet = world.CreatePlanet(country);
        world.AddGroup(planet, 50, 1);
        world.SetJobs(planet, "technician", 1);
        world.SetBuildingUpkeep(planet, Amounts(("energy", 1)));
        world.SetJobBonusWorkforceMultiplier(planet, "technician", projection.JobMultipliers["technician"]);
        world.AdvanceMonth();
        var report = world.GetPlanetReport(planet);
        Equal(50, report.Allocated["technician"].Workforce, "Частичная обычная занятость");
        Equal(10, report.Allocated["technician"].BonusWorkforce, "Частичный импортированный бонус");
        Equal(3.6m, report.Produced["energy"], "Частичный выпуск");
        Equal(1.2m, report.JobUpkeep["food"], "Частичное содержание профессии");
        Equal(1, report.BuildingUpkeep["energy"], "Содержание здания отделено от профессии");
        var beforeFailure = world.StateHash();
        fixture.Traits("bad={allowed_archetypes={BIOLOGICAL} triggered_pop_group_modifier={pop_bonus_workforce_mult=1}}");
        Reject<InvalidDataException>(() => fixture.Import("bad"));
        Check(beforeFailure == world.StateHash(), "Ошибка импорта не меняет существующий мир");
        world.SetJobs(planet, "technician", 0);
        world.AdvanceMonth();
        report = world.GetPlanetReport(planet);
        Equal(0, report.Allocated["technician"].BonusWorkforce, "Без занятости нет бонуса");
        Equal(0, report.Produced["energy"], "Без занятости нет выпуска");
        Equal(0, report.JobUpkeep["food"], "Без занятости нет содержания профессии");
        Equal(1, report.BuildingUpkeep["energy"], "Содержание здания остаётся");
        var first = Replay(projection.JobMultipliers, false);
        Check(first == Replay(projection.JobMultipliers, false) && first == Replay(projection.JobMultipliers, true), "Импортированные bonuses: повтор и параллельные 120 месяцев");
    }

    private static string Replay(IReadOnlyDictionary<string, decimal> multipliers, bool parallel)
    {
        var world = CreateWorld(JOBS);
        var planets = new List<EntityId>();
        for (var index = 0; index < 4; index++)
        {
            var country = world.CreateCountry();
            world.SetBalance(country, "food", 1000);
            var planet = world.CreatePlanet(country);
            world.AddGroup(planet, 300, 1);
            foreach (var job in JOBS)
            {
                world.SetJobs(planet, job.Id, 1);
                world.SetJobBonusWorkforceMultiplier(planet, job.Id, multipliers[job.Id]);
            }
            planets.Add(planet);
        }
        for (var month = 0; month < 120; month++)
        {
            if (month == 60)
            {
                foreach (var planet in planets)
                {
                    world.SetJobs(planet, "technician", 0.5m);
                    world.SetJobBonusWorkforceMultiplier(planet, "miner", 0);
                }
            }
            world.AdvanceMonth(parallel);
        }
        return world.StateHash();
    }

    private static EconomyWorld CreateWorld(IReadOnlyList<JobDefinition> jobs)
    {
        return new EconomyWorld(new[] { new ResourceDefinition("energy", 100000), new ResourceDefinition("minerals", 100000), new ResourceDefinition("food", 100000) }, jobs, new[] { new CategoryDefinition("worker", null) });
    }

    private static IReadOnlyDictionary<string, decimal> Amounts(params (string Id, decimal Value)[] values)
    {
        return values.ToDictionary(value => value.Id, value => value.Value);
    }

    private static void Equal(decimal expected, decimal actual, string context)
    {
        Check(expected == actual, $"{context}: ожидалось {expected}, получено {actual}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Ожидалось исключение {typeof(T).Name}.");
    }

    private sealed class Fixture : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "Stellaris-TraitChecks-" + Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Write("launcher-settings.json", "{\"rawVersion\":\"4.5.2\"}");
            Write("common/scripted_variables/global.txt", "");
            Write("common/pop_jobs/jobs.txt", "technician={} miner={} farmer={} physicist={}");
            Traits("");
            Mappings("");
        }

        public TraitWorkforceProjection Import(params string[] ids)
        {
            var session = new ContentSession(Root);
            return new TraitWorkforceImporter(session).Import(ids, JOBS);
        }

        public void Traits(string content)
        {
            Write("common/traits/traits.txt", content);
        }

        public void Mappings(string content)
        {
            Write("common/static_modifiers/24_static_modifiers_jobs.txt", content);
        }

        public void Write(string relative, string content)
        {
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            var absolute = Path.GetFullPath(Root);
            var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("Stellaris-TraitChecks-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Небезопасный путь временной fixture.");
            }
            Directory.Delete(absolute, true);
        }
    }
}
