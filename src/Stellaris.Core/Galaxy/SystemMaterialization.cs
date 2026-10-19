using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

public enum SystemMaterializationStatus { TargetReached, InputExhausted, RetryBudgetExhausted,
    InvalidInitialState, UnsupportedInitialState, FatalResolverInvariant }
public enum SystemMaterializationFailureKind { InvalidSnapshot, NonFreshStateUnsupported, AllocationStopped,
    CapacityGrowthUnsupported, ChunkStorageUnavailable, UnsafeResolverBinding, PostAllocationResolverInvariant }
public enum SystemFailureStage { PreflightBeforeAllocation, AfterSlotRegistration }
public sealed record SystemMaterializationFailure(SystemMaterializationFailureKind Kind, SystemFailureStage Stage,
    string Detail, bool IdConsumed, NativeSystemHandle? ConsumedHandle);

public sealed class SystemMaterializationResult
{
    public IReadOnlyList<NativeSystemObject> Systems { get; }
    public IReadOnlyDictionary<NativeSystemHandle, NativeSystemObject> Registry { get; }
    public SystemAllocatorSnapshot Allocator { get; }
    public SystemMaterializationStatus Status { get; }
    public SystemMaterializationFailure? Failure { get; }
    public long EvaluatedCandidates { get; }
    public long RejectedCandidates { get; }
    public int RemainingRetryBudget { get; }
    public bool NativeSpatialRefreshPending => Systems.Count != 0 || Failure?.IdConsumed == true;

    internal SystemMaterializationResult(IEnumerable<NativeSystemObject> systems, SystemAllocatorSnapshot allocator,
        SystemMaterializationStatus status, SystemMaterializationFailure? failure, long evaluated, long rejected, int budget)
    {
        var snapshot = systems.ToArray();
        Systems = Array.AsReadOnly(snapshot);
        Registry = new ReadOnlyDictionary<NativeSystemHandle, NativeSystemObject>(snapshot.ToDictionary(s => s.Handle));
        Allocator = allocator; Status = status; Failure = failure;
        EvaluatedCandidates = evaluated; RejectedCandidates = rejected; RemainingRetryBudget = budget;
    }
}

public sealed record SeededSystemMaterialization(SystemMaterializationResult Result, NativeRandomState NextState, long? Rotation);

/// <summary>Изолированный fresh-system success path. Нет native memory access, free/grow, sentinel mutation или spatial rebuild.</summary>
public static class SystemMaterialization
{
    public static SystemMaterializationResult Replay(ReadOnlySpan<PreRelaxationCandidate> candidates, int targetStars,
        long minimumDistance, SystemAllocatorSnapshot allocator, IReadOnlyList<SystemCreationInput> creationInputs)
    {
        var snapshot = candidates.ToArray();
        var index = 0;
        return Run(_ => index < snapshot.Length ? snapshot[index++] : null, targetStars, minimumDistance, allocator, creationInputs);
    }

    public static SeededSystemMaterialization Generate(GalaxySetupPlan plan, int specifiedSeed,
        SystemAllocatorSnapshot allocator, IReadOnlyList<SystemCreationInput> creationInputs)
    {
        var shape = GalaxyShapeProjection.Read(plan);
        var state = NativeRandom.FromSpecifiedSeed(specifiedSeed);
        long? rotation = null;
        var result = Run(index =>
        {
            if (rotation is null)
            {
                var initialized = PreRelaxationCandidates.InitializeRotation(shape, state);
                rotation = initialized.Angle;
                state = initialized.NextState;
            }
            var candidate = PreRelaxationCandidates.Next(shape, index, rotation.Value, state);
            state = candidate.NextState;
            return candidate.Candidate;
        }, plan.TargetStars, shape.MinimumDistance, allocator, creationInputs);
        return new(result, state, rotation);
    }

    private static SystemMaterializationResult Run(Func<int, PreRelaxationCandidate?> next, int targetStars,
        long minimumDistance, SystemAllocatorSnapshot initial, IReadOnlyList<SystemCreationInput> creationInputs)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(creationInputs);
        if (targetStars < 0) throw new ArgumentOutOfRangeException(nameof(targetStars));
        if (minimumDistance < 0) throw new ArgumentOutOfRangeException(nameof(minimumDistance));
        var inputs = creationInputs.ToArray();
        if (inputs.Length != targetStars || inputs.Any(input => input.NameInput is null))
            throw new ArgumentException("Нужен explicit name/context payload для каждого target ordinal.", nameof(creationInputs));
        if (Preflight(initial, targetStars) is { } failure)
            return new(Array.Empty<NativeSystemObject>(), initial,
                failure.Kind == SystemMaterializationFailureKind.InvalidSnapshot
                    ? SystemMaterializationStatus.InvalidInitialState : SystemMaterializationStatus.UnsupportedInitialState,
                failure, 0, 0, NativePlacementRules.RetryBudget);

        var slots = initial.Slots.ToArray();
        var freeHead = initial.FreeHead;
        var highest = initial.HighestActive;
        var active = initial.ActiveCount;
        var managerDirty = initial.ManagerDirty;
        var worldDirty = initial.WorldDirty;
        var lastHandle = initial.LastReturnedHandle;
        var bySlot = new Dictionary<uint, NativeSystemObject>();
        var systems = new List<NativeSystemObject>();
        var stored = new List<NativeFixedPoint>();
        long evaluated = 0, rejected = 0;
        var budget = NativePlacementRules.RetryBudget;
        SystemMaterializationResult Finish(SystemMaterializationStatus status, SystemMaterializationFailure? error = null) =>
            new(systems, new SystemAllocatorSnapshot(initial.Capacity, freeHead, highest, active,
                initial.Stop34, initial.Stop35, initial.PreparedChunkCount, initial.Resolver, managerDirty,
                worldDirty, lastHandle, slots), status, error, evaluated, rejected, budget);

        while (systems.Count < targetStars)
        {
            var supplied = next(systems.Count);
            if (supplied is not { } candidate) return Finish(SystemMaterializationStatus.InputExhausted);
            evaluated++;
            if (NativePlacementRules.HasCollision(new(candidate.X, candidate.Y), CollectionsMarshal.AsSpan(stored), minimumDistance))
            {
                rejected++;
                if (budget < 1) return Finish(SystemMaterializationStatus.RetryBudgetExhausted);
                budget--;
                continue;
            }

            var slotIndex = freeHead;
            var slot = slots[(int)slotIndex];
            var oldWord = slot.ObjectHandleWord!.Value; // Выбранный storage проверен до первого draw.
            var rawObject = new NativeSystemObject(oldWord, NativeSystemHandles.Locate(slotIndex));
            var handle = NativeSystemHandles.ComposeAllocated(slotIndex, (byte)(oldWord >> 24));

            // Slot registration/count/highest предшествуют native constructor и записи handle.
            freeHead = slot.NextFree;
            if (freeHead != NativeSystemHandles.ReservedSlot)
                slots[(int)freeHead] = slots[(int)freeHead] with { PreviousFree = NativeSystemHandles.ReservedSlot };
            slots[(int)slotIndex] = slot with { IsRegistered = true, NextFree = NativeSystemHandles.ReservedSlot };
            bySlot.Add(slotIndex, rawObject);
            active++;
            managerDirty = true;
            highest = NativeSystemHandles.HighestAfterAllocation(highest, slotIndex);
            rawObject.InitializeConstructor();
            rawObject.AssignHandle(handle);
            slots[(int)slotIndex] = slots[(int)slotIndex] with { ObjectHandleWord = handle.Value };

            // Safe success-domain global resolver связан с той же owned table; full-handle check обязателен.
            if (!bySlot.TryGetValue(handle.Slot, out var resolved) || !ReferenceEquals(resolved, rawObject) || resolved.Handle != handle)
                return Finish(SystemMaterializationStatus.FatalResolverInvariant,
                    new(SystemMaterializationFailureKind.PostAllocationResolverInvariant, SystemFailureStage.AfterSlotRegistration,
                        "ID уже consumed; sentinel continuation не исполняется.", true, handle));
            resolved.MarkResolved();
            resolved.CopyCreationInput(inputs[systems.Count]);
            worldDirty = true;
            lastHandle = resolved.Handle;
            resolved.AssignPosition(candidate);
            systems.Add(resolved);
            resolved.FreezeAfterAppend();
            stored.Add(resolved.Position);
            budget = NativePlacementRules.RetryBudget;
        }
        return Finish(SystemMaterializationStatus.TargetReached);
    }

    private static SystemMaterializationFailure? Preflight(SystemAllocatorSnapshot snapshot, int target)
    {
        SystemMaterializationFailure Gate(SystemMaterializationFailureKind kind, string detail) =>
            new(kind, SystemFailureStage.PreflightBeforeAllocation, detail, false, null);
        if (snapshot.Capacity < 1 || (uint)snapshot.Capacity > NativeSystemHandles.MaximumCapacity ||
            snapshot.Slots.Count != snapshot.Capacity || snapshot.PreparedChunkCount < 0 ||
            snapshot.ActiveCount < 0 || snapshot.ActiveCount > snapshot.Capacity || !Enum.IsDefined(snapshot.Resolver))
            return Gate(SystemMaterializationFailureKind.InvalidSnapshot, "Capacity/records/count/resolver snapshot invalid.");
        if (snapshot.ActiveCount != 0 || snapshot.HighestActive != uint.MaxValue || snapshot.Slots.Any(s => s.IsRegistered))
            return Gate(SystemMaterializationFailureKind.NonFreshStateUnsupported, "Поддержан только явно fresh empty manager.");

        var seen = new bool[snapshot.Capacity];
        var freeOrder = new List<uint>(snapshot.Capacity);
        var current = snapshot.FreeHead;
        var previous = NativeSystemHandles.ReservedSlot;
        while (current != NativeSystemHandles.ReservedSlot)
        {
            if (current >= snapshot.Capacity || seen[(int)current] || snapshot.Slots[(int)current].PreviousFree != previous)
                return Gate(SystemMaterializationFailureKind.InvalidSnapshot, "Out-of-range/cyclic/inconsistent free chain.");
            seen[(int)current] = true;
            freeOrder.Add(current);
            previous = current;
            current = snapshot.Slots[(int)current].NextFree;
        }
        if (freeOrder.Count != snapshot.Capacity)
            return Gate(SystemMaterializationFailureKind.InvalidSnapshot, "Fresh free chain не покрывает все records.");
        if (snapshot.Slots.Any(s => s.ObjectHandleWord is { } word && word != uint.MaxValue))
            return Gate(SystemMaterializationFailureKind.NonFreshStateUnsupported, "Reuse storage не поддержан этим materializer.");
        if (snapshot.Stop34 != 0 || snapshot.Stop35 != 0)
            return Gate(SystemMaterializationFailureKind.AllocationStopped, "Stop flags не допускают safe allocation.");
        if (snapshot.Resolver != SystemResolverBinding.SameManager)
            return Gate(SystemMaterializationFailureKind.UnsafeResolverBinding, "Native postallocation sentinel path запрещён preflight'ом.");
        if (target > freeOrder.Count)
            return Gate(SystemMaterializationFailureKind.CapacityGrowthUnsupported, "Требуется native grow/storage path, который здесь не исполняется.");
        for (var i = 0; i < target; i++)
        {
            var slot = freeOrder[i];
            if ((slot >> 10) >= snapshot.PreparedChunkCount || snapshot.Slots[(int)slot].ObjectHandleWord is null)
                return Gate(SystemMaterializationFailureKind.ChunkStorageUnavailable, "Нужный chunk не подготовлен; ID не выдан.");
        }
        return null;
    }
}
