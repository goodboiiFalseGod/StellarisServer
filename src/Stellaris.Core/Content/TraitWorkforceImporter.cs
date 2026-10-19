using System.Collections.ObjectModel;
using Stellaris.Core.Economy;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content;

public sealed record TraitWorkforceProjection(ContentManifest Manifest, IReadOnlyList<string> Traits, IReadOnlyDictionary<string, decimal> JobMultipliers, IReadOnlyList<ProjectionNote> Coverage);

/// <summary>Бонусы однородного вида; доступность признаков и прочие эффекты остаются вне M1.</summary>
public sealed class TraitWorkforceImporter
{
    private const string COMPOSITES_PATH = "common/static_modifiers/24_static_modifiers_jobs.txt";
    private static readonly HashSet<string> WORKER_JOBS = new(StringComparer.Ordinal) { "technician", "miner", "farmer" };
    private static readonly HashSet<string> OTHER_JOBS = new(StringComparer.Ordinal)
    {
        "mining_drone", "low_tech_miner", "technician_drone", "low_tech_technician", "agri_drone", "low_tech_farmer",
        "ark_harvester", "ark_harvester_gestalt", "physicist", "biologist", "engineer", "brain_drone_physicist",
        "brain_drone_biologist", "brain_drone_engineer", "calculator_physicist", "calculator_biologist", "calculator_engineer",
        "low_tech_researcher", "experiment_engineer", "experiment_engineer_drone", "bureaucrat", "coordinator", "synapse_drone",
        "livestock", "livestock_lithoid", "livestock_infernal", "foundry", "fabricator", "alloy_drone", "low_tech_laborer",
        "artisan", "artisan_drone", "trader", "shroud_trader", "logistics_drone", "entertainer", "cyberdome_duelist",
        "bio_trophy", "maintenance_drone", "evaluator", "enforcer", "dystopian_enforcer", "telepath", "dystopian_telepath",
        "patrol_drone", "telepath_drone", "roboticist", "identity_designer", "replicator", "soldier", "warrior_drone",
        "knight_commander", "knight", "researcher"
    };
    private static readonly HashSet<string> OTHER_CATEGORIES = new(StringComparer.Ordinal)
    {
        "ruler", "specialist", "simple_drone", "complex_drone", "civilian", "maintenance_drone", "cruise_passenger", "cruise_passenger_unemployment"
    };
    private readonly ContentSession _session;

    public TraitWorkforceImporter(ContentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public TraitWorkforceProjection Import(IReadOnlyList<string> traitIds, IReadOnlyList<JobDefinition> jobs)
    {
        ArgumentNullException.ThrowIfNull(traitIds);
        ArgumentNullException.ThrowIfNull(jobs);
        if (!_session.Version.TrimStart('v').StartsWith("4.5.", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Импорт traits M1 рассчитан на контент 4.5.x, получен {_session.Version}.");
        }
        var traits = traitIds.Order(StringComparer.Ordinal).ToArray();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in traits)
        {
            if (string.IsNullOrWhiteSpace(id) || !selected.Add(id))
            {
                throw new ArgumentException($"Пустой либо повторный ID признака: '{id}'.", nameof(traitIds));
            }
        }
        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var job in jobs.OrderBy(job => job?.Id, StringComparer.Ordinal))
        {
            if (job == null || string.IsNullOrWhiteSpace(job.Id) || !WORKER_JOBS.Contains(job.Id) || !rates.TryAdd(job.Id, 0))
            {
                throw new ArgumentException("Импорт traits M1 принимает уникальные профессии technician/miner/farmer.", nameof(jobs));
            }
        }
        var coverage = new List<ProjectionNote>();
        if (traits.Length > 0)
        {
            _session.LoadFolder("common/scripted_variables");
            _session.LoadFolder("common/traits");
            foreach (var id in traits)
            {
                var definition = _session.Definition("common/traits", id);
                RequireAssignment(definition);
                var block = definition.AsBlock();
                RejectUnsupported(block);
                var archetypes = block.Optional("allowed_archetypes") ?? throw Error(definition, "нет allowed_archetypes; признаки лидеров не поддерживаются");
                RequireAssignment(archetypes);
                archetypes.AsBlock();
                foreach (var entry in block.Entries)
                {
                    if (entry.Key == "modifier")
                    {
                        RequireAssignment(entry);
                        Compile(entry.AsBlock(), 1, rates, coverage, new HashSet<string>(StringComparer.Ordinal));
                    }
                    else
                    {
                        Note(coverage, entry, "excluded", "Сценарий задаёт существующий вид; доступность, стоимость, архетип, противоположности и прочие метаданные не исполняются.");
                    }
                }
            }
        }
        var manifest = _session.Manifest("M1-trait-workforce-v1", new[]
        {
            "Однородный вид, только безусловные положительные бонусы обычных technician/miner/farmer; traits eligibility не проверяется.",
            "Прочие прямые эффекты и известные бонусы других профессий/страт исключены с Coverage; полный стек модификаторов не реализован."
        });
        return new TraitWorkforceProjection(manifest, Array.AsReadOnly(traits), new ReadOnlyDictionary<string, decimal>(rates), coverage.AsReadOnly());
    }

    private void Compile(ScriptBlock block, decimal coefficient, Dictionary<string, decimal> rates, List<ProjectionNote> coverage, HashSet<string> stack)
    {
        RejectUnsupported(block);
        foreach (var entry in block.Entries)
        {
            RequireAssignment(entry);
            if (entry.Key == "custom_tooltip")
            {
                entry.AsScalar();
                Note(coverage, entry, "excluded", "Подсказка составного модификатора не влияет на расчёт.");
                continue;
            }
            if (entry.Block != null)
            {
                throw Error(entry, "неподдерживаемый вложенный исполняемый блок модификатора");
            }
            if (!entry.Key.Contains("bonus_workforce", StringComparison.Ordinal))
            {
                Note(coverage, entry, "excluded", "Прямой эффект вне бонусной Workforce M1.");
                continue;
            }
            var raw = _session.Number(entry);
            if (raw < 0)
            {
                throw Error(entry, "отрицательный бонус/коэффициент Workforce не поддержан");
            }
            var value = checked(coefficient * raw);
            if (entry.Key is "pop_bonus_workforce_mult" or "pop_cat_worker_bonus_workforce_mult")
            {
                foreach (var job in rates.Keys.ToArray())
                {
                    rates[job] = checked(rates[job] + value);
                }
                Note(coverage, entry, "included", "Общий/рабочий бонус применён ко всем выбранным рабочим профессиям.");
                continue;
            }
            const string suffix = "_bonus_workforce_mult";
            if (entry.Key.StartsWith("pop_", StringComparison.Ordinal) && entry.Key.EndsWith(suffix, StringComparison.Ordinal))
            {
                var name = entry.Key[4..^suffix.Length];
                if (WORKER_JOBS.Contains(name))
                {
                    if (rates.ContainsKey(name))
                    {
                        rates[name] = checked(rates[name] + value);
                        Note(coverage, entry, "included", $"Бонус применён профессии {name}.");
                    }
                    else
                    {
                        Note(coverage, entry, "excluded", $"Профессия {name} отсутствует в выбранном сценарии.");
                    }
                    continue;
                }
                if (OTHER_JOBS.Contains(name) || name.StartsWith("cat_", StringComparison.Ordinal) && OTHER_CATEGORIES.Contains(name[4..]))
                {
                    Note(coverage, entry, "excluded", "Известная профессия/страта вне M1; бонус не применяется.");
                    continue;
                }
                throw Error(entry, "неизвестный leaf бонусной Workforce");
            }
            if (entry.Key.StartsWith("pop_", StringComparison.Ordinal))
            {
                throw Error(entry, "неподдерживаемое имя бонуса Workforce");
            }
            var composites = _session.LoadFile(COMPOSITES_PATH).Root;
            var matches = composites.Named(entry.Key).ToArray();
            if (matches.Length != 1)
            {
                throw Error(entry, $"ожидалось одно составное определение Workforce, найдено {matches.Length}");
            }
            if (!stack.Add(entry.Key))
            {
                throw Error(entry, "цикл составных модификаторов Workforce");
            }
            var composite = matches[0];
            RequireAssignment(composite);
            Compile(composite.AsBlock(), value, rates, coverage, stack);
            stack.Remove(entry.Key);
            Note(coverage, entry, "included", "Составной модификатор раскрыт из внешнего static_modifiers с умножением коэффициента.");
        }
    }

    private static void RejectUnsupported(ScriptBlock block)
    {
        foreach (var entry in block.Entries)
        {
            if (entry.Key.StartsWith("triggered_", StringComparison.Ordinal) || entry.Key == "inline_script")
            {
                throw Error(entry, "условный/inline эффект выбранного trait не поддержан");
            }
            if (entry.Key == "auto_mod")
            {
                RequireAssignment(entry);
                if (entry.AsScalar() != "no")
                {
                    throw Error(entry, "автоматическая смена trait не поддержана; ожидается auto_mod=no");
                }
            }
            if (entry.Block != null)
            {
                RejectUnsupported(entry.Block);
            }
        }
    }

    private static void Note(List<ProjectionNote> coverage, ScriptEntry entry, string status, string reason)
    {
        coverage.Add(new ProjectionNote(entry.Source, entry.Line, "trait/" + entry.Key, status, reason));
    }

    private static void RequireAssignment(ScriptEntry entry)
    {
        if (entry.Operator != "=")
        {
            throw Error(entry, "ожидался оператор присваивания =");
        }
    }

    private static InvalidDataException Error(ScriptEntry entry, string reason)
    {
        return new InvalidDataException($"{entry.Source}:{entry.Line}:{entry.Column}: {entry.Key}: {reason}.");
    }
}
