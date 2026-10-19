using Stellaris.Core.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

var root = args.SingleOrDefault() ?? Environment.GetEnvironmentVariable("STELLARIS_GAME_ROOT") ?? throw new ArgumentException("Передайте read-only game root.");
var economy = await new PlanetEconomyImporter().ImportAsync(root);
var content = await M3ConstructionContent.ImportAsync(root, economy);
foreach (var item in content.RuntimeGateEvidence.OrderBy(item => item.Key, StringComparer.Ordinal))
{ Console.WriteLine($"Runtime dependency evidence {item.Key}: {item.Value}"); }
var startupCatalog = await M3StartupCatalog.ImportAsync(root);
CityZoneGraphChecks.Run(root, economy, content, startupCatalog);
ZoneChecks.Run(root, economy, content, startupCatalog);
EnabledChecks.Run(root, economy, content, startupCatalog);
DemolitionChecks.Run(root, economy, content, startupCatalog);
ConditionRefreshChecks.Run(root, economy, content, startupCatalog);
ConditionRefreshCoreChecks.Run();
ZoneReplacementCoreChecks.Run();
BuildingReplacementCoreChecks.Run();
BuildingReplacementProjectionChecks.Run(root, economy, content, startupCatalog);
AbsentPayerCoreChecks.Run();
CalculationFailureChecks.Run();
QueueEstimateChecks.Run(root, economy, content, startupCatalog);
ZoneReplacementChecks.Run(root, economy, content, startupCatalog);
var selected = startupCatalog.Select("economic", 123);
var startup = M3StartupColony.Create("colony-1", 1, selected.Homeworld, selected.Plan);
// Baseline contributions принадлежат economy owner. В fakebank harness они явно синтетические;
// source quote и source slot graph проверяются отдельно, неизвестный capital multiplier не заменяется молча.
var baselineGate = false;
try { content.Create(startup); } catch (ArgumentException error) when (error.Message.Contains("capacity multiplier")) { baselineGate = true; }
Check(baselineGate, "unresolved baseline capital does not silently fall back");
foreach (var term in economy.Buildings["building_capital"].Terms.Where(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacityMultiplier && term.ConditionDisposition != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext && ConstructionLiveConditions.Evaluate(term.Condition, M3ConstructionContent.EconomyFacts(startup)) != false))
{ Console.WriteLine($"baseline economy seam: {term.TargetId} multiplier={term.Value} expression={term.SourceExpression} at {term.Provenance.RelativePath}:{term.Provenance.Line}"); }
var state = content.Create(startup, FakeBaseline(startup));
var presetAdapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get("economic"));
var boundFrom = M3ConstructionContent.FromBinding.ActingCountry(false);
// Явный trusted no-modifier fixture: не скрытая замена modifiers действующего preset.
var adapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get("economic"), new("test-explicit-no-modifiers", 0, 0, 0), boundFrom);
var storage = economy.Resources.Where(pair => pair.Value.StoragePolicy != PlanetEconomyStoragePolicy.Unspecified).ToDictionary(pair => pair.Key, pair => new ConstructionStorage(pair.Value.StoragePolicy == PlanetEconomyStoragePolicy.UnlimitedReserve, pair.Value.SourceMaximum));
var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = economy.Resources.Keys.ToDictionary(id => id, _ => 1000m), [2] = economy.Resources.Keys.ToDictionary(id => id, _ => 0m) }, storage);
var actor = new M3ConstructionActor(1, true, true, true);
var view = adapter.Project(state, 1);
Check(adapter.Project(state, 1, activeCapacity: 0).QueuePolicy is null, "Нестандартная capacity не объявляет ordinary queue policy");
foreach (var item in view.Catalog) { Console.WriteLine($"catalog {item.DefinitionId}: {item.Eligibility} {item.Quote?.DurationDays} {string.Join(',', item.Gates.Select(gate => gate.DependencyId))}"); }
Console.WriteLine($"slots={state.Slots.Count} installed={state.Installed.Count} zones={startup.Zones.Count} district={startup.Districts.Sum(item => item.Level)}");
Check(state.Slots.Keys.Any(slot => presetAdapter.Quote(state, "building_stronghold", slot).Code == "source-numerical-unresolved"), "missing optional-from binding gates dependent price");
var trueFrom = M3ConstructionContent.FromBinding.ActingCountry(true);
var trueAdapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get("economic"), fromBinding: trueFrom);
var trueQuote = state.Slots.Keys.Select(slot => trueAdapter.Quote(state, "building_stronghold", slot)).First(result => result.Quote is not null).Quote!;
Check(trueQuote.Costs.ContainsKey("volatile_motes") && !trueQuote.Costs.ContainsKey("minerals"), "separately bound true from selects sourced branch");
var falseAdapter = new M3ConstructionAdapter(content, startup, ConstructionOrdinaryTemplate.Get("economic"), fromBinding: boundFrom);
Check(state.Slots.Keys.Any(slot => falseAdapter.Quote(state, "building_stronghold", slot).Quote?.Costs.ContainsKey("minerals") == true), "separately bound false from selects ordinary branch");
foreach (var templateId in new[] { "economic", "scientific", "military" })
{
    var home = startupCatalog.Select(templateId, 123);
    var colony = M3StartupColony.Create("actual-" + templateId, 1, home.Homeworld, home.Plan);
    var actual = content.Create(colony, FakeBaseline(colony));
    var actualAdapter = new M3ConstructionAdapter(content, colony, ConstructionOrdinaryTemplate.Get(templateId), fromBinding: boundFrom);
    var actualView = actualAdapter.Project(actual, 1);
    Check(actualView.DistrictTypePools?.Count == 3, "Три source rural pool не дошли до DTO");
    var capacityContext = content.Context(colony, ConstructionOrdinaryTemplate.Get(templateId), actual, boundFrom);
    var capacityFacts = M3ConstructionContent.EconomyFacts(colony);
    var unknownCarrier = new ConstructionContext(capacityContext.OwnerExists, capacityContext.OwnerFacts, capacityContext.FromNomadic,
        capacityContext.PlanetClass, capacityContext.Modifiers, null, capacityContext.Technologies, capacityContext.BuildingCounts, capacityContext.SourceFacts);
    Check(M3DistrictCapacity.Evaluate(content, colony, actual, "district_generator", unknownCarrier, capacityFacts).Gate == "district-uncapped-context-unresolved",
        "Отсутствующий carrier context превратился в false");
    var volcanic = new ConstructionContext(capacityContext.OwnerExists, capacityContext.OwnerFacts, capacityContext.FromNomadic,
        "pc_volcanic", capacityContext.Modifiers, null, capacityContext.Technologies, capacityContext.BuildingCounts, capacityContext.SourceFacts);
    Check(M3DistrictCapacity.Evaluate(content, colony, actual, "district_generator", volcanic, capacityFacts) is { Uncapped: true, Capacity: null, Gate: null },
        "Source volcanic OR не разрешил uncapped при unknown других веток");
    var capBuilding = economy.Buildings["building_generator_districts_1"];
    var capAddition = capBuilding.Terms.Single(term => term.TargetId == "district_generator_max_add").Value!.Value;
    var emptyBuildingSlot = actual.Slots.Values.First(slot => slot.Kind == ConstructionKind.Building && !actual.Installed.ContainsKey(slot.Id));
    // Только trusted installed-graph fixture: эта проверка не сертифицирует allow/can_build здания.
    var withCapacityBuilding = new ConstructionLiveRuntime(actual.ColonyId, actual.OwnerId, actual.Slots.Values,
        actual.Installed.Values.Append(new ConstructionInstalled(emptyBuildingSlot.Id, capBuilding.Id, ConstructionKind.Building,
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>())));
    Check(M3DistrictCapacity.Evaluate(content, colony, withCapacityBuilding, "district_generator", capacityContext, capacityFacts).Capacity
        == actualView.DistrictTypePools!.Single(pool => pool.DefinitionId == "district_generator").Capacity + capAddition,
        "Завершённый источник cap не пересчитан по installed graph");
    foreach (var type in new[] { "district_generator", "district_mining", "district_farming" })
    {
        var pool = actualView.DistrictTypePools!.Single(item => item.DefinitionId == type);
        Check(pool is { Uncapped: false, Capacity: not null } && pool.SharedPoolId == type
            && pool.Capacity == colony.Plan.DistrictCaps.Single(item => item.ModifierId == type + "_max_add").Value
            && pool.MemberDefinitionIds.SequenceEqual([type]), "Rural shared binding/cap расходится с источником");
        var rural = actual;
        // Явный funded unit bank, source recipe/limits остаются реальными.
        var ruralBank = new ConstructionBankSnapshot(bank.Accounts.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyDictionary<string, decimal>)pair.Value.ToDictionary(value => value.Key, _ => 10000m)), bank.Storage);
        var count = Math.Min(pool.Free!.Value, actualView.DistrictPool!.Free);
        Check(count > 0, "Source fixture не оставил места для rural reservation");
        for (var index = 0; index < count; index++)
        {
            var offered = actualAdapter.Project(rural, 1, bank: ruralBank).Catalog.Single(item => item.DefinitionId == type);
            Check(offered.Quote is not null && offered.SlotIds.Count > 0 && offered.CanAfford == true, "Rural source quote недоступен: " + type);
            var acceptedRural = actualAdapter.Execute(rural, ruralBank, actor, new("rural-build-" + index, "enqueueConstruction", ColonyId: rural.ColonyId,
                ExpectedColonyRevision: rural.Revision, DefinitionId: type, SlotId: offered.SlotIds[0], OrderId: "rural-order-" + index));
            Check(acceptedRural.Accepted, "Rural enqueue: " + acceptedRural.Code);
            rural = acceptedRural.State; ruralBank = Apply(ruralBank, acceptedRural.ResourceDeltas);
        }
        var filled = actualAdapter.Project(rural, 1, bank: ruralBank);
        Check(rural.Installed.Count == actual.Installed.Count && rural.Orders.Values.All(order => order.CompletedWork == 0)
            && filled.DistrictTypePools!.Single(item => item.DefinitionId == type).Reserved == count
            && filled.Catalog.Single(item => item.DefinitionId == type).SlotIds.Count == 0, "Pending rural превысил cap или дал ранние effects");
        var head = rural.PendingOrderIds[0]; var previousBank = ruralBank;
        var preview = filled.Queue.Single(item => item.OrderId == head).RefundQuote!;
        var cancelledRural = actualAdapter.Execute(rural, ruralBank, actor, new("rural-cancel", "cancelConstruction", ColonyId: rural.ColonyId,
            ExpectedColonyRevision: rural.Revision, OrderId: head, RefundPreviewId: preview.PreviewId, ConfirmRefundLoss: false));
        Check(cancelledRural.Accepted, "Rural cancel failed"); rural = cancelledRural.State; ruralBank = Apply(ruralBank, cancelledRural.ResourceDeltas);
        var released = actualAdapter.Project(rural, 1, bank: ruralBank).Catalog.Single(item => item.DefinitionId == type);
        Check(released.SlotIds.Count > 0 && ruralBank.Accounts[1]["minerals"] > previousBank.Accounts[1]["minerals"], "Cancel не освободил rural reservation/refund");
        var reused = actualAdapter.Execute(rural, ruralBank, actor, new("rural-reuse", "enqueueConstruction", ColonyId: rural.ColonyId,
            ExpectedColonyRevision: rural.Revision, DefinitionId: type, SlotId: released.SlotIds[0], OrderId: "rural-reused"));
        Check(reused.Accepted, "Rural slot повторно не используется"); rural = reused.State; ruralBank = Apply(ruralBank, reused.ResourceDeltas);
        var target = rural.PendingOrderIds[0]; var work = rural.Orders[target].Quote.RequiredWork;
        for (var day = 0; day < work; day++)
        {
            var advancedRural = actualAdapter.AdvanceDay(rural, ruralBank, "rural-day-" + day);
            Check(advancedRural.Accepted, "Rural completion: " + advancedRural.Code); rural = advancedRural.State;
        }
        var finishedOrder = rural.Orders[target];
        Check(rural.PendingOrderIds.Count == count - 1 && rural.Orders.Values.Count(order => order.Status == ConstructionOrderStatus.Cancelled) == 1,
            "Completion головы ошибочно отменил ожидающие заказы того же type pool");
        Check(finishedOrder.Status == ConstructionOrderStatus.Completed && rural.Installed.Count == actual.Installed.Count + 1
            && rural.Installed[finishedOrder.SlotId].InfrastructureUpkeep.Values.Sum() > 0
            && rural.Installed[finishedOrder.SlotId].JobCapacity.Values.Sum() > 0, "Rural completion не установил contributions");
    }
    Check(actualView.Features.Count == colony.Features.Count(feature => !feature.IsHidden) && actualView.Features.All(feature => !colony.Features.Single(item => item.InstanceId == feature.InstanceId).IsHidden), "hidden deposits remain hidden in runtime projection");
    Check(actualView.Features.Count(feature => feature.Clearable) == colony.Features.Count(feature => feature.IsBlocker && !feature.IsHidden)
        && actualView.Features.Where(feature => feature.Clearable).All(feature => feature.ClearanceSlotId is null && feature.Gates.Count > 0), "blockers keep category while clearance stays unavailable");
    Check(actualView.StartupBuildings!.Count == colony.Buildings.Count && actualView.StartupBuildings.All(item => actualView.Installed.Count(installed => installed.SlotId == item.SlotInstanceId && installed.DefinitionId == item.DefinitionId) == 1), "each startup occurrence matches one canonical runtime slot");
    Check(actualView.StartupBuildings.Select(item => item.SlotInstanceId).Distinct(StringComparer.Ordinal).Count() == colony.Buildings.Count
        && actualView.StartupBuildings.All(item => item.CompatibleZoneInstanceIds!.Count == 1 && actual.Slots[item.SlotInstanceId!].ZoneInstanceId == item.CompatibleZoneInstanceIds[0]), "exact canonical zone link per visual building");
    var executable = actualView.Catalog.Where(item => item.Eligibility == "available").ToArray();
    Check(executable.Any(item => item.DefinitionId == "district_city" && item.Quote!.Costs["minerals"] == 425 && item.Quote!.DurationDays == 480), "exact actual preset city");
    Check(executable.Any(item => item.DefinitionId == "building_foundry_1" && item.Quote!.DurationDays > 313 && item.Quote.DurationDays < 314), "actual preset foundry fractional nominal quote");
    Check(executable.Any(item => item.DefinitionId == "building_stronghold" && item.Quote!.DurationDays > 208 && item.Quote.DurationDays < 209), "actual preset stronghold fractional nominal quote");
    Console.WriteLine($"actual preset {templateId}: district_city 425 minerals / 480 days; foundry +15% nominal 7200/23, day 314");
    if (templateId == "economic") { ActualPresetLifecycle(content, actualAdapter, actual, bank, actor, colony, ConstructionOrdinaryTemplate.Get(templateId)); }
}
var building = view.Catalog.First(item => item.Kind == "building" && item.Eligibility == "available");
var district = view.Catalog.Single(item => item.DefinitionId == "district_city");
Check(district.Eligibility == "available", "source city executable");
var command = new M3ClientCommand("order-building", "enqueueConstruction", ColonyId: state.ColonyId, ExpectedColonyRevision: state.Revision, DefinitionId: building.DefinitionId, SlotId: building.SlotIds[0], OrderId: "building-1");
var order = adapter.Execute(state, bank, actor, command);
Check(order.Accepted && order.State.PendingOrderIds.Count == 1 && order.ResourceDeltas.Any(delta => delta.Amount < 0), "order pays and reserves");
var paidBank = Apply(bank, order.ResourceDeltas);
var staged = ConstructionLiveTransaction.Stage(order, bank);
Check(staged.Accepted && staged.Runtime == order.State && staged.Bank!.Accounts[1]["minerals"] == paidBank.Accounts[1]["minerals"], "typed transaction candidate");
Check(adapter.Execute(order.State, paidBank, actor, command).Code == "duplicate", "exact retry");
Check(adapter.Execute(order.State, paidBank, actor, command with { OrderId = "changed" }).Code == "command-id-conflict", "conflicting retry");
Check(adapter.Execute(state, bank, actor with { IsPlayer = false }, command).Code == "OUT_OF_SCOPE", "observer");
Check(adapter.Execute(state, bank, actor with { ParticipantId = 2 }, command).Code == "OUT_OF_SCOPE", "foreign");
Check(adapter.Execute(state, bank, actor with { AssetReady = false }, command).Code == "ASSETS_NOT_READY", "assets");
Check(!adapter.Execute(state, bank, actor, command with { SlotId = "forged" }).Accepted, "forged slot");
Check(!adapter.Execute(state, bank, actor, command with { Count = 1 }).Accepted, "strict shape");
Check(adapter.Execute(order.State, paidBank, actor, command with { CommandId = "stale" }).Code == "STALE_REVISION", "stale");
var emptyBank = new ConstructionBankSnapshot(bank.Accounts.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, decimal>)pair.Value.ToDictionary(value => value.Key, _ => 0m)), storage);
var affordableView = adapter.Project(state, 1, bank: bank);
var unfundedView = adapter.Project(state, 1, bank: emptyBank);
Check(affordableView.Catalog.Single(item => item.DefinitionId == "building_hydroponics_farm").CanAfford == true
    && unfundedView.Catalog.Single(item => item.DefinitionId == "building_hydroponics_farm") is { Eligibility: "available", CanAfford: false, Quote: not null },
    "Доступность source рецепта отделена от текущего stock/canAfford");
Check(adapter.Execute(state, emptyBank, actor, command).Code == "insufficient-resources", "insufficient atomic");
var districtCommand = new M3ClientCommand("order-district", "enqueueConstruction", ColonyId: state.ColonyId, ExpectedColonyRevision: order.State.Revision, DefinitionId: district.DefinitionId, SlotId: district.SlotIds[0], OrderId: "district-1");
Check(adapter.Execute(order.State, paidBank, actor, districtCommand).Code == "queue-kind-sharing-unresolved", "sharing gate specific");
var partial = adapter.AdvanceDay(order.State, paidBank, "day-1");
Check(partial.Accepted && partial.State.Orders["building-1"].CompletedWork > 0 && partial.State.Installed.Count == state.Installed.Count, "day work");
var buildingState = partial.State;
for (var day = 2; day <= building.Quote!.DurationDays; day++)
{
    var advance = adapter.AdvanceDay(buildingState, paidBank, "building-day-" + day);
    Check(advance.Accepted, "source building daily");
    buildingState = advance.State;
}
Check(buildingState.Installed.Count == state.Installed.Count + 1 && buildingState.Orders["building-1"].Status == ConstructionOrderStatus.Completed, "source building completion");
Check(buildingState.Installed.Values.Sum(item => item.JobCapacity.Values.Sum()) > state.Installed.Values.Sum(item => item.JobCapacity.Values.Sum()), "completion capacity refresh graph");
var cancelled = adapter.Execute(partial.State, paidBank, actor, new("cancel-building", "cancelConstruction", ColonyId: state.ColonyId, ExpectedColonyRevision: partial.State.Revision, OrderId: "building-1"));
Check(cancelled.Accepted && cancelled.State.PendingOrderIds.Count == 0, "cancel releases");
var refunded = Apply(paidBank, cancelled.ResourceDeltas);
Check(refunded.Accounts[1]["minerals"] == bank.Accounts[1]["minerals"], "refund paid");
{
    var pending = partial.State;
    var paidOrder = pending.Orders["building-1"];
    var resource = paidOrder.Quote.Costs.First(pair => pair.Value > 1m).Key;
    ConstructionBankSnapshot At(decimal stock, decimal cap = 1000m, bool unlimited = false)
    {
        var balances = paidBank.Accounts[1].ToDictionary(pair => pair.Key, pair => pair.Value);
        balances[resource] = stock;
        var accounts = paidBank.Accounts.ToDictionary(pair => pair.Key, pair => pair.Value);
        accounts[1] = balances;
        var policies = paidBank.Storage.ToDictionary(pair => pair.Key, pair => pair.Value);
        policies[resource] = new(unlimited, unlimited ? null : cap);
        return new(accounts, policies);
    }
    var cappedBank = At(999m);
    var preview = M3ConstructionAdapter.RefundPreview(pending, paidOrder, cappedBank);
    Check(preview.Amounts[resource] == 1m && preview.Losses![resource] == paidOrder.Quote.Costs[resource] - 1m,
        "Preview считает частичную потерю от фактической оплаты");
    var cancel = new M3ClientCommand("confirmed-cancel", "cancelConstruction", ColonyId: pending.ColonyId,
        ExpectedColonyRevision: pending.Revision, OrderId: paidOrder.Id, RefundPreviewId: preview.PreviewId, ConfirmRefundLoss: false);
    var rejected = adapter.Execute(pending, cappedBank, actor, cancel);
    Check(!rejected.Accepted && rejected.Code == "REFUND_CONFIRMATION_REQUIRED" && ReferenceEquals(rejected.State, pending) && rejected.ResourceDeltas.Count == 0,
        "Без подтверждения потеря не отменяет и не оплачивает заказ");
    Check(adapter.Execute(pending, cappedBank, actor, cancel with { ConfirmRefundLoss = null }).Code == "INVALID_COMMAND", "Неполная пара preview/confirm запрещена");
    Check(adapter.Execute(pending, At(998m), actor, cancel with { ConfirmRefundLoss = true }).Code == "REFUND_PREVIEW_CHANGED", "Изменение stocks инвалидирует preview без colony revision");
    Check(adapter.Execute(pending, At(999m, 1001m), actor, cancel with { ConfirmRefundLoss = true }).Code == "REFUND_PREVIEW_CHANGED", "Изменение cap инвалидирует preview");
    Check(adapter.Execute(pending.SynchronizeRevision(pending.Revision + 1), cappedBank, actor, cancel with { ConfirmRefundLoss = true }).Code == "REFUND_PREVIEW_CHANGED", "Изменение colony revision требует обновить подтверждение");
    Check(adapter.Execute(pending, cappedBank, actor with { ParticipantId = 2 }, cancel with { ConfirmRefundLoss = true }).Code == "OUT_OF_SCOPE", "Preview не даёт чужих полномочий");
    var confirmed = cancel with { ConfirmRefundLoss = true };
    var accepted = adapter.Execute(pending, cappedBank, actor, confirmed);
    Check(accepted.Accepted && accepted.ResourceDeltas.Single(item => item.ResourceId == resource).Amount == 1m
        && accepted.State.PendingOrderIds.Count == 0 && accepted.State.Orders[paidOrder.Id].CompletedWork == paidOrder.CompletedWork, "Подтверждение применяет capped refund без изменения работы");
    Check(adapter.Execute(accepted.State, At(0m), actor, confirmed).Code == "duplicate", "Replay не пересчитывает уже принятый refund");
    Check(adapter.Execute(accepted.State, cappedBank, actor, confirmed with { ConfirmRefundLoss = false }).Code == "command-id-conflict", "Изменённое подтверждение не является replay");
    Check(M3ConstructionAdapter.RefundPreview(pending, paidOrder, At(1000m)).Losses![resource] == paidOrder.Quote.Costs[resource], "Полная потеря при полном хранилище");
    Check(M3ConstructionAdapter.RefundPreview(pending, paidOrder, At(1000m, unlimited: true)).Losses!.Count == 0, "Unlimited не создаёт потерь");
    Check(adapter.Project(pending, 1, bank: cappedBank).Queue.Single(item => item.OrderId == paidOrder.Id).RefundQuote?.PreviewId == preview.PreviewId,
        "Projection и execute используют один preview");
}
var districtOrder = adapter.Execute(cancelled.State, refunded, actor, districtCommand with { ExpectedColonyRevision = cancelled.State.Revision });
Check(districtOrder.Accepted, "kind switch after empty");
var active = districtOrder.State;
var districtBank = Apply(refunded, districtOrder.ResourceDeltas);
for (var day = 1; day <= district.Quote!.DurationDays; day++)
{
    var advanced = adapter.AdvanceDay(active, districtBank, "district-day-" + day);
    Check(advanced.Accepted, "district daily candidate");
    active = advanced.State;
    if (day < district.Quote.DurationDays) { Check(active.Orders["district-1"].Status == ConstructionOrderStatus.Pending, "not early"); }
}
Check(active.Orders["district-1"].Status == ConstructionOrderStatus.Completed && active.Installed.Count == state.Installed.Count + 1, "district completion");
var before = active.Installed.Values.Sum(item => item.InfrastructureUpkeep.GetValueOrDefault("energy"));
var nextDay = adapter.AdvanceDay(active, districtBank, "next-day");
Check(nextDay.Accepted && nextDay.State.Installed.Values.Sum(item => item.InfrastructureUpkeep.GetValueOrDefault("energy")) == before, "no double upkeep");
Synthetic();
Console.WriteLine("PASS real source quotes/slots, adapter authorization/idempotency/payment/cancellation, district ordered lifecycle; synthetic storage/ownership/queue checks");

static void Check(bool condition, string label) { if (!condition) { throw new InvalidOperationException(label); } }
static void ActualPresetLifecycle(M3ConstructionContent content, M3ConstructionAdapter adapter, ConstructionLiveRuntime baseline, ConstructionBankSnapshot bank, M3ConstructionActor actor, M3StartupColony colony, ConstructionOrdinaryTemplate template)
{
    var entry = adapter.Project(baseline, 1).Catalog.Single(item => item.DefinitionId == "building_foundry_1");
    var rawCost = adapter.Quote(baseline, "building_foundry_1", entry.SlotIds[0]).Quote!.Costs["minerals"];
    var fromBinding = M3ConstructionContent.FromBinding.ActingCountry(false);
    var noBonus = new M3ConstructionEvaluation(content.Context(colony, template, baseline, fromBinding), M3ConstructionContent.EconomyFacts(colony), new("explicit-source-baseline", 0, 0, 0), fromBinding);
    var unmodified = adapter.Quote(baseline, "building_foundry_1", entry.SlotIds[0], noBonus).Quote!;
    Check(rawCost == unmodified.Costs["minerals"] * 0.85m, "actual civic cost minus fifteen percent");
    var orderCommand = new M3ClientCommand("actual-building", "enqueueConstruction", ColonyId: baseline.ColonyId, ExpectedColonyRevision: baseline.Revision, DefinitionId: entry.DefinitionId, SlotId: entry.SlotIds[0], OrderId: "actual-foundry");
    var empty = new ConstructionBankSnapshot(bank.Accounts.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, decimal>)pair.Value.ToDictionary(value => value.Key, _ => 0m)), bank.Storage);
    var denied = adapter.Execute(baseline, empty, actor, orderCommand);
    Check(!denied.Accepted && denied.Code == "insufficient-resources" && denied.State == baseline && denied.ResourceDeltas.Count == 0, "actual preset insufficient resources atomic");
    var ordered = adapter.Execute(baseline, bank, actor, orderCommand);
    Check(ordered.Accepted && ordered.ResourceDeltas.Single(delta => delta.ResourceId == "minerals").Amount == -rawCost, "actual preset fixed price at order");
    var paid = ConstructionLiveTransaction.Stage(ordered, bank);
    Check(paid.Accepted && paid.Bank is not null, "actual preset atomic bank candidate");
    var secondEntry = adapter.Project(ordered.State, 1).Catalog.Single(item => item.DefinitionId == "building_precinct_house");
    var secondSlot = secondEntry.SlotIds.First(slot => slot != entry.SlotIds[0]);
    var secondCommand = new M3ClientCommand("paid-paused", "enqueueConstruction", ColonyId: baseline.ColonyId, ExpectedColonyRevision: ordered.State.Revision, DefinitionId: secondEntry.DefinitionId, SlotId: secondSlot, OrderId: "paused-precinct");
    var secondOrder = adapter.Execute(ordered.State, paid.Bank!, actor, secondCommand);
    Check(secondOrder.Accepted, "second same-kind order for base-one queue");
    var twicePaid = ConstructionLiveTransaction.Stage(secondOrder, paid.Bank!);
    Check(twicePaid.Accepted, "second order bank candidate");
    var pausedProjection = adapter.Project(secondOrder.State, 1, bank: twicePaid.Bank, activeCapacity: 1);
    var pausedEntry = pausedProjection.Queue.Single(item => item.OrderId == "paused-precinct");
    Check(pausedEntry.Status == "pending" && pausedEntry.Gates.Count == 0 && pausedEntry.RefundQuote is not null
        && pausedProjection.Queue.ToList().FindIndex(item => item.OrderId == "paused-precinct") == 1, "paused execution does not block accepted cancel consumer");
    CheckAcceptedConsumer(pausedProjection);
    var zeroCapacityProjection = adapter.Project(secondOrder.State, 1, bank: twicePaid.Bank, activeCapacity: 0);
    Check(zeroCapacityProjection.Queue.Where(item => item.Status == "pending").All(item => item.Gates.Count == 0 && item.RefundQuote is not null), "capacity drop never removes paid cancellation");
    var pausedCancel = adapter.Execute(secondOrder.State, twicePaid.Bank!, actor, new("cancel-paused", "cancelConstruction", ColonyId: baseline.ColonyId, ExpectedColonyRevision: secondOrder.State.Revision, OrderId: "paused-precinct"));
    var afterPausedCancel = ConstructionLiveTransaction.Stage(pausedCancel, twicePaid.Bank!);
    Check(pausedCancel.Accepted && afterPausedCancel.Accepted && afterPausedCancel.Bank!.Accounts[1]["minerals"] == paid.Bank!.Accounts[1]["minerals"]
        && pausedCancel.State.PendingOrderIds.SequenceEqual(["actual-foundry"]) && pausedCancel.State.Orders["actual-foundry"].CompletedWork == 0
        && pausedCancel.State.Installed.Count == baseline.Installed.Count, "paused cancel refunds original payer and preserves active order");
    var state = ordered.State;
    for (var day = 1; day <= 313; day++)
    {
        var result = adapter.AdvanceDay(state, paid.Bank!, "actual-day-" + day);
        Check(result.Accepted, "actual preset daily increment");
        state = result.State;
    }
    Check(state.Orders["actual-foundry"].Status == ConstructionOrderStatus.Pending && state.Orders["actual-foundry"].CompletedWork == 313 * 1.15m, "day 313 before completion");
    var boundary = adapter.AdvanceDay(state, paid.Bank!, "actual-day-314");
    Check(boundary.Accepted && boundary.State.Orders["actual-foundry"].Status == ConstructionOrderStatus.Completed && boundary.State.Installed.Count == baseline.Installed.Count + 1, "day 314 completes before monthly candidate");
    Check(boundary.State.Orders["actual-foundry"].CompletedWork == boundary.State.Orders["actual-foundry"].Quote.RequiredWork, "daily remainder discarded");
    var nextDay = adapter.AdvanceDay(boundary.State, paid.Bank!, "actual-day-315");
    Check(nextDay.Accepted && nextDay.State.Installed.Count == boundary.State.Installed.Count, "no repeated upkeep or second install");
    var invalidSpeed = new M3ConstructionEvaluation(noBonus.Context, noBonus.EconomyFacts, new("invalid-speed", -0.15m, -1, 0), fromBinding);
    Check(adapter.Quote(baseline, entry.DefinitionId, entry.SlotIds[0], invalidSpeed).Code == "invalid-modifier-factor", "nonpositive denominator refuses quote");
    var changed = ordered.State;
    for (var day = 1; day <= 10; day++)
    {
        var result = adapter.AdvanceDay(changed, paid.Bank!, "changed-fast-" + day);
        Check(result.Accepted, "fast days");
        changed = result.State;
    }
    var completedFastWork = changed.Orders["actual-foundry"].CompletedWork;
    var liveZero = new M3ConstructionEvaluation(noBonus.Context, noBonus.EconomyFacts, new("speed-changed-live", -0.15m, 0, 0), fromBinding);
    var cancelledForSpeed = adapter.AdvanceDay(changed, paid.Bank!, "invalid-day", evaluation: invalidSpeed);
    Check(cancelledForSpeed.Accepted && cancelledForSpeed.State.PendingOrderIds.Count == 0
        && cancelledForSpeed.State.Orders["actual-foundry"].Status == ConstructionOrderStatus.Cancelled
        && cancelledForSpeed.State.Orders["actual-foundry"].CancellationReason == ConstructionCancellationReason.NonPositiveSpeed
        && cancelledForSpeed.State.Orders["actual-foundry"].CompletedWork == completedFastWork
        && cancelledForSpeed.State.Installed.Count == changed.Installed.Count
        && cancelledForSpeed.ResourceDeltas.Sum(delta => delta.Amount) == changed.Orders["actual-foundry"].Quote.Costs.Values.Sum(), "Неположительная live speed отменяет заказ, сохраняя работу и возвращая оплату");
    Check(adapter.Project(cancelledForSpeed.State, 1).Queue.Single(order => order.OrderId == "actual-foundry").CancellationReason == "non-positive-speed", "Причина отмены не дошла до DTO");
    for (var day = 11; day <= 358; day++)
    {
        var result = adapter.AdvanceDay(changed, paid.Bank!, "changed-slow-" + day, evaluation: liveZero);
        Check(result.Accepted, "slow days");
        changed = result.State;
    }
    Check(changed.Orders["actual-foundry"].Status == ConstructionOrderStatus.Pending && changed.Orders["actual-foundry"].CompletedWork == 359.5m, "remaining base work preserved at live speed change");
    var final = adapter.AdvanceDay(changed, paid.Bank!, "changed-slow-359", evaluation: liveZero);
    Check(final.Accepted && final.State.Orders["actual-foundry"].Status == ConstructionOrderStatus.Completed, "speed change completes day 359");
    var cancel = adapter.Execute(ordered.State, paid.Bank!, actor, new("actual-cancel", "cancelConstruction", ColonyId: baseline.ColonyId, ExpectedColonyRevision: ordered.State.Revision, OrderId: "actual-foundry"));
    Check(cancel.Accepted && ConstructionLiveTransaction.Stage(cancel, paid.Bank!).Bank!.Accounts[1]["minerals"] == bank.Accounts[1]["minerals"], "actual preset cancel refunds fixed paid price");
}
static IReadOnlyDictionary<string, ConstructionInstalled> FakeBaseline(M3StartupColony startup)
    => startup.Buildings.Select(item => (Id: item.InstanceId, Definition: item.DefinitionId, Kind: ConstructionKind.Building))
        .Concat(startup.Districts.Select(item => (Id: item.InstanceId, Definition: item.DefinitionId, Kind: ConstructionKind.District)))
        .ToDictionary(item => item.Id, item => new ConstructionInstalled(item.Id, item.Definition, item.Kind, new Dictionary<string, decimal>(), new Dictionary<string, decimal>()));
static void CheckAcceptedConsumer(M3DevelopmentColonyView projection)
{
    var path = Path.Combine(Path.GetTempPath(), "stellaris-construction-consumer-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { id = projection.ColonyId, gates = Array.Empty<object>(), development = projection }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        var start = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "consumer.test.mjs"));
        start.ArgumentList.Add(path);
        start.ArgumentList.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Stellaris.Server/wwwroot/m3-construction-controls.mjs")));
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Node consumer test did not start.");
        if (!process.WaitForExit(10000)) { process.Kill(entireProcessTree: true); throw new InvalidOperationException("Node consumer test timed out."); }
        var result = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        Check(process.ExitCode == 0, "accepted consumer integration: " + error);
        Console.WriteLine(result.Trim());
    }
    finally { File.Delete(path); }
}
static ConstructionBankSnapshot Apply(ConstructionBankSnapshot bank, IReadOnlyList<ConstructionResourceDelta> deltas)
{
    var accounts = bank.Accounts.ToDictionary(pair => pair.Key, pair => pair.Value.ToDictionary());
    foreach (var delta in deltas)
    {
        var value = checked(accounts[delta.PayerId][delta.ResourceId] + delta.Amount);
        var policy = bank.Storage[delta.ResourceId];
        if (value < 0 || !policy.Unlimited && value > policy.Cap) { throw new InvalidOperationException("bank transaction invalid"); }
        accounts[delta.PayerId][delta.ResourceId] = value;
    }
    return new(accounts.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, decimal>)pair.Value), bank.Storage);
}

static void Synthetic()
{
    var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = new Dictionary<string, decimal> { ["minerals"] = 99, ["unity"] = 99 }, [2] = new Dictionary<string, decimal> { ["minerals"] = 0, ["unity"] = 0 } }, new Dictionary<string, ConstructionStorage> { ["minerals"] = new(false, 100), ["unity"] = new(true, null) });
    var state = new ConstructionLiveRuntime("synthetic", 1, [new("s1", ConstructionKind.Building, "z", "zone"), new("s2", ConstructionKind.Building, "z", "zone")], []);
    var quote = new ConstructionQuote("synthetic", "explicit", "building", ConstructionKind.Building, 2, new Dictionary<string, decimal> { ["minerals"] = 10, ["unity"] = 10 }, new Dictionary<string, decimal> { ["energy"] = 1 }, new Dictionary<string, decimal> { ["soldier"] = 100 }, []);
    var first = state.Enqueue("a", 1, 0, "a", "s1", quote, bank);
    var second = first.State.Enqueue("b", 1, 1, "b", "s2", quote, bank);
    Check(first.Accepted && second.Accepted, "synthetic orders");
    var negativeSpeeds = new Dictionary<string, decimal?> { ["a"] = -1, ["b"] = -2 };
    var zeroCapacityCancel = second.State.AdvanceDay("negative-n0", second.State.Revision, 0, negativeSpeeds,
        new Dictionary<string, bool?> { ["a"] = null, ["b"] = true }, bank);
    Check(zeroCapacityCancel.Accepted && zeroCapacityCancel.State.PendingOrderIds.Count == 0
        && zeroCapacityCancel.State.Orders.Values.All(order => order.CancellationReason == ConstructionCancellationReason.NonPositiveSpeed && order.CompletedWork == 0)
        && zeroCapacityCancel.ResourceDeltas.Single(delta => delta.ResourceId == "minerals").Amount == 1
        && zeroCapacityCancel.ResourceDeltas.Single(delta => delta.ResourceId == "unity").Amount == 20, "N=0 не скрывает индивидуальную неработоспособность; refund capped/unlimited");
    var promotionSpeeds = new Dictionary<string, decimal?> { ["a"] = -1, ["b"] = 0 };
    var speedConditions = new Dictionary<string, bool?> { ["a"] = true, ["b"] = true };
    var speedCancel = second.State.AdvanceDay("speed-cancel", second.State.Revision, 1, promotionSpeeds, speedConditions, bank);
    Check(speedCancel.Accepted && speedCancel.State.PendingOrderIds.SequenceEqual(["b"]) && speedCancel.State.Orders["b"].CompletedWork == 0, "Отмена active не переносит дневную работу следующему");
    var replaySpeedCancel = speedCancel.State.AdvanceDay("speed-cancel", second.State.Revision, 1, promotionSpeeds, speedConditions, bank);
    Check(replaySpeedCancel.Accepted && replaySpeedCancel.Code == "duplicate" && replaySpeedCancel.ResourceDeltas.Count == 0, "Повтор отмены speed не возвращает деньги дважды");
    var promotedDay = speedCancel.State.AdvanceDay("promoted-day", speedCancel.State.Revision, 1, promotionSpeeds, speedConditions, bank);
    Check(promotedDay.Accepted && promotedDay.State.Orders["b"].CompletedWork == 1, "Следующая дневная фаза не продвигает новый active");
    var speeds = new Dictionary<string, decimal?> { ["a"] = 0, ["b"] = 0 };
    var eligible = new Dictionary<string, bool?> { ["a"] = true, ["b"] = true };
    var paused = second.State.AdvanceDay("pause", 2, 0, speeds, eligible, bank);
    Check(paused.Accepted && paused.State.Orders.Values.All(order => order.CompletedWork == 0), "capacity pause");
    var reordered = paused.State.Reorder("reorder", 1, paused.State.Revision, ["b", "a"]);
    var day = reordered.State.AdvanceDay("day", reordered.State.Revision, 1, speeds, eligible, bank);
    Check(day.Accepted && day.State.Orders["b"].CompletedWork == 1 && day.State.Orders["a"].CompletedWork == 0, "reorder base1");
    var cancel = day.State.Cancel("cancel", 1, day.State.Revision, "b", bank);
    Check(cancel.ResourceDeltas.Single(delta => delta.ResourceId == "minerals").Amount == 1 && cancel.ResourceDeltas.Single(delta => delta.ResourceId == "unity").Amount == 10, "cap overflow lost and unlimited");
    var lost = cancel.State.AdvanceDay("lost", cancel.State.Revision, 1, speeds, new Dictionary<string, bool?> { ["a"] = false }, bank);
    Check(lost.Accepted && lost.State.PendingOrderIds.Count == 0 && lost.ResourceDeltas.All(delta => delta.PayerId == 1), "condition loss original payer");
    var transfer = first.State.ChangeOwner("transfer", first.State.Revision, 2, false, bank);
    Check(transfer.Accepted && transfer.State.OwnerId == 2 && transfer.ResourceDeltas.All(delta => delta.PayerId == 1), "ownership refund");
    var destroyed = first.State.ChangeOwner("destroy", first.State.Revision, 2, true, bank);
    Check(destroyed.Accepted && destroyed.State.Destroyed && destroyed.ResourceDeltas.Count == 0, "destruction no refund");
    Check(destroyed.State.Enqueue("after-destroy", 2, destroyed.State.Revision, "x", "s2", quote, bank).Code == "colony-destroyed", "destroyed cannot build");
    var transferBoth = second.State.ChangeOwner("both-transfer", second.State.Revision, 2, false, bank);
    Check(transferBoth.Accepted && transferBoth.ResourceDeltas.Single(delta => delta.ResourceId == "minerals").Amount == 1, "batch refund single cap headroom");
    var invalidDelta = ConstructionLiveTransaction.Stage(first with { ResourceDeltas = [new(1, "minerals", -1000)] }, bank);
    Check(!invalidDelta.Accepted && invalidDelta.Runtime is null && invalidDelta.Bank is null && state.Revision == 0 && bank.Accounts[1]["minerals"] == 99, "bank failure discards both candidates");
    var complete = day.State.AdvanceDay("boundary", day.State.Revision, 1, speeds, eligible, bank);
    Check(complete.Accepted && complete.State.Orders["b"].Status == ConstructionOrderStatus.Completed && complete.State.Orders["a"].CompletedWork == 0, "boundary completion no carry");
    var monthlyUpkeep = complete.State.Installed.Values.Sum(item => item.InfrastructureUpkeep.GetValueOrDefault("energy"));
    Check(monthlyUpkeep == 1, "completion before fake monthly settlement");
    Check(day.State.Installed.Count == 0, "failed outer settlement can retain previous graph");
    Check(complete.State.AdvanceDay("boundary", day.State.Revision, 1, speeds, eligible, bank).Code == "duplicate", "boundary retry no double completion");
    var fractionalQuote = new ConstructionQuote("synthetic", "accepted-daily-work-v1", "building", ConstructionKind.Building, 2.5m, new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), []);
    var fractionalA = state.Enqueue("fractional-a", 1, state.Revision, "fractional-a", "s1", fractionalQuote, bank);
    var fractionalB = fractionalA.State.Enqueue("fractional-b", 1, fractionalA.State.Revision, "fractional-b", "s2", fractionalQuote, bank);
    var activeRemainder = fractionalB.State;
    var fast = new Dictionary<string, decimal?> { ["fractional-a"] = 0.15m, ["fractional-b"] = 0.15m };
    var bothEligible = new Dictionary<string, bool?> { ["fractional-a"] = true, ["fractional-b"] = true };
    for (var index = 1; index <= 3; index++)
    {
        var transition = activeRemainder.AdvanceDay("remainder-" + index, activeRemainder.Revision, 1, fast, bothEligible, bank);
        Check(transition.Accepted, "fractional source-profile daily work");
        activeRemainder = transition.State;
    }
    Check(activeRemainder.Orders["fractional-a"].CompletedWork == 2.5m && activeRemainder.Orders["fractional-b"].CompletedWork == 0, "fractional final-day remainder never carries into next order");
}
