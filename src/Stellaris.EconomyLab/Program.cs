using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Encodings.Web;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;

try
{
    return Run(args);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Ошибка: {exception.Message}");
    return 1;
}

static int Run(string[] arguments)
{
    if (arguments.Length == 0 || arguments[0] == "help")
    {
        Console.WriteLine("Экономическая лаборатория M1 (.NET 8). Не полная экономика Stellaris.");
        Console.WriteLine("inspect --game-root PATH --workforce-per-job NUMBER --output PATH");
        Console.WriteLine("simulate --game-root PATH --scenario PATH --output PATH [--parallel yes|no]");
        Console.WriteLine("benchmark --game-root PATH --workforce-per-job NUMBER --output PATH [--planets N] [--months N] [--parallel yes|no]");
        return 0;
    }
    var command = arguments[0];
    if (command is not "inspect" and not "simulate" and not "benchmark")
    {
        throw new ArgumentException($"Неизвестная команда: {command}");
    }
    var options = Options(arguments.Skip(1).ToArray());
    var allowed = new HashSet<string>(["--game-root", "--output", "--workforce-per-job", "--scenario", "--parallel", "--planets", "--months"], StringComparer.Ordinal);
    foreach (var key in options.Keys)
    {
        if (!allowed.Contains(key))
        {
            throw new ArgumentException($"Неизвестная опция: {key}");
        }
    }
    var scenario = command == "simulate" ? JsonSerializer.Deserialize<Scenario>(File.ReadAllText(Required(options, "--scenario")), JsonOptions())
        ?? throw new InvalidDataException("Пустой сценарий.") : null;
    var workforcePerJob = scenario?.WorkforcePerJob ?? decimal.Parse(Required(options, "--workforce-per-job"), CultureInfo.InvariantCulture);
    if (scenario != null)
    {
        ValidateScenario(scenario);
    }
    var session = new ContentSession(Required(options, "--game-root"));
    var projection = new NeutralEconomyImporter(session).Import(workforcePerJob);
    var traits = new TraitWorkforceImporter(session).Import(scenario?.Traits ?? [], projection.Jobs);
    var output = Path.GetFullPath(Required(options, "--output"));
    var gameRoot = Path.GetFullPath(Required(options, "--game-root")).TrimEnd(Path.DirectorySeparatorChar);
    if (output.Equals(gameRoot, StringComparison.OrdinalIgnoreCase) || output.StartsWith(gameRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Отчёты нельзя записывать в установку игры.");
    }
    Directory.CreateDirectory(output);
    Save(output, "projection.json", projection);
    Save(output, "trait-workforce.json", traits);
    Console.WriteLine($"Контент: {session.Version}; профиль: {projection.Manifest.Profile}; файлов: {projection.Manifest.Files.Count}.");
    Console.WriteLine("Внимание: нормализация Workforce задана явно; полного сравнения с Clausewitz нет.");
    if (traits.Traits.Count > 0)
    {
        Console.WriteLine($"Видовые traits: {traits.Traits.Count}; применена только бонусная Workforce. Покрытие и исключения: trait-workforce.json.");
    }
    if (command == "inspect")
    {
        Console.WriteLine($"Отчёт импорта: {output}");
        return 0;
    }
    var parallel = options.GetValueOrDefault("--parallel", "no") switch
    {
        "yes" => true,
        "no" => false,
        _ => throw new ArgumentException("--parallel: ожидается yes или no.")
    };
    if (scenario != null)
    {
        var first = Simulate(projection, traits, scenario, parallel);
        var second = Simulate(projection, traits, scenario, !parallel);
        if (first.Hash != second.Hash)
        {
            throw new InvalidOperationException("Повторный/параллельный прогон отличается от эталона.");
        }
        Save(output, "simulation.json", first);
        Console.WriteLine($"Месяцев: {scenario.Months}; хеш: {first.Hash}; повтор/parallel: PASS.");
    }
    else
    {
        var planets = int.Parse(options.GetValueOrDefault("--planets", "600"), CultureInfo.InvariantCulture);
        var months = int.Parse(options.GetValueOrDefault("--months", "120"), CultureInfo.InvariantCulture);
        if (planets < 1 || planets > 10000 || months < 1 || months > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Нагрузка: 1–10000 планет и месяцев.");
        }
        var result = Benchmark(projection, planets, months, parallel);
        Save(output, "benchmark.json", result);
        Console.WriteLine($"Планет: {planets}; месяцев: {months}; p95: {result.P95Milliseconds:F3} мс; режим parallel={parallel}.");
    }
    session.VerifyStable();
    Console.WriteLine($"Результаты: {output}");
    return 0;
}

static SimulationResult Simulate(EconomyProjection content, TraitWorkforceProjection traits, Scenario scenario, bool parallel)
{
    var world = NewWorld(content);
    var country = world.CreateCountry();
    foreach (var resource in content.Resources)
    {
        world.SetBalance(country, resource.Id, scenario.InitialBalance);
    }
    var planet = world.CreatePlanet(country);
    foreach (var population in scenario.Groups)
    {
        world.AddGroup(planet, population, scenario.WorkforcePerPop);
    }
    foreach (var job in scenario.Jobs)
    {
        world.SetJobs(planet, job.Key, job.Value);
    }
    ApplyBuildings(world, planet, content, scenario.Buildings);
    foreach (var bonus in traits.JobMultipliers.Where(pair => pair.Value != 0))
    {
        world.SetJobBonusWorkforceMultiplier(planet, bonus.Key, bonus.Value);
    }
    var journal = new List<object>();
    for (var month = 1; month <= scenario.Months; month++)
    {
        foreach (var item in scenario.Commands.Where(item => item.BeforeMonth == month))
        {
            world.SetJobs(planet, item.Job, item.Count);
            journal.Add(new { BeforeMonth = month, Command = "SetJobs", item.Job, item.Count });
        }
        world.AdvanceMonth(parallel);
        journal.Add(new
        {
            Month = world.Month,
            Balances = content.Resources.ToDictionary(resource => resource.Id, resource => world.GetBalance(country, resource.Id)),
            Report = world.GetPlanetReport(planet)
        });
    }
    return new SimulationResult(world.Month, world.StateHash(), journal);
}

static BenchmarkResult Benchmark(EconomyProjection content, int planets, int months, bool parallel)
{
    var world = NewWorld(content);
    var countries = Enumerable.Range(0, 20).Select(_ => world.CreateCountry()).ToArray();
    foreach (var country in countries)
    {
        foreach (var resource in content.Resources)
        {
            world.SetBalance(country, resource.Id, Math.Min(1000, resource.Capacity));
        }
    }
    for (var index = 0; index < planets; index++)
    {
        var planet = world.CreatePlanet(countries[index % countries.Length]);
        foreach (var size in new[] { 150m, 200m, 250m })
        {
            world.AddGroup(planet, size, 1);
        }
        foreach (var job in content.Jobs)
        {
            world.SetJobs(planet, job.Id, 200 / content.WorkforcePerJob);
        }
        ApplyBuildings(world, planet, content, content.Buildings.Select(building => building.Id).ToArray());
    }
    world.AdvanceMonth(parallel);
    var samples = new double[months];
    var allocatedBefore = GC.GetTotalAllocatedBytes(true);
    var collectionsBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
    var timer = new Stopwatch();
    for (var month = 0; month < months; month++)
    {
        timer.Restart();
        world.AdvanceMonth(parallel);
        samples[month] = timer.Elapsed.TotalMilliseconds;
    }
    var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    var collections = Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - collectionsBefore[index]).ToArray();
    Array.Sort(samples);
    using var process = Process.GetCurrentProcess();
    return new BenchmarkResult(planets, countries.Length, months, parallel, Percentile(samples, .5), Percentile(samples, .95),
        Percentile(samples, .99), samples[^1], allocated, process.WorkingSet64, collections, Environment.ProcessorCount,
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        world.StateHash(), "Один warmup месяц не входит в измерения. 3 группы и 3 профессии на планету; без роста/полной экономики; это не прогноз скорости партии Stellaris.");
}

static void ApplyBuildings(EconomyWorld world, EntityId planet, EconomyProjection content, IReadOnlyList<string> buildingIds)
{
    var upkeep = new Dictionary<string, decimal>(StringComparer.Ordinal);
    var modifiers = new List<Modifier>();
    if (buildingIds.Distinct(StringComparer.Ordinal).Count() != buildingIds.Count)
    {
        throw new InvalidDataException("Повтор здания в сценарии M1.");
    }
    foreach (var id in buildingIds)
    {
        var building = content.Buildings.SingleOrDefault(item => item.Id == id) ?? throw new InvalidDataException($"Неизвестное здание: {id}");
        foreach (var pair in building.Upkeep)
        {
            upkeep[pair.Key] = checked(upkeep.GetValueOrDefault(pair.Key) + pair.Value);
        }
        modifiers.AddRange(building.Modifiers);
    }
    world.SetBuildingUpkeep(planet, upkeep);
    world.SetModifiers(planet, modifiers);
}

static EconomyWorld NewWorld(EconomyProjection content)
{
    return new EconomyWorld(content.Resources, content.Jobs, content.Categories);
}

static void ValidateScenario(Scenario scenario)
{
    if (scenario.Months < 1 || scenario.Months > 10000 || scenario.WorkforcePerJob <= 0 || scenario.WorkforcePerPop < 0 || scenario.InitialBalance < 0
        || scenario.Groups == null || scenario.Jobs == null || scenario.Buildings == null || scenario.Commands == null || scenario.Traits == null)
    {
        throw new InvalidDataException("Некорректные параметры сценария.");
    }
    if (scenario.Commands.Any(command => command.BeforeMonth < 1 || command.BeforeMonth > scenario.Months || command.Count < 0))
    {
        throw new InvalidDataException("Команда за пределами сценария либо отрицательное число профессий.");
    }
}

static double Percentile(double[] sorted, double percentile)
{
    return sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
}

static Dictionary<string, string> Options(string[] arguments)
{
    if (arguments.Length % 2 != 0)
    {
        throw new ArgumentException("Ожидались пары --option value.");
    }
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < arguments.Length; index += 2)
    {
        if (!arguments[index].StartsWith("--", StringComparison.Ordinal) || !result.TryAdd(arguments[index], arguments[index + 1]))
        {
            throw new ArgumentException($"Некорректная/повторная опция: {arguments[index]}");
        }
    }
    return result;
}

static string Required(Dictionary<string, string> options, string name)
{
    return options.TryGetValue(name, out var value) ? value : throw new ArgumentException($"Нужен аргумент {name}");
}

static JsonSerializerOptions JsonOptions()
{
    return new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}

static void Save(string directory, string name, object value)
{
    File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, JsonOptions()));
}

internal sealed record Scenario(int Months, decimal WorkforcePerJob, decimal[] Groups, decimal WorkforcePerPop, Dictionary<string, decimal> Jobs,
    decimal InitialBalance, string[] Buildings, ScenarioCommand[] Commands, string Notes)
{
    public string[] Traits { get; init; } = [];
}
internal sealed record ScenarioCommand(int BeforeMonth, string Job, decimal Count);
internal sealed record SimulationResult(int Months, string Hash, IReadOnlyList<object> Journal);
internal sealed record BenchmarkResult(int Planets, int Countries, int MeasuredMonths, bool Parallel, double P50Milliseconds, double P95Milliseconds,
    double P99Milliseconds, double MaxMilliseconds, long AllocatedBytes, long WorkingSetBytes, int[] GcCollections, int LogicalProcessors, string Runtime,
    string Os, string Hash, string Limitations);
