using System.Security.Cryptography;
using System.Text.Json;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Session.M3.Construction;

public sealed record M3ZoneReplacementPreviewResult(M3ConstructionZoneReplacementView? Preview, string Code);

public sealed partial class M3ConstructionAdapter
{
    private static bool HasZoneReplacement(ConstructionLiveRuntime state, string? zoneInstanceId) => zoneInstanceId is not null
        && state.PendingOrderIds.Any(id => state.Orders[id].ZoneReplacement?.ZoneInstanceId == zoneInstanceId);

    public M3ZoneReplacementPreviewResult PreviewZoneReplacement(ConstructionLiveRuntime state, string targetId, string sourceId,
        string slotId, string zoneInstanceId, ConstructionBankSnapshot? bank = null, M3ConstructionEvaluation? evaluation = null, bool checkReservation = true)
    {
        if (!state.Slots.TryGetValue(slotId, out var slot) || slot.Kind != ConstructionKind.Zone || slot.ZoneInstanceId != zoneInstanceId
            || !state.Installed.TryGetValue(slotId, out var installed) || installed.Kind != ConstructionKind.Zone || installed.DefinitionId != sourceId)
            return new(null, "zone-replacement-source-mismatch");
        // Только подтверждённые городские специализации с одинаковыми тремя building slots.
        if (!IsCityReplacement(sourceId, targetId) || slot.ZoneSlotDefinitionId is not ("slot_city_01" or "slot_city_02")
            || !M3ZoneGraph.Fits(source, targetId, slot.ZoneSlotDefinitionId)) return new(null, "zone-specialization-lifecycle-unresolved");
        var children = state.Slots.Values.Where(child => child.Kind == ConstructionKind.Building && child.ZoneInstanceId == zoneInstanceId)
            .OrderBy(child => child.Id, StringComparer.Ordinal).ToArray();
        if (children.Length != 3 || children.Any(child => child.ZoneDefinitionId != sourceId)) return new(null, "zone-building-slot-binding-unresolved");
        var removed = new Dictionary<string, string>(StringComparer.Ordinal);
        string? gate = null;
        foreach (var child in children)
        {
            if (!state.Installed.TryGetValue(child.Id, out var building)) continue;
            if (!source.Content.Catalogs.TryGetValue(building.DefinitionId, out var catalog)) return new(null, "building-compatibility-unresolved");
            var capital = catalog.Definitions[building.DefinitionId].Fields.Where(field => field.Key == "capital").ToArray();
            if (capital.Any(field => field.Operator != "=" || field.Children.Count != 0 || field.Scalar is not ("yes" or "no")))
                return new(null, "building-capital-condition-unresolved");
            if (capital.Any(field => field.Scalar == "yes")) gate = "zone-capital-building-present";
            var fits = M3ZoneGraph.BuildingCompatibility(source, building.DefinitionId, targetId);
            if (fits is null) return new(null, "building-compatibility-unresolved");
            if (!fits.Value) removed.Add(child.Id, building.DefinitionId);
        }
        if (checkReservation && state.PendingOrderIds.Any(id => state.Orders[id].SlotId == slotId)) gate ??= "slot-unavailable";
        if (state.PendingOrderIds.Any(id => children.Any(child => child.Id == state.Orders[id].SlotId))) gate ??= "zone-building-order-cancel-required";
        var quoteResult = QuoteZoneReplacement(state, targetId, sourceId, slotId, zoneInstanceId, evaluation, checkReservation);
        gate ??= quoteResult.Quote is null ? quoteResult.Code : null;
        var quote = quoteResult.Quote;
        bool? affordable = quote is null || bank is null ? null : bank.Accounts.TryGetValue(state.OwnerId, out var account)
            && quote.Costs.All(pair => account.TryGetValue(pair.Key, out var amount) && amount >= pair.Value && bank.TryGetStorage(state.OwnerId, pair.Key, out _));
        if (state.PendingOrderIds.Any(id => state.Orders[id].Quote.Kind != ConstructionKind.Zone)) gate ??= "queue-kind-sharing-unresolved";
        gate ??= affordable == false ? "insufficient-resources" : null;
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Policy = "city-zone-replacement-preview-v1", state.ColonyId, state.Revision, slotId, sourceId, targetId, zoneInstanceId,
            Source = source.Content.Catalogs[targetId].Files.Concat(source.Content.Catalogs[sourceId].Files)
                .Concat(children.Where(child => state.Installed.ContainsKey(child.Id)).SelectMany(child => source.Content.Catalogs[state.Installed[child.Id].DefinitionId].Files))
                .Select(file => (file.RelativePath, file.Sha256)).Distinct().OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Select(file => new { file.RelativePath, file.Sha256 }),
            Children = children.Select(child => new { child.Id, child.ZoneDefinitionId, Definition = state.Installed.GetValueOrDefault(child.Id)?.DefinitionId })
        }))).ToLowerInvariant();
        return new(new(sourceId, targetId, slotId, zoneInstanceId, state.Revision, fingerprint, RemovedBuildingViews(removed),
            quote is null ? null : new(quote.PolicyId, quote.Costs, Duration(quote, evaluation), quote.InfrastructureUpkeep, quote.JobCapacity),
            affordable, gate is null && affordable == true, gate is null ? [] : [new("dependency-unresolved", gate)],
            Recipe(targetId).LocalizationKey, Recipe(targetId).DescriptionKey), gate ?? "available");
    }

    private IReadOnlyList<M3ConstructionRemovedBuildingView> RemovedBuildingViews(IReadOnlyDictionary<string, string> buildings)
        => Array.AsReadOnly(buildings.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new M3ConstructionRemovedBuildingView(pair.Key, pair.Value, source.Content.Economy.Buildings.GetValueOrDefault(pair.Value)?.LocalizationKey)).ToArray());

    private bool IsCityReplacement(string sourceId, string targetId)
        => sourceId != targetId && source.FixedCityZones.Contains(sourceId) && source.FixedCityZones.Contains(targetId);

    private IReadOnlyList<M3ConstructionZoneReplacementView> ZoneReplacementOptions(ConstructionLiveRuntime state, ConstructionInstalled installed,
        ConstructionBankSnapshot? bank, M3ConstructionEvaluation? evaluation)
        => installed.Kind != ConstructionKind.Zone || !source.FixedCityZones.Contains(installed.DefinitionId) ? []
            : source.FixedCityZones.Where(target => target != installed.DefinitionId)
                .Select(target => PreviewZoneReplacement(state, target, installed.DefinitionId, installed.SlotId,
                    state.Slots[installed.SlotId].ZoneInstanceId!, bank, evaluation).Preview)
                .OfType<M3ConstructionZoneReplacementView>().ToArray();

    private ConstructionLiveQuoteResult QuoteZoneReplacement(ConstructionLiveRuntime state, string targetId, string sourceId, string slotId,
        string zoneInstanceId, M3ConstructionEvaluation? evaluation, bool checkReservation)
    {
        if (!state.Slots.TryGetValue(slotId, out var slot) || slot.Kind != ConstructionKind.Zone || slot.ZoneInstanceId != zoneInstanceId
            || !state.Installed.TryGetValue(slotId, out var installed) || installed.DefinitionId != sourceId)
            return new(null, "zone-replacement-source-mismatch");
        if (!IsCityReplacement(sourceId, targetId) || slot.ZoneSlotDefinitionId is not ("slot_city_01" or "slot_city_02")
            || !M3ZoneGraph.Fits(source, targetId, slot.ZoneSlotDefinitionId)) return new(null, "zone-specialization-lifecycle-unresolved");
        if (checkReservation && state.PendingOrderIds.Any(id => state.Orders[id].SlotId == slotId)) return new(null, "slot-unavailable");
        var eligibility = M3ZoneGraph.Eligibility(source, slot, evaluation?.Context ?? Context(state));
        if (eligibility.Gate is not null) return new(null, eligibility.Gate);
        var level = M3ZoneGraph.AttachedLevel(startup, state, slot.DistrictInstanceId!);
        if (level <= 0) return new(null, "zone-district-unavailable");
        var context = evaluation?.Context ?? Context(state);
        if (source.EvaluateRuntimeGate(targetId, context) is { } dependency) return new(null, dependency);
        var modifiers = Modifiers(ConstructionKind.Zone, evaluation);
        var facts = evaluation?.EconomyFacts ?? M3ConstructionContent.EconomyFacts(startup);
        var result = source.Content.Quote(targetId, context, modifiers.Cost, modifiers.Speed, facts,
            economyConditionEvaluator: source.EconomyConditionEvaluator(targetId, context));
        if (result.Quote is not { } quote) return result;
        return result with { Quote = new(quote.ContentProfileId, "human-zone-replacement-2026-10-13-v1", targetId, ConstructionKind.Zone, quote.RequiredWork,
            quote.Costs, M3ZoneGraph.Contributions(source, targetId, level, PlanetEconomyTermKind.ZoneUpkeep, facts),
            M3ZoneGraph.Contributions(source, targetId, level, PlanetEconomyTermKind.WorkforceCapacity, facts), [], quote.PlanetLimit) };
    }
}
