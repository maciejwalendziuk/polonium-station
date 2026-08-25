// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Shitmed.Body;

public sealed partial class BodyDamageBridgeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Keyed off BodyComponent rather than InjurableComponent: Robust's directed event
        // subscriptions are single-subscriber per (component, event) pair, and core
        // DamageableSystem already owns <InjurableComponent, DamageDealtEvent>. Every mob
        // with a Body also has Injurable, so this still observes the same damage deltas.
        SubscribeLocalEvent<BodyComponent, DamageDealtEvent>(OnDamageDealt);

        // Organ->mob sync. DamageChangedEvent (not DamageDealtEvent) so it also picks up
        // SetDamage-style direct writes and fires after TotalDamage/DamagePerGroup are
        // already recomputed on the organ.
        SubscribeLocalEvent<WoundableComponent, DamageChangedEvent>(OnOrganDamageChanged);
    }

    private void OnDamageDealt(Entity<BodyComponent> ent, ref DamageDealtEvent args)
    {
        // Server-authoritative only. Damage itself still replicates to clients normally via
        // DamageableComponent's own networked state.
        if (!_net.IsServer)
            return;

        if (ent.Comp.Organs is null)
            return;

        if (!HasRealDelta(args.Damage))
            return;

        if (HasComp<SkipDamageBridgeComponent>(ent))
            return;

        if (args.Origin is { } origin)
        {
            var selectedTarget = TargetBodyPart.Chest;
            if (TryComp<TargetingComponent>(origin, out var targeting))
                selectedTarget = targeting.Target;

            var rolledTarget = RollHitLocation(ent.Owner, selectedTarget, args.Damage);

            if (LimbTargetMap.TryGetCategory(rolledTarget, out var category)
                && TryResolveOrganWithFallback(ent.Comp, category, out var organ))
            {
                ApplyBridgedDamage(organ, args.Damage, origin);
                return;
            }

            ApplyToAllLimbs(ent.Comp, args.Damage, args.Origin);
            return;
        }

        ApplyToAllLimbs(ent.Comp, args.Damage, args.Origin);
    }

    private bool TryResolveOrganWithFallback(BodyComponent body, ProtoId<OrganCategoryPrototype> category, out EntityUid organ)
    {
        while (true)
        {
            if (LimbTargetMap.TryGetOrganByCategory(EntityManager, body, category, out organ))
                return true;

            if (!LimbTargetMap.TryGetParentCategory(category, out var parent))
                return false;

            category = parent;
        }
    }

    private TargetBodyPart RollHitLocation(EntityUid victim, TargetBodyPart selectedTarget, DamageSpecifier damage)
    {
        if (damage.GetTotal() <= FixedPoint2.Zero)
            return selectedTarget;

        if (!TryComp<TargetingComponent>(victim, out var victimTargeting))
            return selectedTarget;

        if (_mobState.IsIncapacitated(victim) || _standing.IsDown(victim))
            return selectedTarget;

        if (!victimTargeting.TargetOdds.TryGetValue(selectedTarget, out var odds) || odds.Count == 0)
            return selectedTarget;

        var totalWeight = odds.Values.Sum();
        var randomValue = _random.NextFloat() * totalWeight;

        foreach (var (part, weight) in odds)
        {
            if (randomValue <= weight)
                return part;

            randomValue -= weight;
        }

        return TargetBodyPart.Chest; // Default to torso if something goes wrong.
    }

    private void ApplyToAllLimbs(BodyComponent body, DamageSpecifier damage, EntityUid? origin)
    {
        if (body.Organs is null)
            return;

        // Collect the present limb organs and their relative weights, tracking the heaviest
        // (the torso) as the anchor that will soak the rounding remainder.
        var limbs = new List<(EntityUid Organ, float Weight)>();
        var totalWeight = 0f;
        EntityUid anchor = default;
        var anchorWeight = -1f;

        foreach (var contained in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(contained, out var organComp)
                || organComp.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var target))
                continue;

            var weight = GetPartDamageWeight(target);
            limbs.Add((contained, weight));
            totalWeight += weight;

            if (weight > anchorWeight)
            {
                anchorWeight = weight;
                anchor = contained;
            }
        }

        if (totalWeight <= 0f)
            return;

        var applied = new DamageSpecifier();
        foreach (var (organ, weight) in limbs)
        {
            if (organ == anchor)
                continue;

            var weighted = damage * (weight / totalWeight);
            applied += weighted;
            ApplyBridgedDamage(organ, weighted, origin);
        }

        ApplyBridgedDamage(anchor, damage - applied, origin);
    }

    /// <summary>
    /// Applies a bridge-originated damage delta to a single organ, marked so the organ->mob
    /// auto-sync skips it (the mob already has this damage directly via its own InjurableComponent
    /// reaction to the originating DamageDealtEvent - mirroring it back would double it).
    /// </summary>
    private void ApplyBridgedDamage(EntityUid organ, DamageSpecifier damage, EntityUid? origin)
    {
        AddComp<SkipOrganMobSyncComponent>(organ);
        _damageable.TryChangeDamage(organ, damage, ignoreResistances: true, interruptsDoAfters: false, origin: origin);
        RemComp<SkipOrganMobSyncComponent>(organ);
    }

    /// <summary>
    /// Mirrors an organ's own damage delta back onto its parent mob's DamageableComponent,
    /// unless the delta was just applied here by the mob-&gt;organ fan-out above (in which
    /// case the mob already has it directly via its own InjurableComponent reaction to the
    /// same originating DamageDealtEvent - propagating it back would double it).
    /// </summary>
    private void OnOrganDamageChanged(EntityUid uid, WoundableComponent component, DamageChangedEvent args)
    {
        // Server-authoritative only.
        if (!_net.IsServer)
            return;

        if (args.DamageDelta is not { } delta || delta.Empty)
            return;

        if (HasComp<SkipOrganMobSyncComponent>(uid))
            return;

        if (!TryComp<OrganComponent>(uid, out var organ) || organ.Body is not { } body)
            return;

        // try/finally because the marker suppresses all bridged damage while it's present - if
        // TryChangeDamage throws, a plain sequential RemComp would be skipped and this body would
        // silently stop receiving organ->mob damage for the rest of the round.
        AddComp<SkipDamageBridgeComponent>(body);
        try
        {
            _damageable.TryChangeDamage(body, delta, ignoreResistances: true, interruptsDoAfters: false, origin: args.Origin);
        }
        finally
        {
            RemComp<SkipDamageBridgeComponent>(body);
        }
    }

    // Relative shares only - ApplyToAllLimbs normalizes these to sum to 1.0 over the present
    // limbs, so what matters is the ratio between parts (torso soaks the most), not the absolutes.
    private static float GetPartDamageWeight(TargetBodyPart target)
    {
        return target switch
        {
            TargetBodyPart.Head => 0.2f,
            TargetBodyPart.Chest => 1.0f,
            TargetBodyPart.LeftArm or TargetBodyPart.RightArm => 0.3f,
            TargetBodyPart.LeftLeg or TargetBodyPart.RightLeg => 0.3f,
            _ => 0.2f,
        };
    }

    private static bool HasRealDelta(DamageSpecifier damage)
    {
        return damage.GetTotal() != FixedPoint2.Zero;
    }
}
