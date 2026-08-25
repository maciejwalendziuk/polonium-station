using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical;

[ByRefEvent]
public readonly record struct TargetDefibrillatedEvent(EntityUid User, Entity<DefibrillatorComponent> Defibrillator);

/// <summary>
/// A health-analyzer readout of whether a dead patient could be shocked back, and if not, why not.
/// Mirrors <see cref="SharedDefibrillatorSystem.GetDefibrillationReadiness"/> - only ever set for a
/// Dead target, since that is the only state a defib revival acts on.
/// </summary>
[Serializable, NetSerializable]
public enum DefibrillationReadiness : byte
{
    /// Not a defib candidate (not dead) - no readout line.
    None,

    /// Rotten or unrevivable: the shock can never bring them back.
    Hopeless,

    /// Even the shock's asphyxiation heal can't drop them below the death threshold.
    TooMuchDamage,

    /// Revivable on damage, but there's no adrenaline reagent in their blood - the shock will
    /// always fail until one is injected.
    NeedsAdrenaline,

    /// Revivable, but they'll come back right at the crit edge and won't be stable.
    Risky,

    /// Revivable - the shock has a real chance (still a roll, never a guarantee).
    Ready,
}

public abstract class BeforeDefibrillatorZapsEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid EntityUsingDefib;
    public readonly EntityUid Defib;
    public EntityUid DefibTarget;

    public BeforeDefibrillatorZapsEvent(EntityUid entityUsingDefib, EntityUid defib, EntityUid defibTarget)
    {
        EntityUsingDefib = entityUsingDefib;
        Defib = defib;
        DefibTarget = defibTarget;
    }
}

/// <summary>
///     This event is raised on the user using the defibrillator before is actually zaps someone.
///     The event is triggered on the user and all their clothing.
/// </summary>
public sealed class SelfBeforeDefibrillatorZapsEvent : BeforeDefibrillatorZapsEvent
{
    public SelfBeforeDefibrillatorZapsEvent(EntityUid entityUsingDefib, EntityUid defib, EntityUid defibtarget) : base(entityUsingDefib, defib, defibtarget) { }
}

/// <summary>
///     This event is raised on the target before it gets zapped with the defibrillator.
///     The event is triggered on the target itself and all its clothing.
/// </summary>
public sealed class TargetBeforeDefibrillatorZapsEvent : BeforeDefibrillatorZapsEvent
{
    public TargetBeforeDefibrillatorZapsEvent(EntityUid entityUsingDefib, EntityUid defib, EntityUid defibtarget) : base(entityUsingDefib, defib, defibtarget) { }
}
