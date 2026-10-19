namespace Stellaris.Core.Galaxy;

/// <summary>Opaque supplied world-layer payload; имя/контекст здесь не генерируются и не интерпретируются.</summary>
public readonly record struct SystemCreationInput(string NameInput, ulong CreationContext);
public enum NativeSystemObjectPhase { Storage, ConstructorInitialized, HandleAssigned, Resolved,
    CreationInputsCopied, PositionAssigned, Appended }

/// <summary>Реальный managed object только с доказанным native field subset. Поздний content/spatial state отсутствует.</summary>
public sealed class NativeSystemObject
{
    public NativeSystemHandle Handle { get; private set; }
    public NativeSystemObjectPhase Phase { get; private set; }
    public SystemChunkLocation Location { get; }
    public string NameInput { get; private set; } = string.Empty;
    public ulong? CreationContext { get; private set; }
    public NativeFixedPoint? RawPosition { get; private set; }
    public NativeFixedPoint Position { get; private set; }
    public long PositionAuxiliary { get; private set; }
    public uint PositionSentinel { get; private set; }
    public bool Randomized { get; private set; }
    public bool PositionHelperInvoked { get; private set; }
    public int ArmIndex { get; private set; }
    public bool IsFrozen { get; private set; }

    internal NativeSystemObject(uint oldHandleWord, SystemChunkLocation location)
    {
        Handle = new(oldHandleWord);
        Location = location;
    }

    internal void InitializeConstructor()
    {
        RequirePhase(NativeSystemObjectPhase.Storage);
        Handle = new(uint.MaxValue);
        Position = new(0, 0); PositionAuxiliary = 0; PositionSentinel = uint.MaxValue;
        Randomized = false; ArmIndex = -1; NameInput = string.Empty;
        // Неизвестный global default creationContext не выдумывается: позже будет source overwrite.
        CreationContext = null;
        Phase = NativeSystemObjectPhase.ConstructorInitialized;
    }

    internal void AssignHandle(NativeSystemHandle handle)
    {
        RequirePhase(NativeSystemObjectPhase.ConstructorInitialized);
        Handle = handle;
        Phase = NativeSystemObjectPhase.HandleAssigned;
    }

    internal void MarkResolved()
    {
        RequirePhase(NativeSystemObjectPhase.HandleAssigned);
        Phase = NativeSystemObjectPhase.Resolved;
    }

    internal void CopyCreationInput(SystemCreationInput input)
    {
        RequirePhase(NativeSystemObjectPhase.Resolved);
        NameInput = input.NameInput;
        CreationContext = input.CreationContext;
        Phase = NativeSystemObjectPhase.CreationInputsCopied;
    }

    internal void AssignPosition(PreRelaxationCandidate candidate)
    {
        RequirePhase(NativeSystemObjectPhase.CreationInputsCopied);
        RawPosition = new(candidate.X, candidate.Y);
        PositionSentinel = uint.MaxValue;
        Position = RawPosition.Value;
        Position = NativePlacementRules.ClampAccepted(Position);
        // Normal newly constructed descriptor randomized=0: unconditional helper call является no-op.
        PositionHelperInvoked = true;
        if (candidate.Arm >= 0) ArmIndex = candidate.Arm;
        Phase = NativeSystemObjectPhase.PositionAssigned;
    }

    internal void FreezeAfterAppend()
    {
        RequirePhase(NativeSystemObjectPhase.PositionAssigned);
        Phase = NativeSystemObjectPhase.Appended;
        IsFrozen = true;
    }

    private void RequirePhase(NativeSystemObjectPhase expected)
    {
        if (IsFrozen || Phase != expected) throw new InvalidOperationException("Нарушен порядок materialization stages.");
    }
}
