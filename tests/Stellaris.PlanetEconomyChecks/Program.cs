using Stellaris.Core.Content.PlanetEconomy;

if (args.Length == 4 && args[0] == "--game-root" && args[2] == "--write-asset-inventory")
{
    var source = await new PlanetEconomyImporter().ImportAsync(args[1]);
    await File.WriteAllTextAsync(args[3], System.Text.Json.JsonSerializer.Serialize(new
    {
        Buildings = source.Buildings.Values.Select(item => new { item.Id, item.IconId, item.NameKey, item.Provenance.RelativePath }),
        Zones = source.Zones.Values.Select(item => new { item.Id, item.IconId, item.NameKey, item.Provenance.RelativePath }),
        Jobs = source.Jobs.Values.Select(item => new { item.Id, item.IconId, item.LocalIconPath, item.IconConceptId, item.IconAlias, item.JobStratum, item.Provenance.RelativePath }),
        Resources = source.Resources.Keys
    }));
    return 0;
}

if (args.Length != 0 && (args.Length != 2 && args.Length != 4 || args[0] != "--game-root"
    || args.Length == 4 && args[2] != "--write-coverage-report"))
{
    Console.Error.WriteLine("Использование: Stellaris.PlanetEconomyChecks [--game-root PATH].");
    return 2;
}

var checks = new List<(string Name, Func<Task> Run)>
{
    ("Обычные job-условия, типы stock и неизменяемые данные", FixtureCatalog),
    ("Недостающий обязательный source отвергается", MissingSource),
    ("Malformed обязательный source отвергается", MalformedSource),
    ("Повторное требуемое job definition отвергается", AmbiguousJob),
    ("Повторное building definition отвергается", AmbiguousBuilding),
    ("Изменение source во время импорта отвергается", SourceMutation),
    ("Junction в scripted_variables блокируется до чтения внешнего файла", ReparseVariableDirectory)
};
if (args.Length >= 2) checks.Add(("Установленный Stellaris 4.5.2", () => RealSource(args[1], args.Length == 4 ? args[3] : null)));

var failures = 0;
foreach (var (name, run) in checks)
{
    try
    {
        await run();
        Console.WriteLine("PASS: " + name);
    }
    catch (CheckSkippedException error) { Console.WriteLine("SKIP: " + error.Message); }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {name}\n{error}");
    }
}
Console.WriteLine($"Проверок: {checks.Count}; ошибок: {failures}.");
return failures == 0 ? 0 : 1;

static async Task FixtureCatalog()
{
    using var fixture = await Fixture.Create();
    var catalog = await new PlanetEconomyImporter().ImportAsync(fixture.Root);
    Assert(catalog.Jobs.Count == Fixture.JobIds.Length, "Каталог jobs неполон.");
    Assert(catalog.Buildings.Count == 12 && catalog.Districts.Count == 4 && catalog.Zones.Count == 3, "Набор recipe definitions неполон.");
    Assert(catalog.Jobs["technician"].JobStratum == "worker" && catalog.Jobs["physicist"].JobStratum == "specialist"
        && catalog.Jobs["politician"].JobStratum == "ruler", "Source job stratum потерян или смешан с resources.category.");
    Assert(catalog.Jobs["technician"].SourceJobTags.SequenceEqual(new[] { "fixture_worker" })
        && catalog.Jobs["physicist"].SourceJobTags.Count == 0, "Source job_tags не сохранены буквально или синтезированы.");
    Assert(catalog.Jobs.ContainsKey("physicist") && catalog.Jobs.ContainsKey("biologist") && catalog.Jobs.ContainsKey("engineer"), "Research jobs не разделены.");
    Assert(!catalog.Jobs.ContainsKey("researcher") && !catalog.Jobs.ContainsKey("metallurgist"), "Выдуман alias job.");
    var artisan = catalog.Jobs["artisan"].Terms;
    Assert(artisan.First(term => term.TargetId == "minerals" && term.Kind == PlanetEconomyTermKind.ResourceUpkeep).Value == 6, "Ordinary artisan mineral upkeep потерян.");
    Assert(artisan.First(term => term.TargetId == "minerals" && term.Kind == PlanetEconomyTermKind.ResourceUpkeep).ConditionDisposition == PlanetEconomyConditionDisposition.ActiveInOrdinaryContext, "Non-catalytic upkeep не выбрана.");
    Assert(artisan.First(term => term.TargetId == "food" && term.Kind == PlanetEconomyTermKind.ResourceUpkeep).ConditionDisposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext, "Catalytic upkeep не исключён из ordinary контекста.");
    Assert(artisan.First(term => term.TargetId == "trade" && term.Kind == PlanetEconomyTermKind.ResourceProduction).ConditionDisposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext, "Special swap output не исключён.");
    Assert(catalog.Jobs["artisan"].OmittedSpecialBranches.Count >= 2, "Omitted special branch provenance потеряно.");
    Assert(catalog.Resources["trade"].StoragePolicy == PlanetEconomyStoragePolicy.SourceCapped && catalog.Resources["trade"].SourceMaximum == 50000, "Trade cap отличается.");
    foreach (var science in new[] { "physics_research", "society_research", "engineering_research", "unity" })
    {
        Assert(catalog.Resources[science].StoragePolicy == PlanetEconomyStoragePolicy.UnlimitedReserve && catalog.Resources[science].SourceMaximum is null, $"Storage policy {science} ошибочна.");
    }
    var capacity = catalog.Buildings["building_capital"].Terms.Single(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacity && term.TargetId == "technician");
    Assert(capacity.Value == 100 && capacity.SourceKey == "job_technician_add", "Raw capacity/исходный ключ нормализованы или потеряны.");
    Assert(catalog.Buildings["building_capital"].Terms.Any(term => term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == "planet_amenities_add"), "Planet modifier не отделён от stock.");
    Assert(catalog.Zones["zone_research_unity"].IncludedBuildingSets.Contains("research") && catalog.Zones["zone_research_unity"].IncludedBuildingSets.Contains("unity"),
        "Zone building-set inclusion потерян.");
    Assert(!catalog.Resources.ContainsKey("planet_amenities_add"), "Planet modifier попал в resource stock.");
    Assert(catalog.Jobs["technician"].Terms.Any(term => term.TargetId == "energy" && term.ConditionDisposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext
        && term.Condition is not null && term.Value == 2), "Неподдержанное условие стало unconditional либо потеряло source value.");
    Assert(catalog.Sources.All(source => source.Sha256.Length == 64 && source.RelativePath.Length > 0), "Нет source hash/path provenance.");
    Assert(catalog.Jobs["clerk"].LocalIconPath == "gfx/interface/icons/jobs/job_clerk.dds" && catalog.Jobs["clerk"].IconAlias == "concept_clerks", "Asset metadata clerk потеряна.");
    Assert(catalog.Jobs["merchant"].LocalIconPath == "gfx/interface/icons/jobs/job_merchant.dds", "Локальный icon fallback merchant потерян.");
    Assert(catalog.Jobs["merchant"].Terms.Count == 0 && catalog.Jobs["merchant"].EconomicCategory is null
        && catalog.Jobs["merchant"].AvailabilityDisposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext,
        "Merchant source with possible=always no получил выдуманную экономику.");
    Assert(artisan.Single(term => term.Kind == PlanetEconomyTermKind.ResourceMultiplier).SourceExpression == "value:fixture_ethics_multiplier|ETHIC|materialist|",
        "Динамический source multiplier не сохранён как выражение.");
    Assert(((IDictionary<string, PlanetEconomyJob>)catalog.Jobs).IsReadOnly, "Job map изменяема.");
    Assert(((IList<PlanetEconomyTerm>)catalog.Jobs["artisan"].Terms).IsReadOnly, "Job terms не read-only.");
    Assert(((IDictionary<string, IReadOnlyList<PlanetEconomyCondition>>)catalog.Buildings["building_capital"].SourceFields).IsReadOnly
        && ((IList<PlanetEconomyCondition>)catalog.Buildings["building_capital"].SourceFields["cost"]).IsReadOnly,
        "Definition source metadata не immutable.");
    Assert(catalog.Sources is not IList<PlanetEconomySourceFile> sourceList || sourceList.IsReadOnly, "Sources изменяемы.");
}

static async Task MissingSource()
{
    using var fixture = await Fixture.Create();
    File.Delete(Path.Combine(fixture.Root, "common", "pop_jobs", "03_worker_jobs.txt"));
    await ThrowsAsync<PlanetEconomyImportException>(() => new PlanetEconomyImporter().ImportAsync(fixture.Root));
}

static async Task AmbiguousJob()
{
    using var fixture = await Fixture.Create();
    var path = Path.Combine(fixture.Root, "common", "pop_jobs", "01_ruler_jobs.txt");
    await File.AppendAllTextAsync(path, "\ntechnician = { category = worker resources = { category = test } }\n");
    await ThrowsAsync<PlanetEconomyImportException>(() => new PlanetEconomyImporter().ImportAsync(fixture.Root));
}

static async Task AmbiguousBuilding()
{
    using var fixture = await Fixture.Create();
    var path = Path.Combine(fixture.Root, "common", "buildings", "00_capital_buildings.txt");
    await File.AppendAllTextAsync(path, "\nbuilding_capital = { resources = { category = test } }\n");
    await ThrowsAsync<PlanetEconomyImportException>(() => new PlanetEconomyImporter().ImportAsync(fixture.Root));
}

static async Task MalformedSource()
{
    using var fixture = await Fixture.Create();
    await File.WriteAllTextAsync(Path.Combine(fixture.Root, "common", "pop_jobs", "03_worker_jobs.txt"), "technician = { resources = { produces = { energy = } }");
    await ThrowsAsync<PlanetEconomyImportException>(() => new PlanetEconomyImporter().ImportAsync(fixture.Root));
}

static async Task SourceMutation()
{
    using var fixture = await Fixture.Create();
    var jobSource = Path.Combine(fixture.Root, "common", "pop_jobs", "03_worker_jobs.txt");
    var importer = new PlanetEconomyImporter(async cancellation =>
    {
        cancellation.ThrowIfCancellationRequested();
        await File.AppendAllTextAsync(jobSource, "\n# changed after import read\n", cancellation);
    });
    await ThrowsAsync<PlanetEconomyImportException>(() => importer.ImportAsync(fixture.Root));
}

static async Task ReparseVariableDirectory()
{
    using var fixture = await Fixture.Create();
    var external = Path.Combine(Path.GetTempPath(), "Stellaris-PlanetEconomyChecks-Escape-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(external);
    try
    {
        await File.WriteAllTextAsync(Path.Combine(external, "escaped.txt"), "@fixture_escape = 7\n");
        var link = Path.Combine(fixture.Root, "common", "scripted_variables", "escape");
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("/c");
        process.StartInfo.ArgumentList.Add("mklink");
        process.StartInfo.ArgumentList.Add("/J");
        process.StartInfo.ArgumentList.Add(link);
        process.StartInfo.ArgumentList.Add(external);
        try
        {
            process.Start();
            await process.WaitForExitAsync();
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or System.InvalidOperationException)
        {
            throw new CheckSkippedException("Невозможно запустить mklink /J в этой среде: " + error.GetType().Name);
        }
        if (process.ExitCode != 0 || !Directory.Exists(link))
            throw new CheckSkippedException("Среда отказала в создании тестового junction: " + (await process.StandardError.ReadToEndAsync()).Trim());

        var capitalPath = Path.Combine(fixture.Root, "common", "buildings", "00_capital_buildings.txt");
        var capital = await File.ReadAllTextAsync(capitalPath);
        await File.WriteAllTextAsync(capitalPath, capital.Replace("job_technician_add = 100", "job_technician_add = @fixture_escape", StringComparison.Ordinal));
        // Источник из junction должен быть обнаружен и отвергнут до импорта любого внешнего variable.
        await ThrowsAsync<PlanetEconomyImportException>(() => new PlanetEconomyImporter().ImportAsync(fixture.Root));
    }
    finally
    {
        var link = Path.Combine(fixture.Root, "common", "scripted_variables", "escape");
        if (Directory.Exists(link) && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0) Directory.Delete(link);
        if (Directory.Exists(external)) Directory.Delete(external, recursive: true);
    }
}

static async Task RealSource(string gameRoot, string? coverageReportPath)
{
    var catalog = await new PlanetEconomyImporter().ImportAsync(gameRoot);
    var coverage = catalog.DefinitionCoverage;
    Assert(coverage.Count == 644 && coverage.Select(item => (item.Kind, item.Id)).Distinct().Count() == coverage.Count,
        "Definition coverage содержит пропуски или повторные source IDs.");
    Assert(coverage.Count == Enum.GetValues<PlanetEconomyDefinitionCoverageStatus>().Sum(status => coverage.Count(item => item.Status == status)),
        "Баланс discovered = included + excluded + unresolved нарушен.");
    Assert(coverage.Count(item => item.Status == PlanetEconomyDefinitionCoverageStatus.Included)
        == catalog.Buildings.Count + catalog.Zones.Count, "Included coverage не совпадает с каталогом.");
    Assert(coverage.All(item => item.Status != PlanetEconomyDefinitionCoverageStatus.Included ||
        (item.Kind == PlanetEconomyDefinitionKind.Building ? catalog.Buildings.ContainsKey(item.Id) : catalog.Zones.ContainsKey(item.Id))),
        "Included source ID отсутствует в каталоге.");
    Assert(coverage.Where(item => item.Status != PlanetEconomyDefinitionCoverageStatus.Included)
        .All(item => !catalog.Buildings.ContainsKey(item.Id) && !catalog.Zones.ContainsKey(item.Id)), "Special/unresolved ID попал в ordinary catalog.");
    var capacityTerms = catalog.Buildings.Values.SelectMany(item => item.Terms).Concat(catalog.Districts.Values.SelectMany(item => item.Terms))
        .Concat(catalog.Zones.Values.SelectMany(item => item.Terms))
        .Where(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacity).ToArray();
    var capacityTargets = capacityTerms.Select(term => term.TargetId).ToHashSet(StringComparer.Ordinal);
    Assert(catalog.JobClosure.Select(item => item.JobId).ToHashSet(StringComparer.Ordinal).SetEquals(capacityTargets),
        "Job closure не покрывает каждый уникальный source capacity target ровно один раз.");
    Assert(catalog.JobClosure.Select(item => item.JobId).Distinct(StringComparer.Ordinal).Count() == catalog.JobClosure.Count,
        "Job closure содержит дубликат job ID.");
    var activeCapacityTargets = capacityTerms.Where(term => term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext)
        .Select(term => term.TargetId).ToHashSet(StringComparer.Ordinal);
    foreach (var target in activeCapacityTargets)
    {
        var closure = catalog.JobClosure.Single(item => item.JobId == target);
        Assert(closure.Status != PlanetEconomyJobClosureStatus.Missing,
            $"Active ordinary capacity target {target} не найден в source pop_jobs.");
        Assert((closure.Provenance.RelativePath.StartsWith("common/pop_jobs/", StringComparison.Ordinal)
            || closure.Provenance.RelativePath.StartsWith("common/buildings/", StringComparison.Ordinal)
            || closure.Provenance.RelativePath.StartsWith("common/zones/", StringComparison.Ordinal))
            && closure.Provenance.Line > 0 && closure.Provenance.Sha256.Length == 64,
            $"Job closure target {target} не имеет source-backed provenance.");
        Assert(closure.Status != PlanetEconomyJobClosureStatus.Gated || !string.IsNullOrWhiteSpace(closure.Reason),
            $"Gated target {target} не сообщает source-backed gate.");
        Assert(closure.Status != PlanetEconomyJobClosureStatus.Covered || catalog.Jobs.ContainsKey(target),
            $"Covered target {target} отсутствует в imported Jobs.");
    }
    Assert(catalog.JobClosure.Count == 59
        && catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Covered) == 29
        && catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Gated) == 30
        && catalog.JobClosure.All(item => item.Status != PlanetEconomyJobClosureStatus.Missing),
        $"Source job closure counts changed without corresponding source review: {catalog.JobClosure.Count}/{catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Covered)}/{catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Gated)}/{catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Missing)}.");
    var closureFlowTerms = catalog.JobClosure.Where(item => catalog.Jobs.ContainsKey(item.JobId)).SelectMany(item => catalog.Jobs[item.JobId].Terms
        .Where(term => term.Kind is PlanetEconomyTermKind.ResourceProduction or PlanetEconomyTermKind.ResourceUpkeep)).ToArray();
    var closureResourceGroups = closureFlowTerms.GroupBy(term => term.TargetId, StringComparer.Ordinal).ToArray();
    var coveredClosureResources = closureResourceGroups.Count(group => group.Any(term => term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext));
    Assert(closureResourceGroups.Length == 15 && coveredClosureResources == 12
        && closureResourceGroups.All(group => catalog.Resources.ContainsKey(group.Key)),
        "Transitive job recipes contain a missing/unsupported stock resource or unexpected source inventory.");
    Console.WriteLine($"  Job closure: {catalog.JobClosure.Count} unique capacity targets; covered={catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Covered)}, gated={catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Gated)}, missing={catalog.JobClosure.Count(item => item.Status == PlanetEconomyJobClosureStatus.Missing)}.");
    Assert(coverage.Single(item => item.Id == "building_the_beholder").Status == PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific
        && coverage.Single(item => item.Id == "zone_cosmogenesis_default").Status == PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific
        && coverage.Single(item => item.Id == "symbiosis_nexus_3").Status == PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific
        && coverage.Single(item => item.Id == "building_factory_1").Status == PlanetEconomyDefinitionCoverageStatus.Included,
        "Source-specific event/scenario исключения или обычное здание классифицированы неверно.");
    var slaveProcessingCoverage = coverage.Single(item => item.Id == "building_slave_processing");
    Assert(slaveProcessingCoverage.Status == PlanetEconomyDefinitionCoverageStatus.Included
        && catalog.Buildings.ContainsKey("building_slave_processing"),
        "Обычный government building_slave_processing ошибочно исключён по имени.");
    var slaveProcessing = catalog.Buildings["building_slave_processing"];
    Assert(slaveProcessing.AvailabilityDisposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext
        && slaveProcessing.SourceFields["potential"].SelectMany(FlattenSourceField).Any(value => value == "slavery_allowed")
        && slaveProcessing.SourceFields["potential"].SelectMany(FlattenSourceFieldKey).Any(value => value == "has_exploited_disconnected_drones")
        && slaveProcessing.SourceFields["prerequisites"].SelectMany(FlattenSourceField).Any(value => value == "tech_neural_implants"),
        "Обычная eligibility/prerequisite processing facility потеряна или признана special.");
    Assert(slaveProcessing.Terms.Any(term => term.Kind == PlanetEconomyTermKind.PlanetModifier
        && term.TargetId == "pop_slave_bonus_workforce_mult" && term.Value == 0.10m),
        "Обычный pop_slave_bonus_workforce_mult modifier потерян.");
    foreach (var id in new[] { "building_slave_capital", "building_slave_major_capital" })
    {
        Assert(coverage.Single(item => item.Id == id) is { Status: PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific, Reason: "source_id_thrall_world_capital" }
            && !catalog.Buildings.ContainsKey(id), "Thrall-world capital ошибочно включён: " + id);
    }
    Console.WriteLine("  Coverage: " + string.Join("; ", coverage.GroupBy(item => (item.Kind, item.Status))
        .OrderBy(group => group.Key.Kind).ThenBy(group => group.Key.Status)
        .Select(group => $"{group.Key.Kind}/{group.Key.Status}={group.Count()}")) + ".");
    foreach (var id in new[] { "building_foundry_2", "building_foundry_3", "building_factory_2", "building_factory_3", "building_research_lab_2", "building_research_lab_3" })
    {
        Assert(catalog.Buildings.ContainsKey(id), "Потерян ordinary upgrade tier " + id + ": " + coverage.Single(item => item.Id == id).Reason + ".");
    }
    var factory = catalog.Buildings["building_factory_1"];
    Assert(factory.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ConstructionCost && term.TargetId == "minerals" && term.Value is > 0),
        "Обычная стоимость здания из nomadic inline switcher не импортирована.");
    Assert(factory.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ConstructionTime && term.Value is > 0), "Source build time не импортирован.");
    Assert(factory.SourceFields["prerequisites"].SelectMany(FlattenSourceField).Any(value => value == "tech_basic_industry")
        && factory.SourceFields["upgrades"].SelectMany(FlattenSourceField).Any(value => value == "building_factory_2")
        && catalog.Buildings["building_medical_1"].IconId == "building_medical_1",
        "Icon, prerequisite или upgrade source metadata потеряна.");
    var pendingExpressions = catalog.Jobs.Values.SelectMany(job => job.Terms).Concat(catalog.Buildings.Values.SelectMany(definition => definition.Terms))
        .Concat(catalog.Districts.Values.SelectMany(definition => definition.Terms)).Concat(catalog.Zones.Values.SelectMany(definition => definition.Terms))
        .Where(term => term.SourceExpression is not null).ToArray();
    var pendingOrdinaryFlows = pendingExpressions.Where(term => term.Kind is PlanetEconomyTermKind.ResourceProduction or PlanetEconomyTermKind.ResourceUpkeep
        && term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext).ToArray();
    Console.WriteLine($"  Source expressions retained: {pendingExpressions.Length}; unresolved ordinary flow expressions: {pendingOrdinaryFlows.Length} (not zero-valued).");
    Assert(pendingOrdinaryFlows.Length == 0, "Осталась обычная production/upkeep величина без concrete decimal source value.");
    var pendingOrdinaryCapacity = pendingExpressions.Where(term => term.Kind is PlanetEconomyTermKind.WorkforceCapacity or PlanetEconomyTermKind.WorkforceCapacityMultiplier
        && term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext).ToArray();
    Console.WriteLine($"  Unresolved ordinary capacity terms: {pendingOrdinaryCapacity.Length}; entries: {string.Join(", ", pendingOrdinaryCapacity.Select(term => $"{term.TargetId}@{term.Provenance.RelativePath}:{term.Provenance.Line}"))}.");
    Assert(catalog.GameVersion == "v4.5.2", "Импортирована другая версия.");
    var englishKeys = new HashSet<string>(StringComparer.Ordinal);
    foreach (var path in Directory.EnumerateFiles(Path.Combine(gameRoot, "localisation", "english"), "*.yml", SearchOption.AllDirectories).Where(path => !path.Contains("RINFIX", StringComparison.OrdinalIgnoreCase)))
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(await File.ReadAllTextAsync(path), @"(?m)^\s*([A-Za-z0-9_.-]+)\s*:")) englishKeys.Add(match.Groups[1].Value);
    foreach (var job in catalog.Jobs.Values)
    {
        Assert(job.NameKey is not null && englishKeys.Contains(job.NameKey), "Job localization binding не подтверждён: " + job.Id);
        Assert(job.DescriptionKey is null || englishKeys.Contains(job.DescriptionKey), "Job description binding не подтверждён: " + job.Id);
        Assert(job.PluralKey is null || englishKeys.Contains(job.PluralKey), "Job plural binding не подтверждён: " + job.Id);
        Assert(job.NameKey != job.IconAlias, "IconAlias ошибочно использован как display key: " + job.Id);
    }
    foreach (var definition in catalog.Buildings.Values.Concat(catalog.Zones.Values).Concat(catalog.Districts.Values))
    {
        Assert(definition.LocalizationKey is not null && englishKeys.Contains(definition.LocalizationKey), "Definition localization binding не подтверждён: " + definition.Id);
        Assert(definition.DescriptionKey is null || englishKeys.Contains(definition.DescriptionKey), "Definition description binding не подтверждён: " + definition.Id);
    }
    Assert(PlanetLocalizationBindings.Get("job", "unknown_job").NameKey is null, "Неизвестному job выдуман localization prefix.");
    Assert(Fixture.JobIds.All(catalog.Jobs.ContainsKey), "Потеряна одна из прежних 16 source jobs.");
    var addedJobIds = catalog.Jobs.Keys.Except(Fixture.JobIds, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
    Assert(catalog.Jobs.Count == 31 && addedJobIds.Count == 15
        && addedJobIds.IsSubsetOf(catalog.JobClosure.Select(item => item.JobId).ToHashSet(StringComparer.Ordinal)),
        "Job catalog не ограничен базовыми 16 и реально используемыми source capacity targets.");
    var colonist = catalog.Jobs["colonist"];
    Assert(colonist.JobStratum == "worker" && colonist.EconomicCategory == "planet_jobs" && colonist.IconId == "colonist"
        && new[] { "food", "minerals", "alloys", "energy" }.All(resource => colonist.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceProduction
            && term.TargetId == resource && term.Value == 1 && term.ConditionDisposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext)),
        "Colonist inline job category, icon, conditional outputs or transitive recipe closure не resolved.");
    var mentor = catalog.Jobs["polytechnic_mentor"];
    Assert(mentor.EconomicCategory == "planet_mentors"
        && mentor.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceUpkeep && term.TargetId == "consumer_goods" && term.Value == 3)
        && new[] { "unity", "physics_research", "society_research", "engineering_research" }.All(resource => mentor.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceProduction && term.TargetId == resource && term.Value > 0))
        && mentor.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceMultiplier && term.SourceExpression?.StartsWith("value:mentor_scale_value", StringComparison.Ordinal) == true),
        "New ordinary polytechnic_mentor recipe, source resources, or typed dynamic multiplier is incomplete.");
    Assert(mentor.Provenance.RelativePath == "common/pop_jobs/02_specialist_jobs.txt" && mentor.Provenance.Line > 0
        && (mentor.IconId is not null || mentor.BuildingIconId == "building_research_lab_1"),
        "New ordinary job category/icon/source provenance is incomplete.");
    var prospector = catalog.Jobs["offworld_prospector"];
    Assert(prospector.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceUpkeep && term.TargetId == "energy" && term.Value > 0)
        && new[] { "sr_living_metal", "volatile_motes" }.All(resource => prospector.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceProduction
            && term.TargetId == resource && term.Value > 0))
        && catalog.Resources["sr_living_metal"].StoragePolicy == PlanetEconomyStoragePolicy.SourceCapped
        && catalog.Resources["sr_living_metal"].SourceMaximum is > 0
        && prospector.BuildingIconId == "building_offworld_expedition_hub",
        "Transitive prospector recipe/resource cap/icon metadata is incomplete.");
    Assert(catalog.Buildings["building_automation_technician_1"].Terms.Any(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacityMultiplier
        && term.SourceKey == "job_technician_automated_workforce_mult")
        && !catalog.JobClosure.Any(item => item.JobId == "technician_automated_workforce"),
        "Job workforce multiplier was confused with a job capacity and pulled into recipe closure.");
    Assert(catalog.Jobs["foundry"].Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceProduction && term.TargetId == "alloys" && term.Value == 3), "Не найден source foundry output 3 alloys.");
    Assert(catalog.Jobs["foundry"].Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceUpkeep && term.TargetId == "minerals" && term.Value == 6), "Потерян source foundry upkeep 6 minerals.");
    var artisan = catalog.Jobs["artisan"].Terms;
    Assert(artisan.Any(term => term.Kind == PlanetEconomyTermKind.ResourceUpkeep && term.TargetId == "minerals" && term.Value == 6 && term.ConditionDisposition == PlanetEconomyConditionDisposition.ActiveInOrdinaryContext), "Не сохранён ordinary artisan upkeep.");
    Assert(catalog.Jobs["clerk"].LocalIconPath == "gfx/interface/icons/jobs/job_clerk.dds", "Missing clerk icon path metadata.");
    Assert(catalog.Jobs["merchant"].LocalIconPath == "gfx/interface/icons/jobs/job_merchant.dds", "Missing merchant icon path metadata.");
    foreach (var id in new[] { "clerk", "entertainer", "healthcare", "soldier" })
    {
        Assert(catalog.Jobs[id].LocalIconPath == $"gfx/interface/icons/jobs/job_{id}.dds", $"Missing {id} icon path metadata.");
    }
    Assert(catalog.Jobs["clerk"].IconAlias == "concept_clerks" && catalog.Jobs["soldier"].IconAlias == "concept_soldiers",
        "Source aliases clerk/soldier не сохранены.");
    Assert(catalog.Jobs["merchant"].AvailabilityDisposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext,
        "Source availability merchant=always no не сохранена.");
    var foundryOutput = catalog.Jobs["foundry"].Terms.Single(term => term.Kind == PlanetEconomyTermKind.ResourceProduction && term.TargetId == "alloys" && term.Value == 3);
    Assert(foundryOutput.Provenance.RelativePath == "common/inline_scripts/output/foundry_output.txt" && foundryOutput.Provenance.Line > 0,
        "У inline output foundry отсутствует точное line provenance.");
    var researcherCapacity = catalog.Zones["zone_research_unity"].Terms.Single(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacity
        && term.TargetId == "physicist" && term.Value == 20 && term.ConditionDisposition == PlanetEconomyConditionDisposition.ActiveInOrdinaryContext);
    Assert(researcherCapacity.SourceKey == "job_physicist_add" && researcherCapacity.Provenance.RelativePath.EndsWith("jobs/zone_researchers_add.txt", StringComparison.Ordinal),
        "Raw ordinary source researcher capacity потеряна либо нормализована.");
    Assert(researcherCapacity.ApplicationScope == PlanetEconomyApplicationScope.PerAttachedDistrictLevel,
        "District-triggered zone capacity lost its attached-district application scope.");
    var staticSlots = catalog.Zones["zone_research_unity"].Terms.Single(term => term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == "zone_building_slots_add");
    Assert(staticSlots.Value == 3 && staticSlots.ApplicationScope == PlanetEconomyApplicationScope.PerDefinitionInstance,
        "Static zone building slots were marked as per-district-level contributions.");
    Assert(catalog.Zones["zone_default"].Terms.Any(term => term.Kind == PlanetEconomyTermKind.ConstructionCostMultiplier)
        && !catalog.Zones["zone_default"].Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceMultiplier),
        "Множитель цены zone_default попал в recurring economy.");
    var visited = new HashSet<string>(StringComparer.Ordinal);
    var visiting = new HashSet<string>(StringComparer.Ordinal);
    foreach (var id in catalog.Buildings.Keys) VisitUpgrade(id);
    void VisitUpgrade(string id)
    {
        if (visited.Contains(id)) return;
        Assert(visiting.Add(id), "Source ordinary upgrade graph содержит цикл: " + id);
        foreach (var target in catalog.Buildings[id].SourceProperties.GetValueOrDefault("upgrades", []))
        {
            Assert(catalog.DefinitionCoverage.Any(item => item.Kind == PlanetEconomyDefinitionKind.Building && item.Id == target),
                "Upgrade target отсутствует и в каталоге, и в explicit coverage: " + target);
            if (catalog.Buildings.ContainsKey(target)) VisitUpgrade(target);
        }
        visiting.Remove(id); visited.Add(id);
    }
    if (coverageReportPath is not null) await WriteCoverageReport(catalog, coverageReportPath);
}

static IEnumerable<string?> FlattenSourceField(PlanetEconomyCondition condition)
{
    yield return condition.Value;
    foreach (var child in condition.Children)
        foreach (var value in FlattenSourceField(child)) yield return value;
}

static IEnumerable<string> FlattenSourceFieldKey(PlanetEconomyCondition condition)
{
    yield return condition.Key;
    foreach (var child in condition.Children)
        foreach (var value in FlattenSourceFieldKey(child)) yield return value;
}

static async Task WriteCoverageReport(PlanetEconomyCatalog catalog, string path)
{
    var byPath = catalog.Sources.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
    var counts = catalog.DefinitionCoverage.GroupBy(item => (item.Kind, item.Status)).ToDictionary(group => group.Key, group => group.Count());
    var lines = new List<string>
    {
        "# Покрытие ordinary каталога зданий и зон — Stellaris 4.5.2", "",
        $"Проверено по локальному source набору игры; definitions: **{catalog.DefinitionCoverage.Count}**, Included {catalog.DefinitionCoverage.Count(item => item.Status == PlanetEconomyDefinitionCoverageStatus.Included)}, ExcludedSpecific {catalog.DefinitionCoverage.Count(item => item.Status == PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific)}, Unresolved {catalog.DefinitionCoverage.Count(item => item.Status == PlanetEconomyDefinitionCoverageStatus.Unresolved)}. Дублей IDs нет. Для каждого definition указаны относительный путь, SHA-256 исходного файла и строка. Полные тела game scripts не сохраняются.", "",
        "Статусы: Included означает обычное органическое non-gestalt содержание; technology, upgrade, placement, zone, ordinary civic и ethics eligibility сохраняется в каталоге. ExcludedSpecific применяется к файлам/условиям event, fallen empire, branch office/holding, primitive/origin, hive/machine, mobile, wilderness, habitat, ringworld/arcology/nexus, arkship и другим unique scenario branches. Upgrade tiers, доступные только через исключённую цепочку, получают то же исключение транзитивно; улучшенные ordinary tiers остаются Included. Особые термы внутри обычного определения сохраняются как conditional terms и не подмешиваются в обычные активные потоки. Unresolved оставляется для неоднозначного source shape; unknown не считается false или нулём.", "",
        "## Сводка", "", "| Тип | Included | ExcludedSpecific | Unresolved |", "|---|---:|---:|---:|"
    };
    foreach (var kind in new[] { PlanetEconomyDefinitionKind.Building, PlanetEconomyDefinitionKind.Zone })
    {
        int Count(PlanetEconomyDefinitionCoverageStatus status) => counts.TryGetValue((kind, status), out var value) ? value : 0;
        lines.Add($"| {kind} | {Count(PlanetEconomyDefinitionCoverageStatus.Included)} | {Count(PlanetEconomyDefinitionCoverageStatus.ExcludedSpecific)} | {Count(PlanetEconomyDefinitionCoverageStatus.Unresolved)} |");
    }
    lines.AddRange(["", "## Полный discovered inventory", "", "| Тип | ID | Статус | Основание исключения | Source | Строка | SHA-256 |", "|---|---|---|---|---|---:|---|"]);
    foreach (var item in catalog.DefinitionCoverage)
    {
        var source = byPath[item.Provenance.RelativePath];
        lines.Add($"| {item.Kind} | `{item.Id}` | {item.Status} | {item.Reason ?? "—"} | `{item.Provenance.RelativePath}` | {item.Provenance.Line} | `{source.Sha256}` |");
    }
    var terms = catalog.Jobs.Values.SelectMany(item => item.Terms).Concat(catalog.Buildings.Values.SelectMany(item => item.Terms))
        .Concat(catalog.Districts.Values.SelectMany(item => item.Terms)).Concat(catalog.Zones.Values.SelectMany(item => item.Terms)).ToArray();
    var dynamicTerms = terms.Where(term => term.SourceExpression is not null).ToArray();
    var unresolvedFlows = dynamicTerms.Where(term => term.Kind is PlanetEconomyTermKind.ResourceProduction or PlanetEconomyTermKind.ResourceUpkeep
        or PlanetEconomyTermKind.BuildingUpkeep or PlanetEconomyTermKind.DistrictUpkeep or PlanetEconomyTermKind.ZoneUpkeep
        && term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext).ToArray();
    var unresolvedCapacity = dynamicTerms.Where(term => term.Kind is PlanetEconomyTermKind.WorkforceCapacity or PlanetEconomyTermKind.WorkforceCapacityMultiplier
        && term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext).ToArray();
    lines.AddRange(["", "## Сохранённые runtime expressions", "", $"В каталоге осталось {dynamicTerms.Length} source expressions; active ordinary production/upkeep без resolved decimal: {unresolvedFlows.Length}. Неизвестные expressions остаются типизированными и не сводятся к нулю."]);
    foreach (var term in unresolvedCapacity)
    {
        var source = byPath[term.Provenance.RelativePath];
        lines.Add($"- Обычная capacity `{term.TargetId}`: `{term.SourceExpression}` — `{term.Provenance.RelativePath}:{term.Provenance.Line}`, SHA-256 `{source.Sha256}`.");
    }
    var jobFlowTerms = catalog.JobClosure.Where(item => catalog.Jobs.ContainsKey(item.JobId)).SelectMany(item => catalog.Jobs[item.JobId].Terms
        .Where(term => term.Kind is PlanetEconomyTermKind.ResourceProduction or PlanetEconomyTermKind.ResourceUpkeep)).ToArray();
    var jobResources = jobFlowTerms.GroupBy(term => term.TargetId, StringComparer.Ordinal).Select(group => new
    {
        Id = group.Key,
        Status = group.Any(term => term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext)
            ? "Covered" : "Gated",
        First = group.First()
    }).ToArray();
    var coveredJobs = catalog.JobClosure.Where(item => item.Status == PlanetEconomyJobClosureStatus.Covered).ToArray();
    var gatedJobs = catalog.JobClosure.Where(item => item.Status == PlanetEconomyJobClosureStatus.Gated).ToArray();
    var missingJobs = catalog.JobClosure.Where(item => item.Status == PlanetEconomyJobClosureStatus.Missing).ToArray();
    lines.AddRange(["", "## Транзитивная closure workforce jobs", "",
        $"Capacity targets: {catalog.JobClosure.Count}; Covered {coveredJobs.Length}; Gated {gatedJobs.Length}; Missing {missingJobs.Length}. Imported source job recipes: {catalog.Jobs.Count}. Closure flow resources: {jobResources.Length}; Covered {jobResources.Count(item => item.Status == "Covered")}; Gated {jobResources.Count(item => item.Status == "Gated")}; Missing {jobResources.Count(item => !catalog.Resources.ContainsKey(item.Id))}.",
        "Covered означает, что source recipe импортирован; его обычные и требующие runtime availability условия остаются типизированы в job. Gated означает специальный source file, inline-only category или неактивную ordinary availability; gated source не активируется как ordinary job. Missing означает отсутствие definition для capacity target и не заменяется пустым/нулевым рецептом.", "",
        "| Job target | Статус | Source/gate | Строка | SHA-256 |", "|---|---|---|---:|---|"]);
    foreach (var closure in catalog.JobClosure)
    {
        var source = byPath[closure.Provenance.RelativePath];
        lines.Add($"| `{closure.JobId}` | {closure.Status} | {closure.Reason ?? "—"} `{closure.Provenance.RelativePath}` | {closure.Provenance.Line} | `{source.Sha256}` |");
    }
    lines.AddRange(["", "| Job flow resource | Статус | Source | Строка | SHA-256 |", "|---|---|---|---:|---|"]);
    foreach (var resource in jobResources)
    {
        var source = byPath[resource.First.Provenance.RelativePath];
        lines.Add($"| `{resource.Id}` | {(catalog.Resources.ContainsKey(resource.Id) ? resource.Status : "Missing") } | `{resource.First.Provenance.RelativePath}` | {resource.First.Provenance.Line} | `{source.Sha256}` |");
    }
    lines.AddRange(["", "## Прочитанные source/dependency файлы", "", "| Source | Bytes | SHA-256 |", "|---|---:|---|"]);
    lines.AddRange(catalog.Sources.Select(source => $"| `{source.RelativePath}` | {source.ByteLength} | `{source.Sha256}` |"));
    await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
}

static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception("Ожидалось исключение " + typeof(T).Name + ".");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

sealed class Fixture : IDisposable
{
    public static readonly string[] JobIds = ["technician", "miner", "farmer", "clerk", "physicist", "biologist", "engineer", "foundry", "artisan", "bureaucrat", "enforcer", "entertainer", "healthcare", "soldier", "politician", "merchant"];
    public string Root { get; }

    private Fixture(string root) => Root = root;

    public static async Task<Fixture> Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "Stellaris-PlanetEconomyChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "launcher-settings.json"), "{\"rawVersion\":\"v4.5.2\"}");
        await Write(root, "common/pop_jobs/01_ruler_jobs.txt", JobDefinitions(["politician", "merchant"]));
        await Write(root, "common/pop_jobs/02_specialist_jobs.txt", JobDefinitions(JobIds.Where(id => id is not "technician" and not "miner" and not "farmer" and not "clerk" and not "politician" and not "merchant")));
        await Write(root, "common/pop_jobs/03_worker_jobs.txt", JobDefinitions(["technician", "miner", "farmer", "clerk"]));
        await Write(root, "common/strategic_resources/00_strategic_resources.txt", ResourceDefinitions());
        await Write(root, "common/game_concepts/01_job_concepts.txt", "concept_clerk = { alias = { concept_clerks } icon = \"gfx/interface/icons/jobs/job_clerk.dds\" }\n");
        await Write(root, "common/scripted_variables/checks.txt", "@fixture_value = 3\n");
        foreach (var path in DefinitionFiles.Keys) await Write(root, path, DefinitionDefinitions(path));
        return new Fixture(root);
    }

    private static string JobDefinitions(IEnumerable<string> ids) => string.Join("\n", ids.Select(id => id == "artisan" ?
        "artisan = { category = specialist resources = { category = planet_artisans produces = { consumer_goods = 6 } produces = { trigger = { artisan_is_artificer_trigger = yes } trade = 2 mult = value:fixture_ethics_multiplier|ETHIC|materialist| } upkeep = { trigger = { industrial_jobs_are_catalytic_trigger = no } minerals = 6 } upkeep = { trigger = { industrial_jobs_are_catalytic_trigger = yes } food = 9 } } }" :
        id == "merchant" ? "merchant = { category = ruler possible = { always = no } }" :
        id == "technician" ? "technician = { category = worker job_tags = { fixture_worker } resources = { category = planet_jobs produces = { trigger = { fixture_unknown_condition = yes } energy = 2 } produces = { physics_research = 3 society_research = 3 engineering_research = 3 unity = 1 } } }" :
        $"{id} = {{ category = {Category(id)} {(id == "technician" ? "job_tags = { fixture_worker }" : "")} resources = {{ category = planet_jobs produces = {{ physics_research = 3 society_research = 3 engineering_research = 3 unity = 1 }} }} }}"));

    private static string Category(string id) => id switch { "technician" or "miner" or "farmer" or "clerk" => "worker", "politician" or "merchant" => "ruler", _ => "specialist" };

    private static string ResourceDefinitions()
    {
        var capped = new[] { "energy", "minerals", "food", "consumer_goods", "alloys", "trade" };
        var unlimited = new[] { "physics_research", "society_research", "engineering_research", "unity" };
        return string.Join("\n", capped.Select(id => $"{id} = {{ category = planet_resources max = {(id == "trade" ? 50000 : 1000)} }}")
            .Concat(unlimited.Select(id => $"{id} = {{ category = planet_resources }}"))
            // Намеренно fixture cap, не native число: importer обязан читать source.
            .Concat(new[] { "volatile_motes", "exotic_gases", "rare_crystals", "sr_zro", "sr_dark_matter", "sr_living_metal" }.Select(id => $"{id} = {{ category = strategic max = 3210 }}"))
            .Append("nanites = { category = rare }"));
    }

    private static readonly IReadOnlyDictionary<string, string[]> DefinitionFiles = new Dictionary<string, string[]>
    {
        ["common/districts/02_rural_districts.txt"] = ["district_generator", "district_mining", "district_farming"],
        ["common/districts/00_urban_districts.txt"] = ["district_city"],
        ["common/zones/00_zones.txt"] = ["zone_research_unity", "zone_industrial", "zone_foundry"],
        ["common/buildings/00_capital_buildings.txt"] = ["building_capital"],
        ["common/buildings/04_manufacturing_buildings.txt"] = ["building_factory_1", "building_foundry_1"],
        ["common/buildings/02_government_buildings.txt"] = ["building_precinct_house"],
        ["common/buildings/05_research_buildings.txt"] = ["building_research_lab_1"],
        ["common/buildings/06_trade_buildings.txt"] = ["building_commercial_zone"],
        ["common/buildings/07_amenity_buildings.txt"] = ["building_luxury_residence", "building_holo_theatres", "building_communal_housing_large", "building_medical_1"],
        ["common/buildings/08_unity_buildings.txt"] = ["building_temple"],
        ["common/buildings/09_army_buildings.txt"] = ["building_stronghold"]
    };

    private static string DefinitionDefinitions(string path) => string.Join("\n", DefinitionFiles[path].Select(id =>
        $"{id} = {{ resources = {{ category = test upkeep = {{ minerals = 2 }} }} job_technician_add = 100 planet_modifier = {{ planet_amenities_add = 3 }} building_sets = {{ basic }} zone_sets = {{ ordinary }} included_building_sets = {{ research unity }} }}"));

    private static async Task Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text);
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(Root);
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (Path.GetDirectoryName(full) != parent || !Path.GetFileName(full).StartsWith("Stellaris-PlanetEconomyChecks-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe fixture cleanup was refused.");
        AssertNoReparse(full);
        Directory.Delete(full, recursive: true);
    }

    private static void AssertNoReparse(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Fixture cleanup stopped at a reparse point.");
        if ((File.GetAttributes(path) & FileAttributes.Directory) == 0) return;
        foreach (var child in Directory.EnumerateFileSystemEntries(path)) AssertNoReparse(child);
    }
}

sealed class CheckSkippedException(string message) : Exception(message);
