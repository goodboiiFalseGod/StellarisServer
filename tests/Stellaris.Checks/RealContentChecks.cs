using System.Globalization;
using System.Text;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;

namespace Stellaris.Checks;

public static class RealContentChecks
{
    private static readonly (string Job, string Resource, string Building, decimal Base)[] CASES =
    {
        ("technician", "energy", "building_energy_grid", 6),
        ("miner", "minerals", "building_mineral_purification_plant", 4),
        ("farmer", "food", "building_food_processing_facility", 6)
    };

    public static void Run(string gameRoot)
    {
        var session = new ContentSession(gameRoot);
        if (session.Version.TrimStart('v') != "4.5.2")
        {
            throw new InvalidDataException($"Интеграционный gate требует Stellaris v4.5.2; обнаружена {session.Version}.");
        }
        var projection = new NeutralEconomyImporter(session).Import(100);
        foreach (var item in CASES)
        {
            var job = projection.Jobs.Single(job => job.Id == item.Job);
            Equal(item.Base, job.Production.GetValueOrDefault(item.Resource), $"{item.Job}: базовый выпуск из внешнего контента");
            Equal(100, job.WorkforcePerJob, $"{item.Job}: нормализация Workforce");
            var building = projection.Buildings.Single(building => building.Id == item.Building);
            Equal(2, building.Upkeep.GetValueOrDefault("energy"), $"{item.Building}: содержание energy");
            var modifiers = building.Modifiers.Where(modifier => modifier.Category == job.Category && modifier.Resource == item.Resource && modifier.Operation == "produces").ToArray();
            if (modifiers.Length != 1)
            {
                throw new InvalidDataException($"{item.Building}: ожидался один модификатор выпуска категории {job.Category}, найдено {modifiers.Length}.");
            }
            Equal(0.5m, modifiers[0].Add, $"{item.Building}: прибавка выпуска");
            Equal(0, modifiers[0].Mult, $"{item.Building}: множитель выпуска");
        }
        var sequential = RunScenarios(projection, false);
        var repeated = RunScenarios(projection, false);
        var parallel = RunScenarios(projection, true);
        if (sequential != repeated || sequential != parallel)
        {
            throw new InvalidOperationException("Реальный контент: повторный/параллельный расчёт отличается по хешу состояния или чистым потокам.");
        }
        session.VerifyStable();
        Console.WriteLine($"Реальный контент {session.Version}: проверены 3 профессии × 0/50/100/200 Workforce; SHA-256 {projection.Manifest.Files.Count} источников неизменны.");
    }

    private static string RunScenarios(EconomyProjection projection, bool parallel)
    {
        var world = new EconomyWorld(projection.Resources, projection.Jobs, projection.Categories);
        var scenarios = new List<(EntityId Country, EntityId Planet, string Job, string Resource, decimal Workforce, decimal Base)>();
        foreach (var item in CASES)
        {
            var building = projection.Buildings.Single(building => building.Id == item.Building);
            foreach (var workforce in new[] { 0m, 50m, 100m, 200m })
            {
                var country = world.CreateCountry();
                foreach (var resource in projection.Resources)
                {
                    world.SetBalance(country, resource.Id, 1000);
                }
                var planet = world.CreatePlanet(country);
                world.AddGroup(planet, workforce, 1);
                world.SetJobs(planet, item.Job, 2);
                world.SetModifiers(planet, building.Modifiers);
                world.SetBuildingUpkeep(planet, building.Upkeep);
                scenarios.Add((country, planet, item.Job, item.Resource, workforce, item.Base));
            }
        }
        world.AdvanceMonth(parallel);
        var result = new StringBuilder(world.StateHash());
        foreach (var scenario in scenarios)
        {
            var report = world.GetPlanetReport(scenario.Planet);
            var expectedProduction = (scenario.Base + 0.5m) * scenario.Workforce / 100;
            var context = $"{scenario.Job}, Workforce={scenario.Workforce}";
            Equal(expectedProduction, report.Produced.GetValueOrDefault(scenario.Resource), $"{context}: производство");
            Equal(0, report.JobUpkeep.GetValueOrDefault("energy"), $"{context}: в нейтральной ветви нет содержания профессии");
            Equal(2, report.BuildingUpkeep.GetValueOrDefault("energy"), $"{context}: собственное содержание здания");
            Equal(2, report.Upkeep.GetValueOrDefault("energy"), $"{context}: общий расход без двойного учёта");
            Equal(scenario.Workforce, report.Allocated[scenario.Job].Workforce, $"{context}: занятая Workforce");
            Equal(0, report.UnemployedWorkforce, $"{context}: безработная Workforce");
            foreach (var resource in projection.Resources)
            {
                var expectedNet = (resource.Id == scenario.Resource ? expectedProduction : 0) - (resource.Id == "energy" ? 2 : 0);
                Equal(expectedNet, report.Net.GetValueOrDefault(resource.Id), $"{context}: чистый поток {resource.Id}");
                Equal(1000 + expectedNet, world.GetBalance(scenario.Country, resource.Id), $"{context}: баланс {resource.Id}");
                result.Append('|').Append(resource.Id).Append('=').Append(report.Net.GetValueOrDefault(resource.Id).ToString(CultureInfo.InvariantCulture));
            }
        }
        return result.ToString();
    }

    private static void Equal(decimal expected, decimal actual, string context)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"Реальный контент: {context}; ожидалось {expected}, получено {actual}.");
        }
    }
}
