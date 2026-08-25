using Content.Shared.Atmos.Rotting;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Electrocution;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Timing;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes; // funky
using Robust.Shared.Random; // funky
using System.Linq; // funky
using Robust.Shared.Configuration; // funky
using Robust.Shared.Network; // funky
using Content.Shared.Inventory; // funky
using Content.Shared.FixedPoint; // funky
using Content.Shared.EntityEffects.Effects.StatusEffects; // funky
using Content.Shared.Chemistry.EntitySystems; // funky
using Content.Shared.Chemistry.Reagent; // funky
using Content.Shared.Body.Components; // funky
using Content.Shared._Funkystation.CCVar; // funky
using Content.Shared.Damage; // funky

namespace Content.Shared.Medical;

/// <summary>
/// This handles interactions and logic relating to <see cref="DefibrillatorComponent"/>
/// </summary>
public abstract partial class SharedDefibrillatorSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private InventorySystem _inventory = default!; // funky
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!; // funky
    [Dependency] private IRobustRandom _random = default!; // funky
    [Dependency] private IPrototypeManager _prototypeManager = default!; // funky
    [Dependency] private IConfigurationManager _config = default!; // funky
    [Dependency] private INetManager _net = default!; // funky

    private readonly HashSet<EntityUid> _interacters = new();

    private float _reviveChance; // funky
    private float _adrenalineCostPerShock; // funky

    public override void Initialize()
    {
        base.Initialize(); // funky
        // Subs.CVar auto-unsubscribes on shutdown; raw _config.OnValueChanged would leak a handler each round. // funky
        Subs.CVar(_config, DefibrillatorCVars.ReviveChance, value => _reviveChance = value, true); // funky
        Subs.CVar(_config, DefibrillatorCVars.AdrenalineCost, value => _adrenalineCostPerShock = value, true); // funky

        SubscribeLocalEvent<DefibrillatorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DefibrillatorComponent, DefibrillatorZapDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<DefibrillatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartZap(ent.AsNullable(), target, args.User);
    }

    private void OnDoAfter(Entity<DefibrillatorComponent> ent, ref DefibrillatorZapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Target is not { } target)
            return;

        if (!CanZap(ent.AsNullable(), target, args.User))
            return;

        args.Handled = true;
        Zap(ent.AsNullable(), target, args.User);
    }

    /// <summary>
    /// Checks if you can actually defib a target.
    /// </summary>
    /// <param name="ent">The defbrillator being used.</param>
    /// <param name="target">Uid of the target getting defibbed.</param>
    /// <param name="user">Uid of the entity using the defibrillator.</param>
    /// <param name="targetCanBeAlive">
    /// If true, the target can be alive. If false, the function will check if the target is alive and will return false if they are.
    /// </param>
    /// <returns>
    /// Returns true if the target is valid to be defibed, false otherwise.
    /// </returns>
    public bool CanZap(Entity<DefibrillatorComponent?> ent, EntityUid target, EntityUid? user = null, bool targetCanBeAlive = false)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!_toggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("defibrillator-not-on"), ent.Owner, user);
            return false;
        }

        if (!TryComp<UseDelayComponent>(ent, out var useDelay) || _useDelay.IsDelayed((ent.Owner, useDelay), ent.Comp.DelayId))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return false;

        if (!_powerCell.HasActivatableCharge(ent.Owner, user: user, predicted: true))
            return false;

        if (!targetCanBeAlive && _mobState.IsAlive(target, mobState))
            return false;

        if (!targetCanBeAlive && !ent.Comp.CanDefibCrit && _mobState.IsCritical(target, mobState))
            return false;

        // funky, gotta take off their hardsuit or coat
        if (!_inventory.TryGetSlotEntity(target, "outerClothing", out _))
            return true;

        _popup.PopupClient(Loc.GetString("defibrillator-clothing-blocking"), user);
        return false;
    }

    /// <summary>
    /// Tries to start defibrillating the target. If the target is valid, will start the defib do-after.
    /// </summary>
    /// <param name="ent">The defbrillator being used.</param>
    /// <param name="target">Uid of the target getting defibbed.</param>
    /// <param name="user">Uid of the entity using the defibrillator.</param>
    /// <returns>
    /// Returns true if the defibrillation do-after started, otherwise false.
    /// </returns>
    public bool TryStartZap(Entity<DefibrillatorComponent?> ent, EntityUid target, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanZap(ent, target, user))
            return false;

        _audio.PlayPredicted(ent.Comp.ChargeSound, ent.Owner, user);
        return _doAfter.TryStartDoAfter(
            new DoAfterArgs(EntityManager, user, ent.Comp.DoAfterDuration, new DefibrillatorZapDoAfterEvent(),
            ent.Owner, target, ent.Owner)
            {
                NeedHand = true,
                BreakOnMove = !ent.Comp.AllowDoAfterMovement
            });
    }

    /// <summary>
    /// Tries to defibrillate the target with the given defibrillator.
    /// </summary>
    /// <param name="ent">The defbrillator being used.</param>
    /// <param name="target">Uid of the target getting defibbed.</param>
    /// <param name="user">Uid of the entity using the defibrillator.</param>
    public void Zap(Entity<DefibrillatorComponent?> ent, EntityUid target, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!_powerCell.TryUseActivatableCharge(ent.Owner, user: user))
            return;

        var selfEvent = new SelfBeforeDefibrillatorZapsEvent(user, ent.Owner, target);
        RaiseLocalEvent(user, selfEvent);

        target = selfEvent.DefibTarget;

        // Ensure thet new target is still valid.
        if (selfEvent.Cancelled || !CanZap(ent, target, user, true))
            return;

        var targetEvent = new TargetBeforeDefibrillatorZapsEvent(user, ent.Owner, target);
        RaiseLocalEvent(target, targetEvent);

        target = targetEvent.DefibTarget;

        if (targetEvent.Cancelled || !CanZap(ent, target, user, true))
            return;

        if (!TryComp<MobStateComponent>(target, out var targetMobState))
            return;

        _audio.PlayPredicted(ent.Comp.ZapSound, ent.Owner, user);
        _electrocution.TryDoElectrocution(target, ent.Owner, ent.Comp.ZapDamage, ent.Comp.WritheDuration, true, ignoreInsulation: true);

        _interactionSystem.GetEntitiesInteractingWithTarget(target, _interacters);
        foreach (var other in _interacters)
        {
            if (other == user)
                continue;

            // Anyone else still operating on the target gets zapped too
            _electrocution.TryDoElectrocution(other, null, ent.Comp.ZapDamage, ent.Comp.WritheDuration, true);
        }

        if (TryComp<UseDelayComponent>(ent, out var useDelay))
        {
            _useDelay.SetLength((ent.Owner, useDelay), ent.Comp.ZapDelay, id: ent.Comp.DelayId);
            _useDelay.TryResetDelay((ent.Owner, useDelay), id: ent.Comp.DelayId);
        }

        var failedRevive = true;
        if (_rotting.IsRotten(target))
        {
            _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("defibrillator-rotten"),
                InGameICChatType.Speak, true);
        }
        else if (TryComp<UnrevivableComponent>(target, out var unrevivable))
        {
            _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString(unrevivable.ReasonMessage),
                InGameICChatType.Speak, true);
        }
        else
        {
            if (_mobState.IsDead(target, targetMobState))
                _damageable.TryChangeDamage(target, ent.Comp.ZapHeal, true, origin: user);

            // funky start, need an adrenaline reagent in their system to kick the heart back on
            var hasAdrenaline = false;
            if (TryComp<BloodstreamComponent>(target, out var bloodstream))
            {
                var bloodSolution = bloodstream.BloodSolution;

                if (_solutionContainer.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodSolution))
                {
                    var contents = bloodSolution.Value.Comp.Solution.Contents;

                    // check reagents in bloodstream
                    foreach (var (reagentId, quantity) in contents)
                    {
                        // if this reagent grants adrenaline, consume it and roll for revival
                        if (quantity <= FixedPoint2.Zero || !ReagentGrantsAdrenaline(reagentId.Prototype))
                            continue;

                        hasAdrenaline = true;

                        // removes the adrenaline cost amount
                        _solutionContainer.RemoveReagent(bloodSolution.Value, reagentId, FixedPoint2.New(_adrenalineCostPerShock));

                        break;
                    }
                }
            }

            var canRevive = true;
            if (_mobState.IsDead(target, targetMobState))
            {
                canRevive = false;

                if (hasAdrenaline)
                {
                    // server-only roll to prevent client mispredicting a successful revival
                    canRevive = _net.IsServer && _random.Prob(_reviveChance);
                }
                else
                {
                    // if they have no adrenaline reagent, popup
                    _popup.PopupClient(Loc.GetString("defibrillator-no-adrenaline"), target, user);
                }
            }

            // adrenaline zap heals 25 asphyx
            if (hasAdrenaline)
            {
                var asphyxHeal = new DamageSpecifier();
                asphyxHeal.DamageDict.Add("Asphyxiation", FixedPoint2.New(-25));
                _damageable.TryChangeDamage(target, asphyxHeal, true, origin: user);
            }
            // funky end

            if (canRevive && // funky
                TryComp<MobThresholdsComponent>(target, out var targetThresholds) &&
                _mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold, targetThresholds) &&
                _damageable.GetTotalDamage(target) < threshold)
            {
                _mobState.ChangeMobState(target, MobState.Critical, targetMobState, user);
                failedRevive = false;
            }

            if (_mind.TryGetMind(target, out var mindUid, out var mindComp) &&
                _player.TryGetSessionById(mindComp.UserId, out var playerSession))
            {
                // notify them they're being revived.
                if (mindComp.CurrentEntity != target)
                    OpenReturnToBodyEui((mindUid, mindComp), playerSession);
            }
            else
            {
                _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("defibrillator-no-mind"),
                    InGameICChatType.Speak, true);
            }
        }

        var sound = failedRevive
            ? ent.Comp.FailureSound
            : ent.Comp.SuccessSound;
        _audio.PlayPredicted(sound, ent.Owner, user);

        // if we don't have enough power left for another shot, turn it off
        if (!_powerCell.HasActivatableCharge(ent.Owner))
            _toggle.TryDeactivate(ent.Owner);

        var ev = new TargetDefibrillatedEvent(user, (ent.Owner, ent.Comp));
        RaiseLocalEvent(target, ref ev);
    }

    /// <summary>
    /// Asphyxiation a standard defibrillator's shock heals on a dead target (the zapHeal on the base
    /// Defibrillator prototype). The analyzer can't know which defib a medic will bring, so its
    /// readiness read assumes the standard one.
    /// </summary>
    private const int StandardShockAsphyxHeal = 40;

    /// <summary>Extra asphyxiation the adrenaline kick heals on top of the shock (see <see cref="Zap"/>).</summary>
    private const int AdrenalineAsphyxHeal = 25;

    /// <summary>Post-shock damage this close to the death threshold still revives, but lands them at
    /// the crit edge - reported as risky rather than ready.</summary>
    private const float RiskyThresholdFraction = 0.9f;

    private static readonly ProtoId<Damage.Prototypes.DamageTypePrototype> AsphyxiationType = "Asphyxiation";

    /// <summary>
    /// Whether a reagent's bloodstream metabolism grants the Adrenaline status effect - the same test
    /// <see cref="Zap"/> uses to decide a shock can revive. Matches by effect, not name, so epinephrine
    /// and the stimulants all qualify.
    /// </summary>
    public bool ReagentGrantsAdrenaline(string reagentProtoId)
    {
        return _prototypeManager.TryIndex<ReagentPrototype>(reagentProtoId, out var proto)
            && proto.Metabolisms != null
            && proto.Metabolisms.Metabolisms.TryGetValue("Bloodstream", out var metabolism)
            && metabolism.Effects.Any(effect => effect is GenericStatusEffect { Key: "Adrenaline" });
    }

    /// <summary>Whether the target's blood holds any reagent that would let a defib shock revive them.</summary>
    public bool HasAdrenalineReagent(EntityUid target)
    {
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return false;

        var bloodSolution = bloodstream.BloodSolution;
        if (!_solutionContainer.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodSolution))
            return false;

        foreach (var (reagentId, quantity) in bloodSolution.Value.Comp.Solution.Contents)
        {
            if (quantity > FixedPoint2.Zero && ReagentGrantsAdrenaline(reagentId.Prototype))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a dead patient could be shocked back, and if not, why not - the analyzer's read of the
    /// same conditions <see cref="Zap"/> revives on. Only meaningful for a Dead target; a living (even
    /// crit) patient is not a defib case. Assumes a standard defibrillator's asphyxiation heal.
    /// </summary>
    public DefibrillationReadiness GetDefibrillationReadiness(EntityUid target)
    {
        if (!_mobState.IsDead(target))
            return DefibrillationReadiness.None;

        if (_rotting.IsRotten(target) || HasComp<UnrevivableComponent>(target))
            return DefibrillationReadiness.Hopeless;

        if (!_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var deadThreshold))
            return DefibrillationReadiness.None;

        var asphyx = _damageable.GetDamageOfType(target, AsphyxiationType);
        var healed = FixedPoint2.Min(asphyx, FixedPoint2.New(StandardShockAsphyxHeal + AdrenalineAsphyxHeal));
        var postShockDamage = _damageable.GetTotalDamage(target) - healed;

        if (postShockDamage >= deadThreshold)
            return DefibrillationReadiness.TooMuchDamage;

        if (!HasAdrenalineReagent(target))
            return DefibrillationReadiness.NeedsAdrenaline;

        return postShockDamage >= deadThreshold * RiskyThresholdFraction
            ? DefibrillationReadiness.Risky
            : DefibrillationReadiness.Ready;
    }

    // TODO: SharedEuiManager so that we can just directly open the eui from shared.
    protected virtual void OpenReturnToBodyEui(Entity<MindComponent> mind, ICommonSession session) { }
}
