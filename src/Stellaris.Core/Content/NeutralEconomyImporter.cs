using System.Collections.ObjectModel;
using Stellaris.Core.Economy;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content;

public sealed record ProjectionNote(string Source, int Line, string Area, string Status, string Reason);
public sealed record BuildingDefinition(string Id, IReadOnlyDictionary<string, decimal> Upkeep, IReadOnlyList<Modifier> Modifiers);
public sealed record EconomyProjection(ContentManifest Manifest, IReadOnlyList<ResourceDefinition> Resources, IReadOnlyList<JobDefinition> Jobs,
    IReadOnlyList<CategoryDefinition> Categories, IReadOnlyList<BuildingDefinition> Buildings, IReadOnlyList<ProjectionNote> Coverage, decimal WorkforcePerJob);

/// <summary>Численная проекция трёх обычных профессий. Не полный Script VM и не проверка всего vanilla-контента.</summary>
public sealed class NeutralEconomyImporter
{
    private static readonly string[] JOBS = ["technician", "miner", "farmer"];
    private static readonly string[] BUILDINGS = ["building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility"];
    private static readonly HashSet<string> FALSE_JOB_SWAPS = new(StringComparer.Ordinal)
    {
        "miner_is_scrap_miner_trigger", "miner_is_cave_cleaner_trigger", "miner_is_turtle_miner_trigger",
        "miner_is_crystal_miner_trigger", "miner_is_thermoforger_trigger", "farmer_is_angler_trigger",
        "farmer_is_titan_hunter_trigger", "farmer_is_puddle_technician_trigger", "farmer_is_gas_extractor_trigger",
        "technician_is_mote_harvester_trigger", "technician_is_fuel_chemist_trigger", "technician_is_shield_tuner_trigger"
    };
    private static readonly HashSet<string> FLAGS = new(StringComparer.Ordinal)
    {
        "has_energy_requisitorium", "has_material_requisitorium", "has_produce_requisitorium"
    };
    private readonly ContentSession _session;
    private readonly List<ProjectionNote> _coverage = [];
    private readonly Dictionary<string, ResourceDefinition> _resources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CategoryDefinition> _categories = new(StringComparer.Ordinal);

    public NeutralEconomyImporter(ContentSession session)
    {
        _session = session;
    }

    public EconomyProjection Import(decimal workforcePerJob)
    {
        if (workforcePerJob <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workforcePerJob));
        }
        if (!_session.Version.TrimStart('v').StartsWith("4.5.", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Профиль M1 рассчитан на исследованный контент 4.5.x, получен {_session.Version}.");
        }
        _coverage.Clear();
        _resources.Clear();
        _categories.Clear();
        _session.LoadFile("common/pop_jobs/03_worker_jobs.txt");
        _session.LoadFile("common/buildings/03_resource_buildings.txt");
        _session.LoadFolder("common/strategic_resources");
        _session.LoadFolder("common/economic_categories");

        foreach (var source in _session.Sources.Where(source => source.Path.StartsWith("common/strategic_resources/", StringComparison.Ordinal)))
        {
            foreach (var entry in source.Root.Entries.Where(entry => entry.Block != null && !entry.Key.StartsWith('@')))
            {
                var cap = entry.AsBlock().Optional("max");
                // Ресурсы без max не используются как запасы в нейтральном M1.
                if (cap != null)
                {
                    RequireAssignment(entry);
                    RequireAssignment(cap);
                    var definition = new ResourceDefinition(entry.Key, _session.Number(cap));
                    if (!_resources.TryAdd(entry.Key, definition))
                    {
                        throw Error(entry, "повтор определения ресурса");
                    }
                }
            }
        }
        foreach (var source in _session.Sources.Where(source => source.Path.StartsWith("common/economic_categories/", StringComparison.Ordinal)))
        {
            foreach (var entry in source.Root.Entries.Where(entry => entry.Block != null && !entry.Key.StartsWith('@')))
            {
                RequireAssignment(entry);
                var parentEntry = entry.AsBlock().Optional("parent");
                if (parentEntry != null)
                {
                    RequireAssignment(parentEntry);
                }
                var parent = parentEntry?.AsScalar();
                if (!_categories.TryAdd(entry.Key, new CategoryDefinition(entry.Key, parent)))
                {
                    throw Error(entry, "повтор определения категории");
                }
            }
        }

        var jobs = JOBS.Select(id => CompileJob(_session.Definition("common/pop_jobs", id), workforcePerJob)).ToArray();
        var buildings = BUILDINGS.Select(id => CompileBuilding(_session.Definition("common/buildings", id))).ToArray();
        var usedResources = new HashSet<string>(jobs.SelectMany(job => job.Production.Keys.Concat(job.Upkeep.Keys)), StringComparer.Ordinal);
        foreach (var building in buildings)
        {
            usedResources.UnionWith(building.Upkeep.Keys);
            usedResources.UnionWith(building.Modifiers.Select(modifier => modifier.Resource));
        }
        var resources = usedResources.Order(StringComparer.Ordinal).Select(id => _resources[id]).ToArray();
        var limitations = new[]
        {
            "Фиксированный контекст: обычная органическая, не кочевая империя; нет civics, traits, flags, job swaps, overlord и councilor.",
            "WorkforcePerJob задаётся явно пользователем лаборатории; делитель не выводится из POP_BULK_UNIT_SIZE и не подтверждён движком.",
            "Population upkeep, stability, habitability, happiness, growth, job weight и auto traits не входят в эту численную проекцию.",
            "Здания предустановлены; cost/eligibility/buildtime и их inline scripts не исполняются. Импортируется содержание и экономические модификаторы.",
            "Нет поддержки DLC load order, replace_path и модов. Файлы common — обнаруженный набор установки; manifest не доказывает их ванильную чистоту."
        };
        var manifest = _session.Manifest("M1-neutral-4.5-projection-v1", limitations);
        return new EconomyProjection(manifest, resources, jobs, _categories.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            buildings, _coverage.ToArray(), workforcePerJob);
    }

    private JobDefinition CompileJob(ScriptEntry definition, decimal workforcePerJob)
    {
        RequireAssignment(definition);
        var block = definition.AsBlock();
        var tables = block.Named("resources").ToArray();
        if (tables.Length != 1)
        {
            throw Error(definition, "ожидалась ровно одна resources таблица");
        }
        RequireAssignment(tables[0]);
        var resources = tables[0].AsBlock();
        var categoryEntry = resources.Required("category");
        RequireAssignment(categoryEntry);
        var category = categoryEntry.AsScalar();
        var production = CompileAmounts(resources, "produces");
        var upkeep = CompileAmounts(resources, "upkeep");
        foreach (var entry in resources.Entries)
        {
            if (entry.Key is not "category" and not "produces" and not "upkeep")
            {
                throw Error(entry, "неподдерживаемое поле исполняемой таблицы профессии");
            }
        }
        foreach (var entry in block.Entries.Where(entry => entry.Key != "resources"))
        {
            Note(entry, "job", "excluded", "Фиксированный нейтральный контекст: eligibility, weight, presentation, triggered secondary modifiers и auto traits не исполняются.");
        }
        return new JobDefinition(definition.Key, category, workforcePerJob, production, upkeep);
    }

    private BuildingDefinition CompileBuilding(ScriptEntry definition)
    {
        RequireAssignment(definition);
        var block = definition.AsBlock();
        var resourceEntry = block.Required("resources");
        RequireAssignment(resourceEntry);
        var resources = resourceEntry.AsBlock();
        var upkeep = CompileAmounts(resources, "upkeep");
        foreach (var entry in resources.Entries.Where(entry => entry.Key != "upkeep"))
        {
            if (entry.Key is not "category" and not "cost" and not "inline_script")
            {
                throw Error(entry, "неподдерживаемое поле resources здания");
            }
            Note(entry, "building/resources", "excluded", "Здание уже размещено; строительные расходы и их inline scripts за границами M1.");
        }
        var modifiers = new List<Modifier>();
        foreach (var entry in block.Entries)
        {
            if (entry.Key == "planet_modifier")
            {
                RequireAssignment(entry);
                CompileModifiers(entry.AsBlock(), modifiers);
            }
            else if (entry.Key == "triggered_planet_modifier")
            {
                RequireAssignment(entry);
                var conditional = entry.AsBlock();
                var potential = conditional.Required("potential");
                RequireAssignment(potential);
                var active = Evaluate(potential.AsBlock());
                Note(entry, "building/modifier", active ? "included" : "inactive", "Условие вычислено в фиксированном нейтральном контексте.");
                if (active)
                {
                    CompileModifiers(new ScriptBlock(conditional.Entries.Where(item => item.Key != "potential")), modifiers);
                }
            }
            else if (entry.Key != "resources")
            {
                Note(entry, "building", "excluded", "Строительство, доступность, presentation и AI вне предустановленного сценария.");
            }
        }
        return new BuildingDefinition(definition.Key, upkeep, modifiers);
    }

    private IReadOnlyDictionary<string, decimal> CompileAmounts(ScriptBlock parent, string operation)
    {
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var table in parent.Named(operation))
        {
            RequireAssignment(table);
            var block = table.AsBlock();
            var condition = block.Optional("trigger");
            if (condition != null)
            {
                RequireAssignment(condition);
            }
            var active = condition == null || Evaluate(condition.AsBlock());
            Note(table, operation, active ? "included" : "inactive", "Условие вычислено; повторяющиеся таблицы сохраняются.");
            if (!active)
            {
                continue;
            }
            foreach (var entry in block.Entries.Where(entry => entry.Key != "trigger"))
            {
                if (entry.Operator != "=" || !_resources.ContainsKey(entry.Key))
                {
                    throw Error(entry, "неизвестный ресурс/операция в активной таблице");
                }
                result[entry.Key] = checked(result.GetValueOrDefault(entry.Key) + _session.Number(entry));
            }
        }
        return new ReadOnlyDictionary<string, decimal>(result);
    }

    private void CompileModifiers(ScriptBlock block, List<Modifier> target)
    {
        foreach (var entry in block.Entries)
        {
            var matches = (from category in _categories.Keys
                           from resource in _resources.Keys
                           from operation in new[] { "produces", "upkeep" }
                           from suffix in new[] { "add", "mult" }
                           where entry.Key == $"{category}_{resource}_{operation}_{suffix}"
                           select new { category, resource, operation, suffix }).ToArray();
            if (matches.Length != 1 || entry.Operator != "=")
            {
                throw Error(entry, "неподдерживаемый активный экономический модификатор");
            }
            var match = matches[0];
            var value = _session.Number(entry);
            target.Add(new Modifier(match.category, match.resource, match.operation, match.suffix == "add" ? value : 0, match.suffix == "mult" ? value : 0));
        }
    }

    private bool Evaluate(ScriptBlock block)
    {
        // Проверяем все ветви, не скрывая неизвестные операции short-circuit вычислением.
        var values = block.Entries.Select(EvaluateEntry).ToArray();
        return values.All(value => value);
    }

    private bool EvaluateEntry(ScriptEntry entry)
    {
        if (entry.Operator != "=")
        {
            throw Error(entry, "неподдерживаемый оператор trigger");
        }
        if (entry.Key is "owner" or "owner?" or "colony" or "colony?" or "planet" or "hidden_trigger" or "AND")
        {
            return Evaluate(entry.AsBlock());
        }
        if (entry.Key is "NOT" or "NOR" or "OR")
        {
            var values = entry.AsBlock().Entries.Select(EvaluateEntry).ToArray();
            return entry.Key switch
            {
                "NOT" => !values.All(value => value),
                "NOR" => !values.Any(value => value),
                _ => values.Any(value => value)
            };
        }
        if (entry.Key == "exists")
        {
            return entry.AsScalar() is "owner" or "colony" or "planet" ? true : throw Error(entry, "неизвестный scope exists");
        }
        if (entry.Key is "has_carrier_flag" or "has_planet_flag")
        {
            if (!FLAGS.Contains(entry.AsScalar()))
            {
                throw Error(entry, "неисследованный flag в нейтральном профиле");
            }
            return false;
        }
        bool actual;
        if (entry.Key is "is_nomadic" or "is_gestalt" or "is_robot_empire" or "is_wilderness_empire" or "is_ai" or "is_fallen_empire_spiritualist")
        {
            actual = false;
        }
        else if (entry.Key is "is_regular_empire" or "always")
        {
            actual = true;
        }
        else if (FALSE_JOB_SWAPS.Contains(entry.Key))
        {
            actual = false;
        }
        else
        {
            throw Error(entry, "неподдерживаемый trigger; дополните профиль явно");
        }
        return entry.AsScalar() switch
        {
            "yes" => actual,
            "no" => !actual,
            _ => throw Error(entry, "ожидалось yes/no")
        };
    }

    private void Note(ScriptEntry entry, string area, string status, string reason)
    {
        _coverage.Add(new ProjectionNote(entry.Source, entry.Line, area + "/" + entry.Key, status, reason));
    }

    private static InvalidDataException Error(ScriptEntry entry, string reason)
    {
        return new InvalidDataException($"{entry.Source}:{entry.Line}:{entry.Column}: {entry.Key}: {reason}.");
    }

    private static void RequireAssignment(ScriptEntry entry)
    {
        if (entry.Operator != "=")
        {
            throw Error(entry, "ожидался оператор присваивания =");
        }
    }
}
