using Stellaris.Core.Economy;

namespace Stellaris.Checks;

public static class BonusWorkforceChecks
{
    public static void Run()
    {
        ProductionAndUpkeep();
        CapacityAndPartialWorkforce();
        CommandsAndHash();
        InvalidCommandsAndMonths();
        StageOrdering();
        Reproducibility();
    }

    private static void ProductionAndUpkeep()
    {
        foreach (var upkeepMult in new[] { -0.25m, 0.5m })
        {
            var (world, _, planet) = Create(100);
            world.SetJobs(planet, "first", 1);
            world.SetJobBonusWorkforceMultiplier(planet, "first", 0.2m);
            world.SetModifiers(planet, new[] { new Modifier("worker", "energy", "produces", 1, 0.25m), new Modifier("worker", "food", "upkeep", 0, upkeepMult) });
            world.SetBuildingUpkeep(planet, Amounts(("food", 5)));
            world.AdvanceMonth();
            var report = world.GetPlanetReport(planet);
            Equal(10.5m, report.Produced["energy"], "(W+B)/100 × (6+1) × 1.25");
            Equal(upkeepMult == -0.25m ? 1.8m : 3.6m, report.JobUpkeep["food"], "Bonus Workforce и job-upkeep-модификатор меняют JobUpkeep");
            Equal(5, report.BuildingUpkeep["food"], "Job-upkeep-модификатор не меняет BuildingUpkeep");
            Equal(upkeepMult == -0.25m ? 6.8m : 8.6m, report.Upkeep["food"], "JobUpkeep 1.8/3.6 плюс фиксированные 5");
            world.SetModifiers(planet, new[] { new Modifier("worker", "energy", "produces", 1, 2), new Modifier("worker", "food", "upkeep", 0, upkeepMult) });
            world.AdvanceMonth();
            report = world.GetPlanetReport(planet);
            Equal(upkeepMult == -0.25m ? 1.8m : 3.6m, report.JobUpkeep["food"], "Output не меняет JobUpkeep");
            Equal(5, report.BuildingUpkeep["food"], "Output не меняет BuildingUpkeep");
            Equal(upkeepMult == -0.25m ? 6.8m : 8.6m, report.Upkeep["food"], "Output не меняет общий расход");
        }
        var (addWorld, _, addPlanet) = Create(100);
        addWorld.SetJobs(addPlanet, "first", 1);
        addWorld.SetJobBonusWorkforceMultiplier(addPlanet, "first", 0.2m);
        addWorld.SetModifiers(addPlanet, new[]
        {
            new Modifier("worker", "energy", "produces", 1, 0.25m),
            new Modifier("all", "energy", "produces", 100, 0.25m),
            new Modifier("worker", "food", "produces", 3, 0),
            new Modifier("worker", "food", "upkeep", 1, -0.25m),
            new Modifier("all", "food", "upkeep", 100, -0.25m)
        });
        addWorld.AdvanceMonth();
        Equal(12.6m, addWorld.GetPlanetReport(addPlanet).Produced["energy"], "Родительский add не наследуется, mult складывается");
        Equal(3.6m, addWorld.GetPlanetReport(addPlanet).Produced["food"], "Точный add создаёт отсутствующий в базе выпуск");
        Equal(1.8m, addWorld.GetPlanetReport(addPlanet).Upkeep["food"], "Upkeep add до Workforce, собственные mult после");
    }

    private static void CapacityAndPartialWorkforce()
    {
        var defaultAllocation = new JobAllocation(1, 100);
        Equal(0, defaultAllocation.BonusWorkforce, "Бонус JobAllocation по умолчанию равен нулю");
        Equal(100, defaultAllocation.EffectiveWorkforce, "При нулевом бонусе EffectiveWorkforce совпадает с W");
        var (world, _, planet) = Create(200);
        world.SetJobs(planet, "first", 1);
        world.SetJobs(planet, "second", 1);
        world.SetJobBonusWorkforceMultiplier(planet, "first", 0.2m);
        world.AdvanceMonth();
        var report = world.GetPlanetReport(planet);
        Equal(100, report.Allocated["first"].Workforce, "Вакансия ограничивает обычную Workforce");
        Equal(20, report.Allocated["first"].BonusWorkforce, "Бонус W×0.2");
        Equal(120, report.Allocated["first"].EffectiveWorkforce, "Эффективная Workforce превышает вместимость вакансии");
        Equal(1, report.Allocated["first"].Count, "Count не включает бонус");
        Equal(100, report.Allocated["second"].Workforce, "Бонус первой не отнимает Workforce второй");
        Equal(0, report.UnemployedWorkforce, "Обычная Workforce распределена один раз");
        foreach (var workforce in new[] { 50m, 0m })
        {
            var (partial, _, target) = Create(workforce);
            partial.SetJobs(target, "first", 1);
            partial.SetJobBonusWorkforceMultiplier(target, "first", 0.2m);
            partial.SetBuildingUpkeep(target, Amounts(("food", 5)));
            partial.AdvanceMonth();
            var allocation = partial.GetPlanetReport(target).Allocated["first"];
            Equal(workforce * 0.2m, allocation.BonusWorkforce, "Бонус пропорционален занятости");
            Equal(workforce * 1.2m / 100 * 6, partial.GetPlanetReport(target).Produced["energy"], "Частичный/нулевой выпуск");
            Equal(workforce * 1.2m / 100 * 2, partial.GetPlanetReport(target).JobUpkeep["food"], "Bonus Workforce меняет только содержание профессии");
            Equal(5, partial.GetPlanetReport(target).BuildingUpkeep["food"], "Содержание здания не зависит от занятости");
            Equal(workforce * 1.2m / 100 * 2 + 5, partial.GetPlanetReport(target).Upkeep["food"], "При нулевой занятости остаётся только фиксированное содержание");
        }
    }

    private static void CommandsAndHash()
    {
        var (world, _, planet) = Create(100);
        world.SetJobs(planet, "first", 1);
        world.AdvanceMonth();
        var oldReport = world.GetPlanetReport(planet);
        var oldHash = world.StateHash();
        world.SetJobBonusWorkforceMultiplier(planet, "first", 0.2m);
        Different(oldHash, world.StateHash(), "Бонус меняет хеш до расчёта месяца");
        SameReport(oldReport, world.GetPlanetReport(planet));
        world.AdvanceMonth();
        Equal(7.2m, world.GetPlanetReport(planet).Produced["energy"], "Команда бонуса применена");
        oldReport = world.GetPlanetReport(planet);
        oldHash = world.StateHash();
        world.SetJobBonusWorkforceMultiplier(planet, "first", 0);
        Different(oldHash, world.StateHash(), "Сброс бонуса меняет хеш");
        SameReport(oldReport, world.GetPlanetReport(planet));
        world.AdvanceMonth();
        Equal(6, world.GetPlanetReport(planet).Produced["energy"], "Сброс бонуса применён");
        world.SetJobBonusWorkforceMultiplier(planet, "first", 0.2m);
        oldReport = world.GetPlanetReport(planet);
        oldHash = world.StateHash();
        world.SetJobs(planet, "first", 0.5m);
        Different(oldHash, world.StateHash(), "Вакансии меняют хеш до месяца");
        SameReport(oldReport, world.GetPlanetReport(planet));
        world.AdvanceMonth();
        Equal(50, world.GetPlanetReport(planet).Allocated["first"].Workforce, "Команда вакансий уменьшает W");
        Equal(10, world.GetPlanetReport(planet).Allocated["first"].BonusWorkforce, "Команда вакансий уменьшает B");
    }

    private static void InvalidCommandsAndMonths()
    {
        var (world, country, planet) = Create(100);
        world.SetJobs(planet, "first", 1);
        var before = world.StateHash();
        Reject<ArgumentOutOfRangeException>(() => world.SetJobBonusWorkforceMultiplier(planet, "first", -0.1m));
        Reject<ArgumentException>(() => world.SetJobBonusWorkforceMultiplier(planet, "missing", 1));
        Reject<ArgumentException>(() => world.SetJobBonusWorkforceMultiplier(country, "first", 1));
        Reject<ArgumentException>(() => world.SetJobBonusWorkforceMultiplier(new EntityId(planet.Index, 2), "first", 1));
        EqualHash(before, world.StateHash(), "Неверные команды атомарны");
        world.AdvanceMonth();
        world.SetModifiers(planet, new[] { new Modifier("worker", "food", "upkeep", 0, -1) });
        world.AdvanceMonth();
        Equal(0, world.GetPlanetReport(planet).Upkeep["food"], "Скидка 100% допустима");
        foreach (var modifiers in new[]
        {
            new[] { new Modifier("worker", "food", "upkeep", 0, -1.01m) },
            new[] { new Modifier("worker", "energy", "produces", 0, -2) },
            new[] { new Modifier("worker", "energy", "produces", -7, 0) },
            new[] { new Modifier("worker", "energy", "produces", -7, -2) }
        })
        {
            world.SetModifiers(planet, modifiers);
            AssertMonthFailure<InvalidOperationException>(world, planet);
        }
        world.SetModifiers(planet, Array.Empty<Modifier>());
        world.SetJobBonusWorkforceMultiplier(planet, "first", decimal.MaxValue);
        AssertMonthFailure<OverflowException>(world, planet);
    }

    private static void StageOrdering()
    {
        var world = new EconomyWorld(new[] { new ResourceDefinition("energy", decimal.MaxValue) },
            new[] { new JobDefinition("first", "worker", 100, Amounts(("energy", decimal.MaxValue)), Amounts()) },
            new[] { new CategoryDefinition("worker", null) });
        var country = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        world.AddGroup(planet, 1, 10);
        world.SetJobs(planet, "first", 1);
        world.SetModifiers(planet, new[] { new Modifier("worker", "energy", "produces", 0, 1) });
        world.AdvanceMonth();
        Equal(0.1m * decimal.MaxValue * 2, world.GetPlanetReport(planet).Produced["energy"], "Workforce масштабируется до mult: база×2 иначе переполняет decimal");
        var upkeepWorld = new EconomyWorld(new[] { new ResourceDefinition("energy", decimal.MaxValue) },
            new[] { new JobDefinition("first", "worker", 100, Amounts(), Amounts(("energy", decimal.MaxValue))) },
            new[] { new CategoryDefinition("worker", null) });
        var upkeepCountry = upkeepWorld.CreateCountry();
        upkeepWorld.SetBalance(upkeepCountry, "energy", decimal.MaxValue);
        var upkeepPlanet = upkeepWorld.CreatePlanet(upkeepCountry);
        upkeepWorld.AddGroup(upkeepPlanet, 1, 10);
        upkeepWorld.SetJobs(upkeepPlanet, "first", 1);
        upkeepWorld.SetModifiers(upkeepPlanet, new[] { new Modifier("worker", "energy", "upkeep", 0, 1) });
        upkeepWorld.AdvanceMonth();
        Equal(0.1m * decimal.MaxValue * 2, upkeepWorld.GetPlanetReport(upkeepPlanet).Upkeep["energy"], "Содержание масштабируется Workforce до cost mult, без промежуточного переполнения");
    }

    private static void Reproducibility()
    {
        var sequential = RunScenario(false);
        EqualHash(sequential, RunScenario(false), "Повтор 120 месяцев с бонусом");
        EqualHash(sequential, RunScenario(true), "Параллельные 120 месяцев с бонусом");
    }

    private static string RunScenario(bool parallel)
    {
        var (world, country, first) = Create(200);
        var second = world.CreatePlanet(country);
        world.AddGroup(second, 1, 150);
        foreach (var planet in new[] { first, second })
        {
            world.SetJobs(planet, "first", 1);
            world.SetJobs(planet, "second", 1);
            world.SetJobBonusWorkforceMultiplier(planet, "first", 0.2m);
            world.SetModifiers(planet, new[] { new Modifier("worker", "energy", "produces", 1, 0.25m), new Modifier("worker", "food", "upkeep", 0, -0.25m) });
        }
        for (var month = 1; month <= 120; month++)
        {
            if (month == 31)
            {
                world.SetJobBonusWorkforceMultiplier(first, "first", 0);
            }
            if (month == 61)
            {
                world.SetJobs(second, "first", 0.5m);
                world.SetJobBonusWorkforceMultiplier(second, "second", 0.5m);
            }
            world.AdvanceMonth(parallel);
        }
        return world.StateHash();
    }

    private static (EconomyWorld World, EntityId Country, EntityId Planet) Create(decimal workforce)
    {
        var world = new EconomyWorld(new[] { new ResourceDefinition("energy", 1000000), new ResourceDefinition("food", 1000000) },
            new[] { new JobDefinition("first", "worker", 100, Amounts(("energy", 6)), Amounts(("food", 2))), new JobDefinition("second", "worker", 100, Amounts(("energy", 1)), Amounts()) },
            new[] { new CategoryDefinition("all", null), new CategoryDefinition("worker", "all") });
        var country = world.CreateCountry();
        world.SetBalance(country, "food", 100000);
        var planet = world.CreatePlanet(country);
        world.AddGroup(planet, 1, workforce);
        return (world, country, planet);
    }

    private static void AssertMonthFailure<T>(EconomyWorld world, EntityId planet) where T : Exception
    {
        var hash = world.StateHash();
        var report = world.GetPlanetReport(planet);
        var month = world.Month;
        Reject<T>(() => world.AdvanceMonth());
        EqualHash(hash, world.StateHash(), "Ошибка месяца не меняет мир");
        SameReport(report, world.GetPlanetReport(planet));
        Equal(month, world.Month, "Ошибка месяца не двигает календарь");
        var rejected = false;
        try
        {
            world.AdvanceMonth(true);
        }
        catch (AggregateException exception) when (exception.Flatten().InnerExceptions.All(error => error is T))
        {
            rejected = true;
        }
        catch (T)
        {
            rejected = true;
        }
        if (!rejected)
        {
            throw new InvalidOperationException("Ожидался отказ параллельного месяца.");
        }
        EqualHash(hash, world.StateHash(), "Ошибка параллельного месяца не меняет мир");
        SameReport(report, world.GetPlanetReport(planet));
        Equal(month, world.Month, "Ошибка параллельного месяца не двигает календарь");
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

    private static void EqualHash(string expected, string actual, string message)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Different(string first, string second, string message)
    {
        if (first == second)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void SameReport(PlanetReport first, PlanetReport second)
    {
        if (!ReferenceEquals(first, second))
        {
            throw new InvalidOperationException("До успешного месяца должен сохраняться прежний отчёт.");
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
