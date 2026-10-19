namespace Stellaris.Core.Galaxy;

public enum SystemResolverBinding { SameManager, MissingManager, DifferentManager }

/// <summary>Free links имеют смысл только для не зарегистрированного slot. Null word означает отсутствие storage.</summary>
public readonly record struct SystemSlotSnapshot(uint NextFree, uint PreviousFree, uint? ObjectHandleWord, bool IsRegistered);

/// <summary>Явный immutable input/output allocator projection, без native pointers или memory access.</summary>
public sealed class SystemAllocatorSnapshot
{
    public int Capacity { get; }
    public uint FreeHead { get; }
    public uint HighestActive { get; }
    public int ActiveCount { get; }
    public byte Stop34 { get; }
    public byte Stop35 { get; }
    public int PreparedChunkCount { get; }
    public SystemResolverBinding Resolver { get; }
    public bool ManagerDirty { get; }
    public bool WorldDirty { get; }
    public NativeSystemHandle LastReturnedHandle { get; }
    public IReadOnlyList<SystemSlotSnapshot> Slots { get; }

    public SystemAllocatorSnapshot(int capacity, uint freeHead, uint highestActive, int activeCount,
        byte stop34, byte stop35, int preparedChunkCount, SystemResolverBinding resolver,
        bool managerDirty, bool worldDirty, NativeSystemHandle lastReturnedHandle, IEnumerable<SystemSlotSnapshot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        Capacity = capacity; FreeHead = freeHead; HighestActive = highestActive; ActiveCount = activeCount;
        Stop34 = stop34; Stop35 = stop35; PreparedChunkCount = preparedChunkCount; Resolver = resolver;
        ManagerDirty = managerDirty; WorldDirty = worldDirty; LastReturnedHandle = lastReturnedHandle;
        Slots = Array.AsReadOnly(slots.ToArray());
    }

    /// <summary>Caller явно выбирает fresh empty manager; это не предположение о состоянии существующей партии.</summary>
    public static SystemAllocatorSnapshot CreateFresh(int capacity, int preparedChunkCount, byte stop34, byte stop35,
        SystemResolverBinding resolver, bool managerDirty, bool worldDirty, NativeSystemHandle lastReturnedHandle)
    {
        if (capacity < 1 || (uint)capacity > NativeSystemHandles.MaximumCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (preparedChunkCount < 0) throw new ArgumentOutOfRangeException(nameof(preparedChunkCount));
        var slots = new SystemSlotSnapshot[capacity];
        for (var i = 0; i < capacity; i++)
            slots[i] = new(i + 1 < capacity ? (uint)(i + 1) : NativeSystemHandles.ReservedSlot,
                i == 0 ? NativeSystemHandles.ReservedSlot : (uint)(i - 1),
                ((uint)i >> 10) < preparedChunkCount ? uint.MaxValue : null, false);
        return new(capacity, 0, uint.MaxValue, 0, stop34, stop35, preparedChunkCount, resolver,
            managerDirty, worldDirty, lastReturnedHandle, slots);
    }
}
