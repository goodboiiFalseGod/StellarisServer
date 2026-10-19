namespace Stellaris.Core.Construction;

public sealed class ConstructionQueue
{
    public string Id { get; }
    public string PolicyId { get; }
    public int AdditionalCapacity { get; }
    public int ActiveCapacity => 1 + AdditionalCapacity;
    public IReadOnlyList<string> OrderIds { get; }

    public ConstructionQueue(string id, string policyId, int additionalCapacity, IEnumerable<string> orderIds)
    {
        Id = ConstructionValues.Id(id);
        PolicyId = ConstructionValues.Id(policyId);
        if (additionalCapacity is < 0 or > 4095) { throw new ArgumentException("Invalid additional queue capacity."); }
        AdditionalCapacity = additionalCapacity;
        ArgumentNullException.ThrowIfNull(orderIds);
        var ids = orderIds.Take(4097).Select(ConstructionValues.Id).ToArray();
        if (ids.Length > 4096 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) { throw new ArgumentException("Invalid queue membership."); }
        OrderIds = Array.AsReadOnly(ids);
    }
}

public sealed class ConstructionQueueSnapshot
{
    public long Revision { get; }
    public IReadOnlyList<ConstructionQueue> Queues { get; }

    public ConstructionQueueSnapshot(long revision, IEnumerable<ConstructionQueue> queues)
    {
        if (revision < 0) { throw new ArgumentException("Invalid queue snapshot revision."); }
        ArgumentNullException.ThrowIfNull(queues);
        var copy = queues.Take(4097).ToArray();
        if (copy.Length > 4096 || copy.Any(queue => queue is null) || copy.Select(queue => queue.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length) { throw new ArgumentException("Invalid queue instances."); }
        var ids = copy.SelectMany(queue => queue.OrderIds).Take(4097).ToArray();
        if (ids.Length > 4096 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) { throw new ArgumentException("Invalid aggregate queue membership."); }
        Revision = revision;
        Queues = Array.AsReadOnly(copy);
    }

    internal HashSet<string> ActiveOrders() => Queues.SelectMany(queue => queue.OrderIds.Take(queue.ActiveCapacity)).ToHashSet(StringComparer.Ordinal);

    internal ConstructionQueueSnapshot RetainPending(long revision, IReadOnlyDictionary<string, ConstructionOrder> orders)
    {
        return new(revision, Queues.Select(queue => new ConstructionQueue(queue.Id, queue.PolicyId, queue.AdditionalCapacity, queue.OrderIds.Where(id => orders[id].Status == ConstructionOrderStatus.Pending))));
    }
}
