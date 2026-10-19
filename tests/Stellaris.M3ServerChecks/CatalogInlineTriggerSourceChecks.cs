using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Проверяет раскрытие native inline TRIGGER по действительным источникам 4.5.2.
internal static class CatalogInlineTriggerSourceChecks
{
    private const string InlinePath = "common/inline_scripts/buildings/nomadic_cost_switcher_with_additional_trigger.txt";
    private const string InlineName = "buildings/nomadic_cost_switcher_with_additional_trigger";
    private const string RingTrigger = "has_ringworld_output_boost";
    private const string OwnerTrigger = "is_lithoid_or_infernal_empire";

    private sealed record Caller(string DefinitionId, string Path, int CallLine, int CallColumn,
        int ArgumentLine, int ArgumentColumn, string Argument, string TriggerKind);

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);

        var source = new ContentSession(root);
        source.LoadFolder("common/buildings");
        var callers = new List<Caller>();
        foreach (var file in source.Sources.Where(item => item.Path.StartsWith("common/buildings/", StringComparison.Ordinal)))
        {
            foreach (var definition in file.Root.Entries.Where(item => item.Key.StartsWith("building_", StringComparison.Ordinal)
                         && item.Operator == "=" && item.Block is not null))
            {
                foreach (var call in Descendants(definition.Block!).Where(entry => entry.Key == "inline_script" && entry.Block is not null))
                {
                    var args = call.Block!.Entries;
                    var script = args.SingleOrDefault(entry => entry.Key == "script");
                    if (script?.Scalar != InlineName) { continue; }
                    var trigger = args.Where(entry => entry.Key == "TRIGGER").ToArray();
                    check(trigger.Length == 1 && trigger[0].Operator == "=" && trigger[0].Scalar is not null,
                        $"{definition.Key}: у inline-вызова ровно один скалярный аргумент TRIGGER");
                    var argument = trigger[0];
                    var kind = ParseTriggerKind(argument.Scalar!);
                    check(kind is not null, $"{definition.Key}: TRIGGER соответствует одной из двух исходных форм");
                    callers.Add(new(definition.Key, file.Path, call.Line, call.Column, argument.Line, argument.Column,
                        argument.Scalar!, kind!));
                }
            }
        }

        check(callers.Count == 20, "в исходниках найдено ровно двадцать вызовов целевого inline script");
        check(callers.Count(item => item.TriggerKind == RingTrigger) == 18
              && callers.Count(item => item.TriggerKind == OwnerTrigger) == 2,
            "исходники сохраняют 18 ringworld и 2 owner predicate аргумента");
        check(callers.Where(item => item.TriggerKind == RingTrigger).Select(item => item.Argument)
                .Order(StringComparer.Ordinal).SequenceEqual(Enumerable.Repeat("has_ringworld_output_boost = no", 9)
                    .Concat(Enumerable.Repeat("has_ringworld_output_boost = yes", 9)).Order(StringComparer.Ordinal), StringComparer.Ordinal)
              && callers.Where(item => item.TriggerKind == OwnerTrigger).Select(item => item.Argument)
                .Order(StringComparer.Ordinal).SequenceEqual(new[] { "owner? = { is_lithoid_or_infernal_empire = no }", "owner? = { is_lithoid_or_infernal_empire = yes }" }.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "исходные аргументы TRIGGER содержат обе формы yes/no в точном количестве");

        var sourceFiles = economy.Sources.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var inlineAst = source.LoadFile(InlinePath).Root;
        var placeholders = Descendants(inlineAst).Where(entry => entry.Key == "$TRIGGER$").ToArray();
        check(placeholders.Length == 2 && placeholders.All(placeholder => placeholder.Operator == "" && placeholder.Scalar == "$TRIGGER$"
              && placeholder.Source == InlinePath && placeholder.Line > 0 && placeholder.Column > 0),
            "оба исходных bare placeholder сохраняют путь и координаты в inline script");
        var sourceManifest = source.Manifest("catalog-inline-trigger-source-check", Array.Empty<string>());
        var parsedHashes = sourceManifest.Files.ToDictionary(file => file.Path, file => file.Sha256, StringComparer.Ordinal);
        check(callers.All(caller => parsedHashes.TryGetValue(caller.Path, out var hash) && IsSha256(hash)),
            "фактически разобранный manifest содержит SHA-256 всех двадцати caller-исходников");
        check(parsedHashes.TryGetValue(InlinePath, out var parsedInlineHash) && IsSha256(parsedInlineHash)
              && sourceFiles.TryGetValue(InlinePath, out var inlineFile) && inlineFile.ByteLength > 0
              && SameHash(parsedInlineHash, inlineFile.Sha256),
            "source manifest содержит длину и SHA-256 inline script");

        var included = callers.Where(caller => economy.Buildings.ContainsKey(caller.DefinitionId)).ToArray();
        var excluded = callers.Where(caller => !economy.Buildings.ContainsKey(caller.DefinitionId)).ToArray();
        check(included.Length + excluded.Length == callers.Count,
            "каждый реальный вызов классифицирован как включённое обычное здание или исключённый контекст");
        check(included.All(caller => sourceFiles.TryGetValue(caller.Path, out var file)
                  && parsedHashes.TryGetValue(caller.Path, out var hash) && SameHash(hash, file.Sha256)),
            "SHA-256 economy manifest для включённых callers совпадает с фактически разобранными исходниками");
        foreach (var caller in excluded)
        {
            var coverage = economy.DefinitionCoverage.SingleOrDefault(item => item.Id == caller.DefinitionId);
            check(coverage is not null && coverage.Status != PlanetEconomyDefinitionCoverageStatus.Included,
                $"исключённый вызов сохраняет source coverage: {caller.DefinitionId}");
        }

        var contexts = new Dictionary<string, ConstructionContext>(StringComparer.Ordinal);
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(templateId, 123);
            var startup = M3StartupColony.Create("catalog-inline-trigger-" + templateId, 1, home.Homeworld, home.Plan);
            var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var context = content.Context(startup, template, runtime,
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
            contexts.Add(templateId, context);
            check(context.SourceFacts is not null && context.Technologies is not null,
                $"контекст {templateId} построен из реального M3 startup-шаблона");
        }

        var matchedIncluded = 0;
        foreach (var caller in included)
        {
            var definition = content.Content.Catalogs.TryGetValue(caller.DefinitionId, out var catalog)
                ? catalog.Definitions[caller.DefinitionId] : null;
            check(definition is not null, $"для включённого вызова импортировано construction definition: {caller.DefinitionId}");
            var argumentOrigin = new ConstructionOrigin(caller.Path, caller.ArgumentLine, caller.ArgumentColumn);
            var callOrigin = new ConstructionOrigin(caller.Path, caller.CallLine, caller.CallColumn);
            var costTriggers = Flatten(definition!.Fields).Where(field => field.Key == "trigger" && field.Operator == "=" && field.Scalar is null)
                .Where(field => Flatten(field.Children).Any(leaf => leaf.Children.Count == 0 && SameOrigin(leaf.Origin, argumentOrigin)))
                .ToArray();
            check(costTriggers.Length == 2, $"{caller.DefinitionId}: импорт сохранил обе cost branches для TRIGGER в строке {caller.ArgumentLine}");
            var argumentAst = ParadoxParser.Parse(caller.Argument, caller.Path);
            var expectedLeaves = Descendants(argumentAst).Where(entry => entry.Block is null)
                .Select(entry => (entry.Key, entry.Operator, entry.Scalar)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
            var argumentBranches = costTriggers.Select(trigger => Flatten(trigger.Children)
                .Where(field => field.Children.Count == 0 && SameOrigin(field.Origin, argumentOrigin)).ToArray()).ToArray();
            check(argumentBranches.All(branch => branch.Length == expectedLeaves.Length
                  && branch.Select(field => (field.Key, field.Operator, field.Scalar)).OrderBy(item => item.Key, StringComparer.Ordinal).SequenceEqual(expectedLeaves)),
                $"{caller.DefinitionId}: construction AST совпадает с AST фактического аргумента TRIGGER");
            check(argumentBranches.All(branch => branch.Length > 0
                  && branch.All(field => field.Operator == "=" && field.Scalar is not null && field.Key != "$TRIGGER$")),
                $"{caller.DefinitionId}: раскрытые leaves являются условием с оператором и значением");
            check(argumentBranches.All(branch => branch.All(field => field.InlineCallSites.Any(origin => SameOrigin(origin, callOrigin))
                  && placeholders.Any(placeholder => field.InlineCallSites.Any(origin => origin.Source == InlinePath
                      && origin.Line == placeholder.Line && origin.Column == placeholder.Column)))),
                $"{caller.DefinitionId}: раскрытые листья сохраняют caller и placeholder provenance");
            check(catalog!.Files.Any(file => file.RelativePath == caller.Path && SameHash(file.Sha256, sourceFiles[caller.Path].Sha256))
                  && catalog.Files.Any(file => file.RelativePath == InlinePath && SameHash(file.Sha256, parsedHashes[InlinePath])),
                $"{caller.DefinitionId}: construction manifest содержит caller и inline source с совпадающими SHA-256");
            if (caller.TriggerKind == OwnerTrigger)
            {
                check(argumentAst.Entries is [var nativeOwner] && nativeOwner.Key == "owner?" && nativeOwner.Block is { Entries: [var nativePredicate] }
                      && nativePredicate.Key == OwnerTrigger
                      && costTriggers.All(trigger => Flatten(trigger.Children).Any(field => field.Key == "owner?"
                          && field.Operator == "=" && field.Children.Count == 1 && field.Children[0].Key == OwnerTrigger
                          && field.Children[0].Operator == "=" && field.Children[0].Scalar is "yes" or "no")),
                    $"{caller.DefinitionId}: структура owner? сохранена после раскрытия");
            }

            var terms = economy.Buildings[caller.DefinitionId].Terms.Where(term => term.Kind == PlanetEconomyTermKind.ConstructionCost
                && term.Condition is not null && Flatten(term.Condition).Any(node => SameOrigin(node.Provenance, caller.Path, caller.ArgumentLine, caller.ArgumentColumn)))
                .ToArray();
            check(terms.Length > 0, $"{caller.DefinitionId}: economy terms сохраняют provenance аргумента");
            check(terms.SelectMany(term => Flatten(term.Condition!)).Where(node => SameOrigin(node.Provenance, caller.Path, caller.ArgumentLine, caller.ArgumentColumn))
                    .All(node => node.Provenance.Sha256 == sourceFiles[caller.Path].Sha256),
                $"{caller.DefinitionId}: economy provenance сохраняет manifest SHA-256");
            foreach (var term in terms)
            {
                var fragment = FindArgumentFragment(term.Condition!, caller);
                check(fragment is not null, $"{caller.DefinitionId}: economy AST содержит отдельное дерево TRIGGER");
                var actualLeaves = Flatten(fragment!).Where(node => node.Children.Count == 0)
                    .Select(node => (node.Key, node.Operator, node.Value)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
                check(actualLeaves.SequenceEqual(expectedLeaves),
                    $"{caller.DefinitionId}: construction и economy листья совпадают с исходным аргументом строки {caller.ArgumentLine}");
                check(actualLeaves.All(leaf => leaf.Key != "$TRIGGER$" && leaf.Operator.Length != 0 && leaf.Value is not null),
                    $"{caller.DefinitionId}: economy условие не содержит placeholder, whole-string key или пустой оператор");
                if (caller.TriggerKind == OwnerTrigger)
                {
                    check(fragment!.Key == "owner?" && fragment.Operator == "=" && fragment.Children.Count == 1
                          && fragment.Children[0].Key == OwnerTrigger && fragment.Children[0].Operator == "="
                          && fragment.Children[0].Value is "yes" or "no",
                        $"{caller.DefinitionId}: economy AST сохраняет owner? wrapper и predicate");
                }
                foreach (var context in contexts.Values)
                {
                    if (caller.TriggerKind == RingTrigger)
                    {
                        var evaluated = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(fragment!, context, content.Content.Conditions);
                        check(evaluated.Truth == ConstructionTruth.Unknown,
                            $"ringworld условие остаётся Unknown в реальном startup-контексте {caller.DefinitionId}");
                    }
                    check(content.Content.Quote(caller.DefinitionId, context, 0m, 0m).Quote is null,
                        $"исходная quote остаётся закрытой для {caller.DefinitionId}");
                }
            }
            matchedIncluded++;
        }

        check(matchedIncluded == included.Length, "сравнение AST выполнено для всех включённых реальных вызовов");
        check(parsedHashes.ContainsKey(InlinePath) && included.All(caller => sourceFiles.ContainsKey(caller.Path)),
            "source manifest включает inline script, а importer manifests — каждый сравниваемый исходник");
        source.VerifyStable();
        check(true, "VerifyStable подтвердил неизменность snapshot исходников после проверок");
    }

    private static string? ParseTriggerKind(string text)
    {
        var parsed = ParadoxParser.Parse(text, "inline-trigger-argument");
        if (parsed.Entries is [var leaf] && leaf.Operator == "=" && leaf.Block is null && leaf.Scalar is "yes" or "no"
            && leaf.Key == RingTrigger) { return RingTrigger; }
        if (parsed.Entries is [var owner] && owner.Key == "owner?" && owner.Operator == "=" && owner.Block is { Entries: [var predicate] }
            && predicate.Key == OwnerTrigger && predicate.Operator == "=" && predicate.Block is null && predicate.Scalar is "yes" or "no")
        { return OwnerTrigger; }
        return null;
    }

    private static IEnumerable<ScriptEntry> Descendants(ScriptBlock block)
    {
        foreach (var entry in block.Entries)
        {
            yield return entry;
            if (entry.Block is { } children)
                foreach (var child in Descendants(children)) { yield return child; }
        }
    }

    private static IEnumerable<ConstructionField> Flatten(IEnumerable<ConstructionField> fields)
        => fields.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));

    private static IEnumerable<PlanetEconomyCondition> Flatten(PlanetEconomyCondition condition)
        => new[] { condition }.Concat(condition.Children.SelectMany(Flatten));

    private static PlanetEconomyCondition? FindArgumentFragment(PlanetEconomyCondition condition, Caller caller)
        => Flatten(condition).FirstOrDefault(node => SameOrigin(node.Provenance, caller.Path, caller.ArgumentLine, caller.ArgumentColumn)
            && (caller.TriggerKind == RingTrigger && node.Key == RingTrigger
                || caller.TriggerKind == OwnerTrigger && node.Key == "owner?"));

    private static bool SameOrigin(ConstructionOrigin left, ConstructionOrigin right)
        => left.Source == right.Source && left.Line == right.Line && left.Column == right.Column;

    private static bool SameOrigin(PlanetEconomyProvenance left, string path, int line, int column)
        => left.RelativePath == path && left.Line == line && left.Column == column;

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    private static bool SameHash(string left, string right)
        => Convert.FromHexString(left).AsSpan().SequenceEqual(Convert.FromHexString(right));
}
