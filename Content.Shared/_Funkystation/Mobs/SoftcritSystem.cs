using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared.Mobs.Systems;

public partial class MobStateSystem
{
    private const float SoftCritSpeedMultiplier = 0.4f;

    private void InitializeSoftcrit()
    {
        SubscribeLocalEvent<MobStateComponent, RefreshMovementSpeedModifiersEvent>(OnSoftcritSpeedRefresh);
        SubscribeLocalEvent<MobStateComponent, PullStartedMessage>(OnPullInteractionStateChanged);
        SubscribeLocalEvent<MobStateComponent, PullStoppedMessage>(OnPullInteractionStateChanged);
    }

    private void OnSoftcritSpeedRefresh(EntityUid uid, MobStateComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.CurrentState == MobState.SoftCritical)
        {
            args.ModifySpeed(SoftCritSpeedMultiplier, SoftCritSpeedMultiplier);
        }
    }

    private void OnPullInteractionStateChanged(EntityUid uid, MobStateComponent component, PullMessage args)
    {
        if (component.CurrentState == MobState.SoftCritical)
        {
            _blocker.UpdateCanMove(uid);
        }
    }

    private bool ResolveStateFallback(MobState fromState, MobState toState, MobStateComponent component, out MobState resolvedState)
    {
        resolvedState = toState;

        if (toState != MobState.Critical)
            return false;

        if (component.AllowedStates.Contains(MobState.SoftCritical) &&
            fromState is MobState.Alive or MobState.SoftCritical)
        {
            resolvedState = MobState.SoftCritical;
            return true;
        }

        if (component.AllowedStates.Contains(MobState.HardCritical))
        {
            resolvedState = MobState.HardCritical;
            return true;
        }

        return false;
    }
}
