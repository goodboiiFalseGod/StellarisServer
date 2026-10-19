namespace Stellaris.Core.Construction;

public sealed record ConstructionLiveTransactionResult(bool Accepted, string Code, ConstructionLiveRuntime? Runtime, ConstructionBankSnapshot? Bank);

/// <summary>Проверяет все typed deltas на отдельном bank candidate. Публикацию и monthly settlement выполняет session owner.</summary>
public static class ConstructionLiveTransaction
{
    public static ConstructionLiveTransactionResult Stage(ConstructionLiveResult transition, ConstructionBankSnapshot bank)
    {
        if (!transition.Accepted) { return new(false, transition.Code, null, null); }
        try
        {
            if (transition.ResourceDeltas.Count > 65536) { return new(false, "capacity-limit", null, null); }
            var accounts = bank.Accounts.ToDictionary(pair => pair.Key, pair => pair.Value.ToDictionary(StringComparer.Ordinal));
            foreach (var group in transition.ResourceDeltas.GroupBy(delta => (delta.PayerId, delta.ResourceId)))
            {
                if (!accounts.TryGetValue(group.Key.PayerId, out var account) || !account.TryGetValue(group.Key.ResourceId, out var balance) || !bank.TryGetStorage(group.Key.PayerId, group.Key.ResourceId, out var storage)) { return new(false, "bank-context-unresolved", null, null); }
                var resulting = checked(balance + group.Sum(delta => delta.Amount));
                if (resulting < 0) { return new(false, "insufficient-resources", null, null); }
                if (!storage.Unlimited && resulting > storage.Cap) { return new(false, "storage-cap-exceeded", null, null); }
                account[group.Key.ResourceId] = resulting;
            }
            return new(true, transition.Code, transition.State, new(accounts.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, decimal>)pair.Value), bank.Storage, bank.AbsentPayerIds, bank.PayerStorage));
        }
        catch (OverflowException) { return new(false, "arithmetic-overflow", null, null); }
        catch (ArgumentException) { return new(false, "invalid-bank-candidate", null, null); }
    }
}
