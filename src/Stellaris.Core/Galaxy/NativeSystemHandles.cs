namespace Stellaris.Core.Galaxy;

public readonly record struct NativeSystemHandle(uint Value)
{
    public uint Slot => Value & 0x00FFFFFF;
    public byte Generation => (byte)(Value >> 24);
    public bool IsSentinel => Value == uint.MaxValue;
}

public readonly record struct SystemChunkLocation(uint ChunkIndex, uint ByteOffset);

/// <summary>Только system handle/address arithmetic по §16.4. Не выделяет memory/slot и не делает free.</summary>
public static class NativeSystemHandles
{
    public const uint ReservedSlot = 0x00FFFFFF;
    public const uint MaximumCapacity = 0x00FFFFFE;
    public const uint ObjectsPerChunk = 1024;
    public const uint SystemObjectBytes = 0x13B0;

    public static NativeSystemHandle ComposeAllocated(uint slot, byte previousGeneration)
    {
        RequireSlot(slot);
        var generation = unchecked((byte)(previousGeneration + 1));
        return new(((uint)generation << 24) | slot);
    }

    public static SystemChunkLocation Locate(uint slot)
    {
        RequireSlot(slot);
        return new(slot >> 10, checked((slot & 0x3FF) * SystemObjectBytes));
    }

    public static uint HighestAfterAllocation(uint highest, uint slot)
    {
        RequireSlot(slot);
        return highest == uint.MaxValue || slot > highest ? slot : highest;
    }

    /// <summary>Только recovered grow-target arithmetic; не расширяет manager/storage.</summary>
    public static uint GrowthTarget(uint capacity)
    {
        if (capacity < 1 || capacity > MaximumCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
        return Math.Min(unchecked(2 * capacity), MaximumCapacity);
    }

    private static void RequireSlot(uint slot)
    {
        if (slot >= MaximumCapacity) throw new ArgumentOutOfRangeException(nameof(slot), "Slot должен быть меньше exclusive maximum capacity00FFFFFE.");
    }
}
