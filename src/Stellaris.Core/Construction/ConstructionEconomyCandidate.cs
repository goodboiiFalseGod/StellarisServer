using System.Security.Cryptography;
using System.Text.Json;
using Stellaris.Core.Economy;

namespace Stellaris.Core.Construction;

public enum ConstructionSettlementPhase { CompletionBeforeSettlement, CompletionAfterSettlement }
public sealed record ConstructionEconomyTransition(bool Accepted, string Code, ConstructionLedger Ledger, EconomyWorld Economy);

/// <summary>Stages trusted same-revision inputs; the aggregate owner validates binding and publishes atomically.</summary>
public static class ConstructionEconomyCandidate
{
    public static ConstructionEconomyTransition Stage(EconomyWorld economy, ConstructionLedger ledger, EntityId planet, string commandId, int actorId, long expectedRevision, string? phasePolicyId, ConstructionSettlementPhase phase, bool settleMonth, IEnumerable<ConstructionWork> work)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(ledger);
        if (string.IsNullOrWhiteSpace(phasePolicyId) || phasePolicyId.Length > 160 || phasePolicyId.Any(char.IsControl) || !Enum.IsDefined(phase)) { return new(false, "dependency-unresolved", ledger, economy); }
        try
        {
            var identity = "staging-" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { phasePolicyId, phase, settleMonth, planet })));
            var progress = ledger.Progress(commandId, actorId, expectedRevision, identity, work);
            if (!progress.Accepted) { return new(false, progress.Code, ledger, economy); }
            if (progress.Code == "duplicate") { return new(true, "duplicate", ledger, economy); }
            var candidate = economy.Fork();
            if (settleMonth && phase == ConstructionSettlementPhase.CompletionAfterSettlement) { candidate.AdvanceMonth(); }
            var upkeep = candidate.GetBuildingUpkeep(planet).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var installed in progress.State.Installed.Where(pair => !ledger.Installed.ContainsKey(pair.Key)).Select(pair => pair.Value))
            {
                foreach (var amount in installed.InfrastructureUpkeep)
                {
                    upkeep[amount.Key] = checked(upkeep.GetValueOrDefault(amount.Key) + amount.Value);
                }
            }
            candidate.SetBuildingUpkeep(planet, upkeep);
            if (settleMonth && phase == ConstructionSettlementPhase.CompletionBeforeSettlement) { candidate.AdvanceMonth(); }
            return new(true, "staged", progress.State, candidate);
        }
        catch (OverflowException) { return new(false, "arithmetic-overflow", ledger, economy); }
        catch (ArgumentException) { return new(false, "invalid-economy-context", ledger, economy); }
        catch (ResourceStorageUnresolvedException error) { return new(false, "resource-storage-unresolved:" + error.ResourceId, ledger, economy); }
        catch (InvalidOperationException) { return new(false, "economy-settlement-rejected", ledger, economy); }
    }
}
