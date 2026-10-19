using Stellaris.Core.Economy;

namespace Stellaris.Checks;

public static class EconomyChecks
{
    public static void Run()
    {
        AllocationAndCommands();
        Modifiers();
        Reproducibility();
        InvalidInputs();
        AtomicDeficit();
        MineralDeficitContinues();
        CapacityAndOverflow();
        TypedStorageAndStructuralUpkeep();
    }

    private static void AllocationAndCommands()
    {
        var (world, country, planet) = Create();
        world.AddGroup(planet, 2, 2);
        world.AddGroup(planet, 3, 2);
        world.SetJobs(planet, "first", 3);
        world.SetJobs(planet, "second", 4);
        world.AdvanceMonth();
        var report = world.GetPlanetReport(planet);
        Equal(6, report.Allocated["first"].Workforce, "Первой профессии отданы 6 Workforce");
        Equal(4, report.Allocated["second"].Workforce, "Вторая получает только остаток");
        Equal(0, report.UnemployedWorkforce, "Рабочая сила не расходуется дважды");
        Equal(3, report.Allocated["first"].Count, "Количество занятых профессий");
        world.SetJobs(planet, "first", 0);
        world.AdvanceMonth();
        report = world.GetPlanetReport(planet);
        Equal(0, report.Allocated["first"].Workforce, "Команда снимает первую профессию");
        Equal(8, report.Allocated["second"].Workforce, "Распределение пересчитано");
        Equal(2, report.UnemployedWorkforce, "Остаток рабочей силы");
        Equal(15, world.GetBalance(country, "energy"), "Изменение профессий влияет на баланс");
    }

    private static void Modifiers()
    {
        var (world, country, planet) = Create();
        world.AddGroup(planet, 1, 2);
        world.SetJobs(planet, "first", 1);
        world.SetModifiers(planet, new[]
        {
            new Modifier("all", "energy", "produces", 100, 0.5m),
            new Modifier("worker", "energy", "produces", 2, 0.5m),
            new Modifier("all", "food", "upkeep", 100, 0.5m),
            new Modifier("worker", "food", "upkeep", 1, 0.5m)
        });
        world.SetBalance(country, "food", 10);
        world.SetBuildingUpkeep(planet, Amounts(("food", 1)));
        world.AdvanceMonth();
        var report = world.GetPlanetReport(planet);
        Equal(10, report.Produced["energy"], "Add только точной категории, Mult складывается с предками");
        Equal(4, report.JobUpkeep["food"], "Модификаторы профессии меняют только JobUpkeep");
        Equal(1, report.BuildingUpkeep["food"], "Модификаторы профессии не меняют содержание здания");
        Equal(5, report.Upkeep["food"], "Общий расход содержит оба источника ровно один раз");
        Equal(-5, report.Net["food"], "Чистый поток");
        Equal(5, world.GetBalance(country, "food"), "Списание содержания");
        if (!report.Profile.Contains("experimental", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Отчёт должен явно указывать экспериментальный профиль.");
        }
    }

    private static void Reproducibility()
    {
        var first = Scenario();
        var second = Scenario();
        var parallel = Scenario();
        for (var month = 0; month < 120; month++)
        {
            first.AdvanceMonth();
            second.AdvanceMonth();
            parallel.AdvanceMonth(true);
        }
        if (first.Month != 120 || first.StateHash() != second.StateHash() || first.StateHash() != parallel.StateHash())
        {
            throw new InvalidOperationException("120 месяцев должны воспроизводиться последовательно и параллельно.");
        }
    }

    private static void InvalidInputs()
    {
        var (world, country, planet) = Create();
        Reject<ArgumentOutOfRangeException>(() => world.AddGroup(planet, -1, 2));
        Reject<ArgumentOutOfRangeException>(() => world.SetJobs(planet, "first", -1));
        Reject<ArgumentOutOfRangeException>(() => world.SetBalance(country, "food", -1));
        Reject<ArgumentException>(() => world.SetJobs(planet, "unknown", 1));
        Reject<ArgumentException>(() => world.CreatePlanet(planet));
        Reject<ArgumentException>(() => world.SetJobs(country, "first", 1));
        Reject<ArgumentException>(() => world.GetBalance(planet, "energy"));
        Reject<ArgumentException>(() => world.SetJobs(new EntityId(planet.Index, 2), "first", 1));
        var group = world.AddGroup(planet, 1, 2);
        Reject<ArgumentException>(() => world.SetJobs(group, "first", 1));
        Reject<ArgumentException>(() => world.SetModifiers(planet, new[] { new Modifier("worker", "energy", "unknown", 0, 0) }));
        Reject<ArgumentException>(() => new EconomyWorld(new[] { new ResourceDefinition("r", 10) }, Array.Empty<JobDefinition>(), new[] { new CategoryDefinition("a", "b"), new CategoryDefinition("b", "a") }));
    }

    private static void AtomicDeficit()
    {
        var (world, country, planet) = Create();
        world.AddGroup(planet, 1, 2);
        world.SetJobs(planet, "second", 1);
        world.AdvanceMonth();
        var previousReport = world.GetPlanetReport(planet);
        var otherCountry = world.CreateCountry();
        var otherPlanet = world.CreatePlanet(otherCountry);
        world.SetBuildingUpkeep(otherPlanet, Amounts(("food", 1)));
        var before = world.StateHash();
        Reject<InvalidOperationException>(() => world.AdvanceMonth());
        if (world.Month != 1 || world.StateHash() != before || !ReferenceEquals(previousReport, world.GetPlanetReport(planet)))
        {
            throw new InvalidOperationException("Дефицит другой страны не должен частично менять мир и отчёты.");
        }
        Reject<InvalidOperationException>(() => world.AdvanceMonth(true));
        if (world.StateHash() != before)
        {
            throw new InvalidOperationException("Параллельный отказ должен быть атомарным.");
        }
        Equal(1, world.GetBalance(country, "energy"), "Успешный предыдущий месяц сохранён");
    }

    private static void MineralDeficitContinues()
    {
        var world = new EconomyWorld(new[] { new ResourceDefinition("minerals", 100) }, Array.Empty<JobDefinition>(), Array.Empty<CategoryDefinition>());
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        world.SetBalance(country, "minerals", 3);
        world.SetBuildingUpkeep(planet, Amounts(("minerals", 5)));
        world.AdvanceMonth();
        Equal(0, world.GetBalance(country, "minerals"), "Нехватка минералов обнуляет запас");
        Equal(-5, world.GetPlanetReport(planet).Net["minerals"], "Отчёт сохраняет реальный отрицательный поток");
        world.AdvanceMonth(true);
        Equal(2, world.Month, "Следующий месяц при нуле минералов продолжается");
        Equal(0, world.GetBalance(country, "minerals"), "Повторный дефицит сохраняет нулевой запас");
    }

    private static void CapacityAndOverflow()
    {
        var (world, country, planet) = Create();
        world.SetBalance(country, "energy", 10000);
        world.AddGroup(planet, 1, 2);
        world.SetJobs(planet, "second", 1);
        world.AdvanceMonth();
        Equal(10000, world.GetBalance(country, "energy"), "Лимит ресурса ограничивает накопление");
        var before = world.StateHash();
        Reject<OverflowException>(() => world.AddGroup(planet, decimal.MaxValue, 2));
        if (before != world.StateHash())
        {
            throw new InvalidOperationException("Переполнение команды не меняет мир.");
        }
        world.SetModifiers(planet, new[] { new Modifier("worker", "energy", "produces", decimal.MaxValue, 0) });
        before = world.StateHash();
        Reject<OverflowException>(() => world.AdvanceMonth());
        if (before != world.StateHash())
        {
            throw new InvalidOperationException("Переполнение месяца не меняет мир.");
        }
    }

    private static void TypedStorageAndStructuralUpkeep()
    {
        Reject<ArgumentException>(() => new EconomyWorld(new[] { new ResourceDefinition("unity", 1, ResourceStoragePolicy.UnlimitedReserve) },
            Array.Empty<JobDefinition>(), Array.Empty<CategoryDefinition>()));
        var world = new EconomyWorld(
            new[] { new ResourceDefinition("energy", 10), new ResourceDefinition("unity", 0, ResourceStoragePolicy.UnlimitedReserve), new ResourceDefinition("minerals", 10) },
            new[] { new JobDefinition("clerk", "worker", 1, Amounts(("unity", 2), ("energy", 3)), Amounts(("energy", 1))) },
            new[] { new CategoryDefinition("worker", null) });
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        world.SetBalance(country, "unity", 100000);
        world.SetBalance(country, "energy", 10);
        world.SetBalance(country, "minerals", 3);
        world.AddGroup(planet, 1, 1);
        world.SetJobs(planet, "clerk", 1);
        world.SetBuildingUpkeep(planet, Amounts(("energy", 1)));
        world.SetDistrictUpkeep(planet, Amounts(("minerals", 2)));
        world.SetZoneUpkeep(planet, Amounts(("minerals", 1)));
        world.AdvanceMonth();
        var report = world.GetPlanetReport(planet);
        Equal(3, report.Produced["energy"], "Output energy");
        Equal(2, report.Produced["unity"], "Output unity");
        Equal(1, report.BuildingUpkeep["energy"], "Building upkeep remains separate");
        Equal(2, report.DistrictUpkeep["minerals"], "District upkeep remains separate");
        Equal(1, report.ZoneUpkeep["minerals"], "Zone upkeep remains separate");
        Equal(3, report.Upkeep["minerals"], "Aggregate includes each structural upkeep once");
        Equal(100002, world.GetBalance(country, "unity"), "Unlimited reserve is not clamped");
        Equal(10, world.GetBalance(country, "energy"), "Capped resource still clamps after net flow");
    }

    private static EconomyWorld Scenario()
    {
        var (world, country, planet) = Create();
        world.SetBalance(country, "food", 1000);
        world.AddGroup(planet, 5, 2);
        world.SetJobs(planet, "first", 3);
        world.SetJobs(planet, "second", 2);
        var another = world.CreatePlanet(country);
        world.AddGroup(another, 3, 2);
        world.SetJobs(another, "second", 3);
        return world;
    }

    private static (EconomyWorld World, EntityId Country, EntityId Planet) Create()
    {
        var world = new EconomyWorld(
            new[] { new ResourceDefinition("energy", 10000), new ResourceDefinition("food", 10000) },
            new[]
            {
                new JobDefinition("first", "worker", 2, Amounts(("energy", 3)), Amounts(("food", 1))),
                new JobDefinition("second", "worker", 2, Amounts(("energy", 1)), Amounts())
            },
            new[] { new CategoryDefinition("all", null), new CategoryDefinition("worker", "all") });
        var country = world.CreateCountry();
        world.SetBalance(country, "food", 100);
        return (world, country, world.CreatePlanet(country));
    }

    private static IReadOnlyDictionary<string, decimal> Amounts(params (string Id, decimal Value)[] values)
    {
        return values.ToDictionary(value => value.Id, value => value.Value);
    }

    private static void Equal(decimal expected, decimal actual, string message)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{message}: ожидалось {expected}, получено {actual}.");
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
}
