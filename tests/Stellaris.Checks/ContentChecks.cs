using System.Security.Cryptography;
using System.Text;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;

namespace Stellaris.Checks;

public static class ContentChecks
{
    public static void Run()
    {
        VariablesAndManifest();
        ImportAndEconomy();
        Neutral120Months();
        RejectUnknownActiveContent();
        InactiveContentAndCoverage();
        VersionAndInputValidation();
    }

    private static void VariablesAndManifest()
    {
        using var fixture = new Fixture();
        fixture.Write("common/scripted_variables/global.txt", "@base=7 @alias=@base @shared=11");
        fixture.Write("common/example/local.txt", "@shared=3.5 @local=@shared first=@local second=@alias negative=-1.25 missing=@missing cycle=@cycle @cycle=@cycle math=@[1 + 2]");
        var session = new ContentSession(fixture.Root);
        session.LoadFolder("common/scripted_variables");
        var source = session.LoadFile("common/example/local.txt");
        Equal(3.5m, session.Number(source.Root.Required("first")), "Локальная переменная перекрывает глобальную");
        Equal(7, session.Number(source.Root.Required("second")), "Цепочка глобальных переменных");
        Equal(-1.25m, session.Number(source.Root.Required("negative")), "Десятичное число invariant culture");
        Reject<InvalidDataException>(() => session.Number(source.Root.Required("missing")));
        Reject<InvalidDataException>(() => session.Number(source.Root.Required("cycle")));
        Reject<InvalidDataException>(() => session.Number(source.Root.Required("math")));
        Check(ReferenceEquals(source, session.LoadFile("common/example/local.txt")), "Повторная загрузка использует прежний AST");
        Reject<ArgumentException>(() => session.LoadFile("../outside.txt"));

        var manifest = session.Manifest("synthetic", new[] { "fixture" });
        Check(manifest.Version == "4.5.0" && manifest.Profile == "synthetic", "Версия и профиль manifest");
        Check(manifest.Files.Count == 3, "Manifest содержит settings и два уникальных источника");
        Check(manifest.Files.Select(file => file.Path).SequenceEqual(manifest.Files.Select(file => file.Path).Order(StringComparer.Ordinal)), "Стабильный порядок manifest");
        foreach (var file in manifest.Files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(fixture.Root, file.Path));
            Check(file.Bytes == bytes.LongLength && file.Sha256 == Convert.ToHexString(SHA256.HashData(bytes)), "SHA-256 и длина источника");
        }
        fixture.Write("common/example/local.txt", "changed=yes");
        Reject<IOException>(session.VerifyStable);
        Reject<IOException>(() => session.Manifest("synthetic", Array.Empty<string>()));

        using var duplicate = new Fixture();
        duplicate.Write("common/scripted_variables/first.txt", "@value=1");
        duplicate.Write("common/scripted_variables/second.txt", "@value=2");
        Reject<InvalidDataException>(() => new ContentSession(duplicate.Root).LoadFolder("common/scripted_variables"));

        using var wrongLocal = new Fixture();
        wrongLocal.Write("common/example/local.txt", "@base!=6 value=@base");
        var localSession = new ContentSession(wrongLocal.Root);
        var wrongSource = localSession.LoadFile("common/example/local.txt");
        Reject<InvalidDataException>(() => localSession.Number(wrongSource.Root.Required("value")));

        using var wrongGlobal = new Fixture();
        wrongGlobal.Write("common/scripted_variables/global.txt", "@base!=6 @alias=@base");
        wrongGlobal.Write("common/example/local.txt", "value=@alias");
        var globalSession = new ContentSession(wrongGlobal.Root);
        globalSession.LoadFolder("common/scripted_variables");
        var globalSource = globalSession.LoadFile("common/example/local.txt");
        Reject<InvalidDataException>(() => globalSession.Number(globalSource.Root.Required("value")));
    }

    private static void ImportAndEconomy()
    {
        using var fixture = new Fixture();
        var projection = Import(fixture);
        Check(projection.Jobs.Count == 3 && projection.Buildings.Count == 3, "Три профессии и здания");
        Equal(10, projection.WorkforcePerJob, "Явная единица Workforce");
        Equal(8, projection.Jobs.Single(job => job.Id == "technician").Production["energy"], "Повторяющиеся produces суммируются");
        Check(projection.Manifest.Limitations.Count > 0 && projection.Coverage.Count > 0, "Ограничения и покрытие опубликованы");
        var world = new EconomyWorld(projection.Resources, projection.Jobs, projection.Categories);
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        world.AddGroup(planet, 1, 10);
        world.SetJobs(planet, "technician", 1);
        var grid = projection.Buildings.Single(building => building.Id == "building_energy_grid");
        world.SetModifiers(planet, grid.Modifiers);
        world.SetBuildingUpkeep(planet, grid.Upkeep);
        world.AdvanceMonth();
        var report = world.GetPlanetReport(planet);
        Equal(13.5m, report.Produced["energy"], "Импортированные add и mult применяются к выпуску: (8+1)*1.5");
        Equal(0, report.JobUpkeep["energy"], "Нейтральный technician не создаёт содержание профессии");
        Equal(2, report.BuildingUpkeep["energy"], "Импортированное содержание здания");
        Equal(2, report.Upkeep["energy"], "Общий расход здания учтён один раз");
        Equal(11.5m, world.GetBalance(country, "energy"), "Интеграция импорта и месячной транзакции");
        world.SetJobs(planet, "technician", 0);
        world.AdvanceMonth();
        Equal(9.5m, world.GetBalance(country, "energy"), "Изменение вакансий пересчитывает импортированную экономику");
    }

    private static void RejectUnknownActiveContent()
    {
        using var unknownResource = new Fixture();
        unknownResource.WriteJobs("produces={ unknown_resource=1 }");
        Reject<InvalidDataException>(() => Import(unknownResource));
        using var unknownTrigger = new Fixture();
        unknownTrigger.WriteJobs("produces={ trigger={ always=no unknown_trigger=yes } energy=1 }");
        Reject<InvalidDataException>(() => Import(unknownTrigger));
        using var unknownOperation = new Fixture();
        unknownOperation.WriteJobs("unsupported_operation={ energy=1 }");
        Reject<InvalidDataException>(() => Import(unknownOperation));
        using var unknownModifier = new Fixture();
        unknownModifier.WriteBuildings("planet_modifier={ unknown_modifier=1 }");
        Reject<InvalidDataException>(() => Import(unknownModifier));
        using var wrongAmountOperator = new Fixture();
        wrongAmountOperator.WriteJobs("produces={ energy>1 }");
        Reject<InvalidDataException>(() => Import(wrongAmountOperator));
        using var wrongProducesOperator = new Fixture();
        wrongProducesOperator.WriteJobs("produces!={energy=6}");
        Reject<InvalidDataException>(() => Import(wrongProducesOperator));
        using var wrongTriggerOperator = new Fixture();
        wrongTriggerOperator.WriteJobs("produces={trigger!={always=yes} energy=6}");
        Reject<InvalidDataException>(() => Import(wrongTriggerOperator));
        using var wrongCategoryOperator = new Fixture();
        wrongCategoryOperator.Write("common/pop_jobs/03_worker_jobs.txt", "technician={resources={category!=worker produces={energy=1}}} miner={resources={category=worker produces={minerals=4}}} farmer={resources={category=worker produces={food=5}}}");
        Reject<InvalidDataException>(() => Import(wrongCategoryOperator));
    }

    private static void Neutral120Months()
    {
        using var fixture = new Fixture();
        fixture.Write("common/strategic_resources/resources.txt", "energy={max=100000} minerals={max=100000} food={max=100000}");
        fixture.Write("common/economic_categories/categories.txt", "planet_jobs={} technician_jobs={parent=planet_jobs} miner_jobs={parent=planet_jobs} farmer_jobs={parent=planet_jobs}");
        fixture.Write("common/pop_jobs/03_worker_jobs.txt", "technician={resources={category=technician_jobs produces={energy=6}}} miner={resources={category=miner_jobs produces={minerals=4}}} farmer={resources={category=farmer_jobs produces={food=6}}}");
        fixture.Write("common/buildings/03_resource_buildings.txt", "building_energy_grid={resources={category=planet_jobs upkeep={energy=2}} planet_modifier={technician_jobs_energy_produces_add=0.5}} building_mineral_purification_plant={resources={category=planet_jobs upkeep={energy=2}} planet_modifier={miner_jobs_minerals_produces_add=0.5}} building_food_processing_facility={resources={category=planet_jobs upkeep={energy=2}} planet_modifier={farmer_jobs_food_produces_add=0.5}}");
        var projection = new NeutralEconomyImporter(new ContentSession(fixture.Root)).Import(100);
        var sequential = RunNeutral(projection, false);
        var repeated = RunNeutral(projection, false);
        var parallel = RunNeutral(projection, true);
        Check(sequential == repeated && sequential == parallel, "120 месяцев импорта воспроизводятся при повторе и параллельной стратегии");
    }

    private static string RunNeutral(EconomyProjection projection, bool parallel)
    {
        var world = new EconomyWorld(projection.Resources, projection.Jobs, projection.Categories);
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        foreach (var resource in projection.Resources)
        {
            world.SetBalance(country, resource.Id, 1000);
        }
        world.AddGroup(planet, 3, 100);
        world.AddGroup(planet, 3, 100);
        foreach (var job in projection.Jobs)
        {
            world.SetJobs(planet, job.Id, 2);
        }
        world.SetModifiers(planet, projection.Buildings.SelectMany(building => building.Modifiers).ToArray());
        world.SetBuildingUpkeep(planet, projection.Buildings.SelectMany(building => building.Upkeep)
            .GroupBy(amount => amount.Key).ToDictionary(group => group.Key, group => group.Sum(amount => amount.Value)));
        for (var month = 1; month <= 120; month++)
        {
            if (month == 61)
            {
                world.SetJobs(planet, "technician", 1);
            }
            world.AdvanceMonth(parallel);
            var report = world.GetPlanetReport(planet);
            Equal(month <= 60 ? 7 : 0.5m, report.Net["energy"], "Чистый выпуск energy до и после команды");
            Equal(9, report.Net["minerals"], "Чистый выпуск minerals");
            Equal(13, report.Net["food"], "Чистый выпуск food");
            Equal(month <= 60 ? 0 : 100, report.UnemployedWorkforce, "Остаток Workforce до и после команды");
        }
        Equal(1450, world.GetBalance(country, "energy"), "Energy после 120 месяцев");
        Equal(2080, world.GetBalance(country, "minerals"), "Minerals после 120 месяцев");
        Equal(2560, world.GetBalance(country, "food"), "Food после 120 месяцев");
        return world.StateHash();
    }

    private static void InactiveContentAndCoverage()
    {
        using var fixture = new Fixture();
        fixture.WriteJobs("produces={ trigger={ is_gestalt=yes } unknown_inactive_resource=@missing } produces={energy=2}");
        fixture.WriteBuildings("triggered_planet_modifier={ potential={is_nomadic=yes} unknown_inactive_modifier=@missing }");
        var projection = Import(fixture);
        Equal(10, projection.Jobs.Single(job => job.Id == "technician").Production["energy"], "Неактивная таблица исключена, активная суммируется");
        Check(projection.Coverage.Any(note => note.Status == "inactive" && note.Area == "produces/produces"), "Неактивный выпуск отражён в покрытии");
        Check(projection.Coverage.Any(note => note.Status == "inactive" && note.Area == "building/modifier/triggered_planet_modifier"), "Неактивный модификатор отражён в покрытии");
    }

    private static void VersionAndInputValidation()
    {
        using var fixture = new Fixture();
        Reject<ArgumentOutOfRangeException>(() => new NeutralEconomyImporter(new ContentSession(fixture.Root)).Import(0));
        fixture.Write("launcher-settings.json", "{\"rawVersion\":\"3.5.3\"}");
        Reject<InvalidDataException>(() => Import(fixture));
        fixture.Write("launcher-settings.json", "{\"rawVersion\":\"4.5.0\"}");
        var session = new ContentSession(fixture.Root);
        fixture.Write("launcher-settings.json", "{\"rawVersion\":\"4.5.1\"}");
        Reject<IOException>(() => new NeutralEconomyImporter(session).Import(10));
    }

    private static EconomyProjection Import(Fixture fixture)
    {
        return new NeutralEconomyImporter(new ContentSession(fixture.Root)).Import(10);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal(decimal expected, decimal actual, string message)
    {
        Check(expected == actual, $"{message}: ожидалось {expected}, получено {actual}.");
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
        public readonly string Root = Path.Combine(Path.GetTempPath(), "Stellaris-ContentChecks-" + Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Write("launcher-settings.json", "{\"rawVersion\":\"4.5.0\"}");
            Write("common/strategic_resources/resources.txt", "energy={max=1000} minerals={max=1000} food={max=1000}");
            Write("common/economic_categories/categories.txt", "planet_jobs={} worker={parent=planet_jobs}");
            WriteJobs("");
            WriteBuildings("");
        }

        public void Dispose()
        {
            var absolute = Path.GetFullPath(Root);
            var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("Stellaris-ContentChecks-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Небезопасный путь временной fixture.");
            }
            Directory.Delete(absolute, true);
        }

        public void Write(string relative, string content)
        {
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        public void WriteJobs(string extra)
        {
            Write("common/pop_jobs/03_worker_jobs.txt", "@base=6 technician={resources={category=worker produces={energy=@base} produces={energy=2} " + extra + "}} miner={resources={category=worker produces={minerals=4}}} farmer={resources={category=worker produces={food=5}}}");
        }

        public void WriteBuildings(string extra)
        {
            Write("common/buildings/03_resource_buildings.txt", "building_energy_grid={resources={category=planet_jobs upkeep={energy=2}} planet_modifier={worker_energy_produces_add=1 planet_jobs_energy_produces_mult=0.5} " + extra + "} building_mineral_purification_plant={resources={category=planet_jobs}} building_food_processing_facility={resources={category=planet_jobs}}");
        }
    }
}
