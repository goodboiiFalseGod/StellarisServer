using System.Text.Json;
using System.Security.Cryptography;
using System.Collections.Frozen;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;

namespace Stellaris.Session.M3.Construction;

public sealed record M3ConstructionActor(int ParticipantId, bool IsPlayer, bool AssetReady, bool Running);
public sealed record M3ConstructionNumerics(string PolicyId, decimal CostFraction, decimal BuildingSpeedFraction, decimal DistrictSpeedFraction, decimal? ZoneSpeedFraction = null);
public sealed class M3ConstructionEvaluation
{
    public ConstructionContext Context { get; }
    public IReadOnlyDictionary<string, bool> EconomyFacts { get; }
    public M3ConstructionNumerics Numerics { get; }
    public M3ConstructionContent.FromBinding? FromBinding { get; }
    public M3ConstructionEvaluation(ConstructionContext context, IReadOnlyDictionary<string, bool> economyFacts, M3ConstructionNumerics numerics, M3ConstructionContent.FromBinding? fromBinding = null)
    {
        if (context.FromNomadic != fromBinding?.IsNomadic || fromBinding is not null && fromBinding.PolicyId != "m3-authenticated-acting-country-v1") { throw new ArgumentException("Optional-from scope requires its own trusted binding."); }
        Context = context;
        EconomyFacts = economyFacts.ToFrozenDictionary(StringComparer.Ordinal);
        Numerics = numerics;
        FromBinding = fromBinding;
    }
}

/// <summary>Адаптер существующего wire envelope. Возвращает candidate и typed deltas без публикации экономики.</summary>
public sealed partial class M3ConstructionAdapter(M3ConstructionContent source, M3StartupColony startup, ConstructionOrdinaryTemplate template, M3ConstructionNumerics? explicitNumerics = null, M3ConstructionContent.FromBinding? fromBinding = null,
    Func<IReadOnlySet<string>>? researchedTechnologies = null, decimal? sapientPopulation = null)
{
    private ConstructionContext Context(ConstructionLiveRuntime state) => source.Context(startup, template, state, fromBinding, researchedTechnologies?.Invoke(), sapientPopulation);
    private (decimal Cost, decimal? Speed) Modifiers(ConstructionKind kind, M3ConstructionEvaluation? evaluation = null)
    {
        var numerics = evaluation?.Numerics ?? explicitNumerics;
        if (numerics is not null) return (numerics.CostFraction, kind switch
        { ConstructionKind.Building or ConstructionKind.Zone => numerics.BuildingSpeedFraction, ConstructionKind.District => numerics.DistrictSpeedFraction, _ => null });
        var preset = source.PresetModifiers(template, kind == ConstructionKind.Zone ? ConstructionKind.Building : kind);
        return (preset.Cost, preset.Speed);
    }
    private decimal Duration(ConstructionQuote quote, M3ConstructionEvaluation? evaluation)
        => quote.RequiredWork / (1 + Modifiers(quote.Kind, evaluation).Speed!.Value);
    public ConstructionLiveResult Execute(ConstructionLiveRuntime state, ConstructionBankSnapshot bank, M3ConstructionActor actor, M3ClientCommand command, M3ConstructionEvaluation? evaluation = null)
    {
        ConstructionLiveResult Reject(string code) => new(false, code, state, []);
        if (!actor.IsPlayer || actor.ParticipantId != state.OwnerId) { return Reject("OUT_OF_SCOPE"); }
        if (!actor.AssetReady) { return Reject("ASSETS_NOT_READY"); }
        if (!actor.Running) { return Reject("INVALID_PHASE"); }
        var allowed = command.Type switch
        {
            "enqueueConstruction" or "clearBlocker" => new[] { "ColonyId", "ExpectedColonyRevision", "DefinitionId", "SlotId", "OrderId" },
            "enqueueUpgrade" => new[] { "ColonyId", "ExpectedColonyRevision", "SourceDefinitionId", "DefinitionId", "SlotId", "ZoneInstanceId", "OrderId" },
            "enqueueZoneReplacement" => new[] { "ColonyId", "ExpectedColonyRevision", "SourceDefinitionId", "DefinitionId", "SlotId", "ZoneInstanceId", "OrderId", "ZoneReplacementPreviewId", "ConfirmZoneRemoval" },
            "setBuildingEnabled" => new[] { "ColonyId", "ExpectedColonyRevision", "DefinitionId", "SlotId", "Enabled" },
            "demolishBuilding" => new[] { "ColonyId", "ExpectedColonyRevision", "DefinitionId", "SlotId", "ConfirmDemolition" },
            "cancelConstruction" => new[] { "ColonyId", "ExpectedColonyRevision", "OrderId", "RefundPreviewId", "ConfirmRefundLoss" },
            "reorderConstruction" => new[] { "ColonyId", "ExpectedColonyRevision", "OrderIds" },
            _ => []
        };
        if (allowed.Length == 0 || typeof(M3ClientCommand).GetProperties().Where(property => property.Name is not ("CommandId" or "Type"))
            .Any(property => allowed.Contains(property.Name) != (property.GetValue(command) is not null)
                && !(command.Type == "cancelConstruction" && property.Name is ("RefundPreviewId" or "ConfirmRefundLoss") && property.GetValue(command) is null))) { return Reject("INVALID_COMMAND"); }
        if ((command.RefundPreviewId is null) != (command.ConfirmRefundLoss is null)) { return Reject("INVALID_COMMAND"); }
        if (new[] { command.CommandId, command.ColonyId, command.OrderId, command.DefinitionId, command.SlotId, command.SourceDefinitionId, command.ZoneInstanceId, command.ZoneReplacementPreviewId }.Where(id => id is not null).Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160 || id.Any(char.IsControl)) || command.ExpectedColonyRevision < 0) { return Reject("INVALID_COMMAND"); }
        if (command.ColonyId != state.ColonyId || startup.ColonyId != state.ColonyId) { return Reject("OUT_OF_SCOPE"); }
        if (command.OrderIds is { } ids && (ids.Length == 0 || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160 || id.Any(char.IsControl))
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)) { return Reject("INVALID_COMMAND"); }
        if (state.Destroyed) { return Reject("colony-destroyed"); }
        if (startup.OwnerParticipantId != state.OwnerId) { return Reject("ownership-context-refresh-required"); }
        var identity = JsonSerializer.Serialize(command);
        var replay = state.ReplayCommand(command.CommandId, actor.ParticipantId, command.ExpectedColonyRevision!.Value, identity);
        if (replay is not null) { return replay; }
        if (command.ExpectedColonyRevision != state.Revision) { return Reject(command.Type == "enqueueZoneReplacement" ? "ZONE_REPLACEMENT_PREVIEW_CHANGED" : command.Type == "cancelConstruction" && command.RefundPreviewId is not null ? "REFUND_PREVIEW_CHANGED" : "STALE_REVISION"); }
        if (command.Type == "enqueueZoneReplacement")
        {
            var preview = PreviewZoneReplacement(state, command.DefinitionId!, command.SourceDefinitionId!, command.SlotId!, command.ZoneInstanceId!, bank, evaluation);
            if (preview.Preview is not { } current) return Reject(preview.Code);
            if (current.PreviewId != command.ZoneReplacementPreviewId) return Reject("ZONE_REPLACEMENT_PREVIEW_CHANGED");
            if (current.RemovedBuildings.Count > 0 && command.ConfirmZoneRemoval != true) return Reject("ZONE_REMOVAL_CONFIRMATION_REQUIRED");
            if (current.Gates.Count > 0) return Reject(current.Gates[0].DependencyId);
            var quote = QuoteZoneReplacement(state, current.DefinitionId, current.SourceDefinitionId, current.SlotId, current.ZoneInstanceId, evaluation, true);
            if (quote.Quote is null) return Reject(quote.Code);
            return state.Enqueue(command.CommandId, actor.ParticipantId, state.Revision, command.OrderId!, current.SlotId, quote.Quote, bank, identity,
                zoneReplacement: new(current.SourceDefinitionId, current.ZoneInstanceId, current.RemovedBuildings.ToDictionary(item => item.SlotId, item => item.DefinitionId, StringComparer.Ordinal)));
        }
        if (command.Type == "setBuildingEnabled")
        {
            if (!state.Installed.TryGetValue(command.SlotId!, out var installed) || installed.Kind != ConstructionKind.Building || installed.DefinitionId != command.DefinitionId)
                return Reject("building-target-mismatch");
            var gate = ToggleGate(state, installed);
            if (gate is not null) return Reject(gate);
            return state.SetBuildingEnabled(command.CommandId, actor.ParticipantId, state.Revision, installed.SlotId, installed.DefinitionId, command.Enabled!.Value, identity);
        }
        if (command.Type == "demolishBuilding")
        {
            if (command.ConfirmDemolition != true) return Reject("DEMOLITION_CONFIRMATION_REQUIRED");
            if (!state.Installed.TryGetValue(command.SlotId!, out var installed) || installed.Kind != ConstructionKind.Building || installed.DefinitionId != command.DefinitionId)
                return Reject("building-target-mismatch");
            var gate = DemolitionGate(state, installed);
            if (gate is not null) return Reject(gate);
            return state.DemolishBuilding(command.CommandId, actor.ParticipantId, state.Revision, installed.SlotId, installed.DefinitionId, identity);
        }
        if (command.Type == "clearBlocker") { return Reject("clearance-lifecycle-unresolved"); }
        if (command.Type == "cancelConstruction")
        {
            if (!state.Orders.TryGetValue(command.OrderId!, out var order) || order.Status != ConstructionOrderStatus.Pending) { return Reject("order-unavailable"); }
            var preview = RefundPreview(state, order, bank);
            if (command.RefundPreviewId is not null && command.RefundPreviewId != preview.PreviewId) { return Reject("REFUND_PREVIEW_CHANGED"); }
            if (preview.Losses!.Count > 0 && command.ConfirmRefundLoss != true) { return Reject("REFUND_CONFIRMATION_REQUIRED"); }
            return state.Cancel(command.CommandId, actor.ParticipantId, state.Revision, command.OrderId!, bank, identity);
        }
        if (command.Type == "reorderConstruction") { return state.Reorder(command.CommandId, actor.ParticipantId, state.Revision, command.OrderIds!, identity, preserveActiveHead: true); }
        var upgrade = command.Type == "enqueueUpgrade";
        var result = upgrade ? QuoteUpgrade(state, command.DefinitionId!, command.SourceDefinitionId!, command.SlotId!, command.ZoneInstanceId!, evaluation)
            : Quote(state, command.DefinitionId!, command.SlotId!, evaluation);
        if (result.Quote is null) { return Reject(result.Code); }
        return state.Enqueue(command.CommandId, actor.ParticipantId, state.Revision, command.OrderId!, command.SlotId!, result.Quote, bank, identity,
            upgrade ? command.SourceDefinitionId : null, upgrade ? command.ZoneInstanceId : null);
    }

    public ConstructionLiveQuoteResult Quote(ConstructionLiveRuntime state, string id, string slotId, M3ConstructionEvaluation? evaluation = null)
    {
        var context = evaluation?.Context ?? Context(state);
        if (source.EvaluateRuntimeGate(id, context) is { } dependency) { return new(null, dependency); }
        if (!state.Slots.TryGetValue(slotId, out var slot) || state.Installed.ContainsKey(slotId) || state.Orders.Values.Any(order => order.SlotId == slotId && order.Status == ConstructionOrderStatus.Pending)) { return new(null, "slot-unavailable"); }
        if (slot.Kind == ConstructionKind.Building && HasZoneReplacement(state, slot.ZoneInstanceId)) return new(null, "zone-order-cancel-required");
        if (!source.Content.Catalogs.TryGetValue(id, out var catalog)) { return new(null, "definition-unavailable"); }
        var kind = catalog.Definitions[id].Kind switch { "building" => ConstructionKind.Building, "district" => ConstructionKind.District, _ => ConstructionKind.Zone };
        if (kind != slot.Kind) { return new(null, "slot-kind-mismatch"); }
        if (kind == ConstructionKind.Zone)
        {
            if (!M3ZoneGraph.IsBasicZone(id) && !source.FixedCityZones.Contains(id)) return new(null, "zone-specialization-lifecycle-unresolved");
            if (slot.ZoneSlotDefinitionId is null || !M3ZoneGraph.Fits(source, id, slot.ZoneSlotDefinitionId)) return new(null, "zone-set-excluded");
            var eligibility = M3ZoneGraph.Eligibility(source, slot, evaluation?.Context ?? Context(state));
            if (eligibility.Gate is not null) return new(null, eligibility.Gate);
            if (M3ZoneGraph.AttachedLevel(startup, state, slot.DistrictInstanceId!) <= 0) return new(null, "zone-district-unavailable");
        }
        if (kind == ConstructionKind.Building && (slot.ZoneDefinitionId is null || !M3ZoneGraph.BuildingFits(source, id, slot.ZoneDefinitionId))) { return new(null, "building-set-excluded"); }
        if (kind == ConstructionKind.District && id != "district_city")
        {
            var capacity = M3DistrictCapacity.Evaluate(source, startup, state, id, evaluation?.Context ?? Context(state), evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup));
            if (capacity.Gate is not null) return new(null, capacity.Gate);
            var installed = state.Installed.Values.Count(item => item.Kind == ConstructionKind.District && capacity.Members.Contains(item.DefinitionId));
            var reserved = state.Orders.Values.Count(item => item.Status == ConstructionOrderStatus.Pending && item.Quote.Kind == ConstructionKind.District && capacity.Members.Contains(item.Quote.DefinitionId));
            if (capacity.Capacity is { } cap && installed + reserved >= cap) return new(null, "district-type-capacity-exhausted");
        }
        var modifiers = Modifiers(kind, evaluation);
        var facts = evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup);
        var result = QuoteSource(id, context, modifiers.Cost, modifiers.Speed, facts);
        if (result.Quote is { Kind: ConstructionKind.Zone } quote)
        {
            var level = M3ZoneGraph.AttachedLevel(startup, state, slot.DistrictInstanceId!);
            result = result with { Quote = new(quote.ContentProfileId, quote.PolicyId, quote.DefinitionId, quote.Kind, quote.RequiredWork, quote.Costs,
                M3ZoneGraph.Contributions(source, id, level, PlanetEconomyTermKind.ZoneUpkeep, facts),
                M3ZoneGraph.Contributions(source, id, level, PlanetEconomyTermKind.WorkforceCapacity, facts), quote.Gates, quote.PlanetLimit) };
        }
        return PendingLimit(result, state);
    }

    public ConstructionLiveQuoteResult QuoteUpgrade(ConstructionLiveRuntime state, string targetId, string sourceId, string slotId, string zoneInstanceId, M3ConstructionEvaluation? evaluation = null)
        => QuoteUpgradeCore(state, targetId, sourceId, slotId, zoneInstanceId, evaluation, true);

    private ConstructionLiveQuoteResult QuoteUpgradeCore(ConstructionLiveRuntime state, string targetId, string sourceId, string slotId, string zoneInstanceId, M3ConstructionEvaluation? evaluation, bool checkReservation)
    {
        if (!state.Slots.TryGetValue(slotId, out var slot) || slot.Kind != ConstructionKind.Building || slot.ZoneInstanceId != zoneInstanceId
            || !state.Installed.TryGetValue(slotId, out var installed) || installed.DefinitionId != sourceId || installed.Kind != ConstructionKind.Building)
        { return new(null, "upgrade-source-mismatch"); }
        if (checkReservation && state.Orders.Values.Any(order => order.SlotId == slotId && order.Status == ConstructionOrderStatus.Pending)) { return new(null, "slot-unavailable"); }
        if (HasZoneReplacement(state, zoneInstanceId)) return new(null, "zone-order-cancel-required");
        if (!source.Content.Economy.Buildings.TryGetValue(sourceId, out var previous) || !Values(previous, "upgrades").Contains(targetId)) { return new(null, "upgrade-edge-unavailable"); }
        var context = evaluation?.Context ?? Context(state);
        if (source.EvaluateRuntimeGate(targetId, context) is { } dependency) { return new(null, dependency); }
        if (slot.ZoneDefinitionId is null || !M3ZoneGraph.BuildingFits(source, targetId, slot.ZoneDefinitionId)) { return new(null, "building-set-excluded"); }
        var modifiers = Modifiers(ConstructionKind.Building, evaluation);
        var quote = QuoteSource(targetId, context, modifiers.Cost, modifiers.Speed,
            evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup), sourceId);
        return checkReservation ? PendingLimit(quote, state) : quote;
    }

    private static ConstructionLiveQuoteResult PendingLimit(ConstructionLiveQuoteResult result, ConstructionLiveRuntime state)
        => result.Quote?.PlanetLimit is { } limit && state.Installed.Values.Count(item => item.DefinitionId == result.Quote.DefinitionId)
            + state.Orders.Values.Count(order => order.Status == ConstructionOrderStatus.Pending && order.Quote.DefinitionId == result.Quote.DefinitionId) >= limit
            ? new(null, "planet-limit-reached") : result;

    private string? ToggleGate(ConstructionLiveRuntime state, ConstructionInstalled installed)
    {
        var definitionId = installed.DefinitionId;
        if (!source.Content.Catalogs.TryGetValue(definitionId, out var catalog)) return "building-disable-permission-unresolved";
        var fields = catalog.Definitions[definitionId].Fields.Where(field => field.Key == "can_be_disabled").ToArray();
        // Подтверждены ordinary factory/silo; отсутствие флага не открывает остальные здания.
        var context = startup.Plan.Context;
        if (fields.Length == 0 && definitionId is "building_factory_1" or "building_resource_silo"
            && context.Authority == Stellaris.Core.Content.Construction.Startup.StartupAuthority.Regular
            && context.Origin == "origin_default" && !context.IsNomadic && !context.IsWilderness && !context.IsIndividualMachine)
        {
            if (state.Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending && order.Quote.Kind == ConstructionKind.Zone))
                return "zone-order-cancel-required";
            if (state.Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending && order.Quote.Kind == ConstructionKind.Building))
                return "building-order-cancel-required";
            return null;
        }
        return fields is [var field] && field.Operator == "=" && field.Children.Count == 0
            ? field.Scalar switch { "yes" => null, "no" => "building-disable-forbidden", _ => "building-disable-permission-unresolved" }
            : "building-disable-permission-unresolved";
    }

    private ConstructionLiveQuoteResult QuoteSource(string id, ConstructionContext context, decimal? cost, decimal? speed,
        IReadOnlyDictionary<string, bool> facts, string? upgradeFrom = null)
        => source.Content.Quote(id, context, cost, speed, facts, upgradeFrom,
            source.EconomyConditionEvaluator(id, context),
            consumedCountryModifier: source.ResourceSilo(id, context) is { } silo ? silo.Consumes : null);

    private string? DemolitionGate(ConstructionLiveRuntime state, ConstructionInstalled installed)
    {
        if (HasZoneReplacement(state, state.Slots[installed.SlotId].ZoneInstanceId)) return "zone-order-cancel-required";
        if (state.Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending && order.SlotId == installed.SlotId))
            return "building-order-cancel-required";
        if (!source.Content.Catalogs.TryGetValue(installed.DefinitionId, out var catalog)) return "building-demolition-permission-unresolved";
        var fields = catalog.Definitions[installed.DefinitionId].Fields.Where(field => field.Key == "can_demolish").ToArray();
        if (fields is not [var field] || field.Operator != "=" || field.Children.Count != 0 || field.Scalar is not ("yes" or "no"))
            return "building-demolition-permission-unresolved";
        if (field.Scalar == "no") return "building-demolition-forbidden";
        return null;
    }

    public ConstructionLiveResult AdvanceDay(ConstructionLiveRuntime state, ConstructionBankSnapshot bank, string dayId, int activeCapacity = 1, M3ConstructionEvaluation? evaluation = null)
    {
        if (state.Destroyed || startup.OwnerParticipantId != state.OwnerId) { return new(false, "ownership-context-refresh-required", state, []); }
        var (speeds, eligibility) = PendingContext(state, evaluation);
        var result = state.AdvanceDay(dayId, state.Revision, activeCapacity, speeds, eligibility, bank);
        return result.Accepted ? result with { State = M3ZoneGraph.Materialize(source, startup, result.State) } : result;
    }

    public ConstructionLiveResult RefreshPendingConditions(ConstructionLiveRuntime state, ConstructionBankSnapshot bank, M3ConstructionEvaluation? evaluation = null)
    {
        if (state.Destroyed || startup.OwnerParticipantId != state.OwnerId) { return new(false, "ownership-context-refresh-required", state, []); }
        var (speeds, eligibility) = PendingContext(state, evaluation);
        return state.RefreshPendingConditions(speeds, eligibility, bank);
    }

    private (IReadOnlyDictionary<string, decimal?> Speeds, IReadOnlyDictionary<string, bool?> Eligibility) PendingContext(ConstructionLiveRuntime state, M3ConstructionEvaluation? evaluation)
    {
        var speeds = new Dictionary<string, decimal?>();
        var eligibility = new Dictionary<string, bool?>();
        foreach (var id in state.PendingOrderIds)
        {
            var order = state.Orders[id];
            var modifiers = Modifiers(order.Quote.Kind, evaluation);
            var current = order.UpgradeFromDefinitionId is { } previous
                ? QuoteUpgradeCore(state, order.Quote.DefinitionId, previous, order.SlotId, order.UpgradeZoneInstanceId!, evaluation, false)
                : QuoteSource(order.Quote.DefinitionId, evaluation?.Context ?? Context(state), modifiers.Cost, modifiers.Speed, evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup));
            speeds[id] = modifiers.Speed;
            eligibility[id] = current.Quote is not null ? true : current.Code is "not-eligible" or "upgrade-source-mismatch" or "upgrade-edge-unavailable" ? false : null;
            if (order.Quote.Kind == ConstructionKind.Zone)
            {
                var slot = state.Slots[order.SlotId];
                var zone = M3ZoneGraph.Eligibility(source, slot, evaluation?.Context ?? Context(state));
                if (zone.Gate == "zone-slot-locked" || !M3ZoneGraph.Fits(source, order.Quote.DefinitionId, slot.ZoneSlotDefinitionId!)
                    || M3ZoneGraph.AttachedLevel(startup, state, slot.DistrictInstanceId!) <= 0) eligibility[id] = false;
                else if (zone.Gate is not null && eligibility[id] != false) eligibility[id] = null;
                if (order.ZoneReplacement is { } replacing)
                {
                    var assessed = PreviewZoneReplacement(state, order.Quote.DefinitionId, replacing.SourceDefinitionId, order.SlotId,
                        replacing.ZoneInstanceId, null, evaluation, false);
                    if (assessed.Preview is not { } preview) eligibility[id] = assessed.Code == "zone-replacement-source-mismatch" ? false : null;
                    else if (assessed.Code is "zone-capital-building-present" or "zone-building-order-cancel-required") eligibility[id] = false;
                    else if (!preview.RemovedBuildings.Select(item => (item.SlotId, item.DefinitionId)).OrderBy(item => item.SlotId, StringComparer.Ordinal)
                        .SequenceEqual(replacing.RemovedBuildings.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => (item.Key, item.Value)))) eligibility[id] = false;
                }
            }
            if (order.Quote.Kind == ConstructionKind.District && order.Quote.DefinitionId != "district_city")
            {
                var capacity = M3DistrictCapacity.Evaluate(source, startup, state, order.Quote.DefinitionId, evaluation?.Context ?? Context(state), evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup));
                var installed = state.Installed.Values.Count(item => item.Kind == ConstructionKind.District && capacity.Members.Contains(item.DefinitionId));
                var ahead = state.PendingOrderIds.TakeWhile(other => other != id).Count(other => eligibility[other] != false
                    && state.Orders[other].Quote.Kind == ConstructionKind.District && capacity.Members.Contains(state.Orders[other].Quote.DefinitionId));
                if (capacity.Gate is not null && eligibility[id] != false) eligibility[id] = null;
                else if (capacity.Capacity is { } cap && installed + ahead >= cap) eligibility[id] = false;
            }
            if (modifiers.Speed is <= -1) eligibility[id] = false;
        }
        return (speeds, eligibility);
    }

    public static M3ConstructionRefundView RefundPreview(ConstructionLiveRuntime state, ConstructionOrder order, ConstructionBankSnapshot bank)
    {
        var amounts = bank.Refund(order).ToDictionary(delta => delta.ResourceId, delta => delta.Amount, StringComparer.Ordinal);
        var losses = order.Quote.Costs.Where(pair => pair.Value > amounts.GetValueOrDefault(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value - amounts.GetValueOrDefault(pair.Key), StringComparer.Ordinal);
        var inputs = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Policy = "manual-refund-preview-v1", state.ColonyId, state.Revision, order.Id, order.PayerId,
            Paid = order.Quote.Costs.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            PayerAbsent = bank.AbsentPayerIds.Contains(order.PayerId),
            Stocks = bank.AbsentPayerIds.Contains(order.PayerId) ? null : bank.Accounts[order.PayerId].OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            Storage = bank.Storage.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            PayerStorage = bank.PayerStorage.GetValueOrDefault(order.PayerId)?.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
        });
        return new("actual-paid-capped-or-unlimited", amounts, Convert.ToHexString(SHA256.HashData(inputs)).ToLowerInvariant(), losses);
    }

    public M3DevelopmentColonyView Project(ConstructionLiveRuntime state, int? economyPlanetId, M3ConstructionEvaluation? evaluation = null, ConstructionBankSnapshot? bank = null, int activeCapacity = 1)
    {
        static string Kind(ConstructionKind kind) => kind switch { ConstructionKind.Building => "building", ConstructionKind.District => "district", _ => "zone" };
        var slots = state.Slots.Values.Select(slot =>
        {
            var status = slot.Kind == ConstructionKind.Zone ? M3ZoneGraph.Eligibility(source, slot, evaluation?.Context ?? Context(state)) : (Gate: (string?)null, Requirements: (IReadOnlyList<string>)[]);
            return new M3DevelopmentSlotView(slot.Id, Kind(slot.Kind), state.Orders.Values.FirstOrDefault(order => order.SlotId == slot.Id && order.Status == ConstructionOrderStatus.Pending)?.Id,
                state.Installed.GetValueOrDefault(slot.Id)?.DefinitionId, status.Gate is null ? [] : [new("dependency-unresolved", status.Gate)], slot.ZoneInstanceId,
                slot.DistrictInstanceId, slot.ZoneSlotDefinitionId, status.Requirements);
        }).ToArray();
        var catalogContext = evaluation?.Context ?? Context(state);
        var catalog = source.Content.Catalogs.Keys.Order(StringComparer.Ordinal)
            .Where(id => ConstructionCatalogAssessor.Visibility(source.Content.Catalogs[id], id, catalogContext, source.Content.Conditions).Truth != ConstructionTruth.False)
            .Select(id =>
        {
            var kind = source.Content.Catalogs[id].Definitions[id].Kind;
            var condition = ConstructionCatalogAssessor.Eligibility(source.Content.Catalogs[id], id, catalogContext, source.Content.Conditions);
            var compatible = state.Slots.Values.Where(slot => kind == Kind(slot.Kind)
                && (kind != "building" || slot.ZoneDefinitionId is { } zone && M3ZoneGraph.BuildingFits(source, id, zone))
                && (kind != "zone" || slot.ZoneSlotDefinitionId is { } zoneSlot && M3ZoneGraph.Fits(source, id, zoneSlot))).ToArray();
            var candidates = compatible.Where(slot => condition.Truth == ConstructionTruth.True && !state.Installed.ContainsKey(slot.Id)
                && !state.Orders.Values.Any(order => order.SlotId == slot.Id && order.Status == ConstructionOrderStatus.Pending))
                .GroupBy(slot => (slot.ZoneDefinitionId, slot.ZoneSlotDefinitionId, slot.DistrictInstanceId, Installed: state.Installed.GetValueOrDefault(slot.Id)?.DefinitionId))
                .Select(group => (Slots: group.ToArray(), Result: Quote(state, id, group.First().Id, evaluation))).ToArray();
            // Quote общий только для совместимых мест одной зоны с одинаковым исходным зданием.
            var available = candidates.Where(candidate => candidate.Result.Quote is not null).ToArray();
            var quote = available.FirstOrDefault().Result?.Quote;
            var diagnostic = condition.Truth == ConstructionTruth.Unknown ? "eligibility-unresolved"
                : condition.Truth == ConstructionTruth.False ? "not-eligible"
                : source.EvaluateRuntimeGate(id, catalogContext) ?? candidates.FirstOrDefault().Result?.Code ?? "no-free-compatible-slot";
            var recipe = CatalogRecipe(id);
            var localization = PlanetLocalizationBindings.Get(kind, id);
            return new M3ConstructionCatalogView(id, kind, null, quote is null ? "unavailable" : "available", Array.AsReadOnly(available.SelectMany(candidate => candidate.Slots.Select(slot => slot.Id)).ToArray()),
                quote is null ? null : new(quote.PolicyId, quote.Costs, Duration(quote, evaluation), quote.InfrastructureUpkeep, quote.JobCapacity),
                quote is null ? [new("dependency-unresolved", diagnostic)] : [],
                CatalogTier(id), CatalogValues(id, "prerequisites"), CatalogValues(id, "upgrades"),
                source.UpgradePredecessors.GetValueOrDefault(id, []), Array.AsReadOnly(compatible.Select(slot => slot.Id).ToArray()),
                recipe is null ? null : SourceRecipe(recipe), quote is null || bank is null ? null : bank.Accounts.TryGetValue(state.OwnerId, out var account)
                    && quote.Costs.All(pair => account.TryGetValue(pair.Key, out var balance) && balance >= pair.Value),
                source.UpgradePredecessors.ContainsKey(id) ? "installed-source-required" : null,
                recipe?.LocalizationKey ?? localization.NameKey ?? CatalogValues(id, "name").FirstOrDefault() ?? id,
                recipe?.DescriptionKey ?? localization.DescriptionKey ?? id + "_desc");
        }).ToArray();
        var installed = state.Installed.Values.Select(item =>
        {
            var level = item.Kind == ConstructionKind.Zone ? M3ZoneGraph.AttachedLevel(startup, state, state.Slots[item.SlotId].DistrictInstanceId!) : 1;
            var facts = evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup);
            return new M3ConstructionInstalledView(item.SlotId, item.DefinitionId, Kind(item.Kind), null,
                !item.Enabled ? new Dictionary<string, decimal>() : item.Kind == ConstructionKind.Zone ? M3ZoneGraph.Contributions(source, item.DefinitionId, level, PlanetEconomyTermKind.ZoneUpkeep, facts) : item.InfrastructureUpkeep,
                !item.Enabled ? new Dictionary<string, decimal>() : item.Kind == ConstructionKind.Zone ? M3ZoneGraph.Contributions(source, item.DefinitionId, level, PlanetEconomyTermKind.WorkforceCapacity, facts) : item.JobCapacity,
                item.Kind == ConstructionKind.Building ? UpgradeOptions(item) : [], Recipe(item.DefinitionId).LocalizationKey, Recipe(item.DefinitionId).DescriptionKey,
                item.Kind == ConstructionKind.Building ? item.Enabled : null,
                item.Kind != ConstructionKind.Building ? null : new(ToggleGate(state, item) is null, !item.Enabled,
                    ToggleGate(state, item) is { } gate ? [new("dependency-unresolved", gate)] : []),
                item.Kind != ConstructionKind.Building ? null : new(DemolitionGate(state, item) is null,
                    DemolitionGate(state, item) is { } demolitionGate ? [new("dependency-unresolved", demolitionGate)] : []),
                ZoneReplacementOptions(state, item, bank, evaluation));
        }).ToArray();
        var positions = state.PendingOrderIds.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
        var queue = state.Orders.Values.OrderBy(order => positions.GetValueOrDefault(order.Id, int.MaxValue)).ThenBy(order => order.Id, StringComparer.Ordinal)
            .Select(order =>
            {
                var executionState = order.Status != ConstructionOrderStatus.Pending || activeCapacity is < 0 or > 4096 ? null
                    : activeCapacity == 0 ? "paused" : positions[order.Id] < activeCapacity ? "active" : "waiting";
                decimal? dailyWork = null, remainingWorkDays = null;
                if (order.Status == ConstructionOrderStatus.Pending && Modifiers(order.Quote.Kind, evaluation).Speed is { } speed && speed > -1)
                {
                    try
                    {
                        dailyWork = checked(1 + speed);
                        if (executionState is "active" or "waiting") remainingWorkDays = decimal.Ceiling(checked((order.Quote.RequiredWork - order.CompletedWork) / dailyWork.Value));
                    }
                    catch (OverflowException) { dailyWork = null; remainingWorkDays = null; }
                }
                M3ConstructionRefundView? refund = null;
                M3DevelopmentGate[] gates = [];
                if (order.Status == ConstructionOrderStatus.Pending && bank is not null)
                {
                    try { refund = RefundPreview(state, order, bank); }
                    catch (ResourceStorageUnresolvedException exception) { gates = [new("dependency-unresolved", "resource-storage-unresolved:" + exception.ResourceId)]; }
                }
                return new M3ConstructionOrderView(order.Id, order.SlotId, order.Quote.DefinitionId, Kind(order.Quote.Kind), order.Status.ToString().ToLowerInvariant(), order.Quote.Costs, order.CompletedWork, order.Quote.RequiredWork, "baseDays",
                    refund, gates, order.UpgradeFromDefinitionId, order.UpgradeZoneInstanceId, Recipe(order.Quote.DefinitionId).LocalizationKey, Recipe(order.Quote.DefinitionId).DescriptionKey,
                    order.CancellationReason switch
                    {
                        ConstructionCancellationReason.Player => "player",
                        ConstructionCancellationReason.ConditionLost => "condition-lost",
                        ConstructionCancellationReason.OwnershipChanged => "ownership-changed",
                        ConstructionCancellationReason.Destroyed => "destroyed",
                        ConstructionCancellationReason.NonPositiveSpeed => "non-positive-speed",
                        ConstructionCancellationReason.PayerMissing => "payer-missing",
                        ConstructionCancellationReason.CalculationFailed => "calculation-failed",
                        _ => null
                    }, order.ZoneReplacement is { } replacing ? new(replacing.SourceDefinitionId, replacing.ZoneInstanceId, RemovedBuildingViews(replacing.RemovedBuildings)) : null,
                    executionState, executionState is null ? null : "accepted-daily-work-v1", dailyWork, remainingWorkDays,
                    order.BuildingReplacement is { } buildingReplacement ? new(buildingReplacement.SourceDefinitionId, buildingReplacement.ZoneInstanceId) : null);
            }).ToArray();
        var features = startup.Features.Where(feature => !feature.IsHidden).Select(feature => new M3DevelopmentFeatureView(feature.InstanceId, feature.DefinitionId, null, feature.IsBlocker, null, feature.IsBlocker ? [new("dependency-unresolved", "clearance-lifecycle-unresolved")] : [],
            LocalizationKey: PlanetLocalizationBindings.Get("feature", feature.DefinitionId).NameKey, DescriptionKey: PlanetLocalizationBindings.Get("feature", feature.DefinitionId).DescriptionKey)).ToArray();
        var districts = state.Installed.Values.Where(item => item.Kind == ConstructionKind.District).GroupBy(item => item.DefinitionId).Select(group => new M3StartupDistrictView(state.ColonyId + "/district/" + group.Key, group.Key, group.Count(), Recipe(group.Key).LocalizationKey, Recipe(group.Key).DescriptionKey)).ToArray();
        return new(state.ColonyId, economyPlanetId, state.OwnerId, state.Revision, "individualist", false, null, null, Array.AsReadOnly(slots), Array.AsReadOnly(catalog), Array.AsReadOnly(installed), Array.AsReadOnly(queue), Array.AsReadOnly(features), [], [], startup.Homeworld.InitializerId, startup.Homeworld.PlanetClass, startup.Plan.FinalPlanetSize,
            Array.AsReadOnly(districts), Array.AsReadOnly(startup.Buildings.Where(item => state.StartupBuildingBySlot.Any(binding => binding.Value == item.InstanceId && state.Installed.ContainsKey(binding.Key))).Select(item =>
            {
                var binding = state.StartupBuildingBySlot.Single(pair => pair.Value == item.InstanceId);
                var zone = state.Slots[binding.Key];
                var installedId = state.Installed[binding.Key].DefinitionId;
                return new M3StartupBuildingView(item.InstanceId, installedId, binding.Key, zone.ZoneDefinitionId, item.Order, null, Array.AsReadOnly(new[] { zone.ZoneInstanceId! }), Recipe(installedId).LocalizationKey, Recipe(installedId).DescriptionKey);
            }).ToArray()), startup.Homeworld.PlanetSize,
            Array.AsReadOnly(M3ZoneGraph.Current(startup, state).Select(item => new M3StartupZoneView(item.InstanceId, item.DistrictInstanceId, item.SlotInstanceId, item.ZoneId, item.SlotNumber,
                PlanetLocalizationBindings.Get("zone", item.ZoneId).NameKey, PlanetLocalizationBindings.Get("zone", item.ZoneId).DescriptionKey)).ToArray()),
            Array.AsReadOnly(startup.Features.Select(item => new M3StartupFeatureView(item.InstanceId, item.DefinitionId, item.IsBlocker, item.IsHidden, item.HiddenByBlockerInstanceId, item.HiddenFeatureInstanceId, item.Order,
                PlanetLocalizationBindings.Get("feature", item.DefinitionId).NameKey, PlanetLocalizationBindings.Get("feature", item.DefinitionId).DescriptionKey)).ToArray()),
            Array.AsReadOnly(startup.Plan.DistrictCaps.Select(item => new M3StartupDistrictCapView(item.ModifierId, item.Value)).ToArray()),
            QueuePolicy: activeCapacity == 1 ? new("ordinary-sequential-v1", 1, true) : null,
            DistrictPool: DistrictPool(), DistrictTypePools: DistrictTypePools());

        IReadOnlyList<M3ConstructionDistrictTypePoolView> DistrictTypePools()
        {
            var pools = new List<M3ConstructionDistrictTypePoolView>();
            foreach (var id in new[] { "district_generator", "district_mining", "district_farming" })
            {
                var capacity = M3DistrictCapacity.Evaluate(source, startup, state, id, evaluation?.Context ?? Context(state), evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup));
                if (capacity.Gate is not null) continue;
                var installed = state.Installed.Values.Count(item => item.Kind == ConstructionKind.District && capacity.Members.Contains(item.DefinitionId));
                var reserved = state.Orders.Values.Count(item => item.Status == ConstructionOrderStatus.Pending && item.Quote.Kind == ConstructionKind.District && capacity.Members.Contains(item.Quote.DefinitionId));
                pools.Add(new("ordinary-source-district-type-pool-v1", id, capacity.SharedPoolId!, capacity.Uncapped!.Value, capacity.Capacity,
                    installed, reserved, capacity.Capacity is { } cap ? Math.Max(0, cap - installed - reserved) : null, capacity.Members));
            }
            return pools.AsReadOnly();
        }

        M3ConstructionDistrictPoolView DistrictPool()
        {
            var capacity = state.Slots.Values.Count(slot => slot.Kind == ConstructionKind.District);
            var installedCount = state.Installed.Values.Count(item => item.Kind == ConstructionKind.District);
            var reserved = state.Orders.Values.Count(order => order.Status == ConstructionOrderStatus.Pending && order.Quote.Kind == ConstructionKind.District);
            return new("ordinary-district-slot-pool-v1", capacity, installedCount, reserved, capacity - installedCount - reserved);
        }

        IReadOnlyList<M3ConstructionUpgradeView> UpgradeOptions(ConstructionInstalled item)
        {
            var zone = state.Slots[item.SlotId].ZoneInstanceId!;
            return Array.AsReadOnly(Values(Recipe(item.DefinitionId), "upgrades").Select(target =>
            {
                var assessed = QuoteUpgrade(state, target, item.DefinitionId, item.SlotId, zone, evaluation);
                var quote = assessed.Quote;
                bool? affordable = quote is null || bank is null ? null : bank.Accounts.TryGetValue(state.OwnerId, out var account)
                    && quote.Costs.All(pair => account.TryGetValue(pair.Key, out var balance) && balance >= pair.Value && bank.TryGetStorage(state.OwnerId, pair.Key, out _));
                var queueCompatible = state.PendingOrderIds.All(id => state.Orders[id].Quote.Kind == ConstructionKind.Building);
                var reason = quote is null ? assessed.Code : !queueCompatible ? "queue-kind-sharing-unresolved" : affordable == false ? "insufficient-resources" : affordable is null ? "bank-unavailable" : null;
                return new M3ConstructionUpgradeView(item.DefinitionId, target, item.SlotId, zone, state.Revision,
                    quote is null ? null : new(quote.PolicyId, quote.Costs, Duration(quote, evaluation), quote.InfrastructureUpkeep, quote.JobCapacity),
                    quote is null ? "unavailable" : "available", affordable, quote is not null && queueCompatible && affordable == true,
                    quote is not null && queueCompatible ? "enqueueUpgrade" : null, reason is null ? [] : [new("dependency-unresolved", reason)],
                    LocalizationKey: source.Content.Economy.Buildings.GetValueOrDefault(target)?.LocalizationKey,
                    DescriptionKey: source.Content.Economy.Buildings.GetValueOrDefault(target)?.DescriptionKey);
            }).ToArray());
        }
    }

    private PlanetEconomyDefinition? CatalogRecipe(string id) => source.Content.Economy.Buildings.GetValueOrDefault(id)
        ?? source.Content.Economy.Districts.GetValueOrDefault(id) ?? source.Content.Economy.Zones.GetValueOrDefault(id);
    private IReadOnlyList<string> CatalogValues(string id, string key) => Array.AsReadOnly(source.Content.Catalogs[id].Definitions[id].Fields
        .Where(field => field.Key == key).SelectMany(field => field.Scalar is { } scalar ? new[] { scalar }
            : field.Children.Select(child => child.Scalar ?? child.Key)).ToArray());
    private decimal? CatalogTier(string id) => source.Content.Catalogs[id].Parameters
        .Where(parameter => parameter.DefinitionId == id && parameter.FieldPath.Count(character => character == '/') == 1
            && parameter.FieldPath.EndsWith(":tier", StringComparison.Ordinal)).Select(parameter => (decimal?)parameter.Value).SingleOrDefault();
    private PlanetEconomyDefinition Recipe(string id) => source.Content.Economy.Buildings.TryGetValue(id, out var building)
        ? building : source.Content.Economy.Districts.TryGetValue(id, out var district) ? district : source.Content.Economy.Zones[id];
    private static IReadOnlyList<string> Values(PlanetEconomyDefinition definition, string key) => definition.SourceProperties.GetValueOrDefault(key, []);
    private static decimal? SourceTier(PlanetEconomyDefinition definition) => Values(definition, "tier") is [var value]
        && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var tier) ? tier : null;
    private static IReadOnlyList<M3ConstructionSourceTermView> SourceRecipe(PlanetEconomyDefinition definition) => Array.AsReadOnly(definition.Terms.Select(term =>
        new M3ConstructionSourceTermView(term.Kind.ToString(), term.TargetId, term.Value, term.SourceExpression, term.ConditionDisposition.ToString(), term.ApplicationScope.ToString())).ToArray());
}
