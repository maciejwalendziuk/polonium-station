// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Medical;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Medical.HealthAnalyzer;


// Base message that contains common data for all Modes
[Serializable, NetSerializable]
public abstract class HealthAnalyzerBaseMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity? TargetEntity;
    public readonly float Temperature;
    public readonly float BloodLevel;
    public readonly bool? ScanMode;
    public readonly HealthAnalyzerMode ActiveMode;
    public readonly Dictionary<TargetBodyPart, WoundableSeverity>? Body;
    public readonly Dictionary<TargetBodyPart, bool> Bleeding;

    public readonly bool SystemicBleeding;

    public readonly Dictionary<TargetBodyPart, bool> Tourniqueted;

    public readonly Dictionary<TargetBodyPart, bool> UnfinishedSurgery;

    public readonly List<ProtoId<OrganCategoryPrototype>> MissingOrgans;

    public readonly Dictionary<TargetBodyPart, bool> BlockedHealing;

    public readonly DefibrillationReadiness Defibrillation;

    protected HealthAnalyzerBaseMessage(
        NetEntity? targetEntity,
        float temperature,
        float bloodLevel,
        bool? scanMode,
        HealthAnalyzerMode activeMode,
        Dictionary<TargetBodyPart, WoundableSeverity>? body,
        Dictionary<TargetBodyPart, bool> bleeding,
        bool systemicBleeding,
        Dictionary<TargetBodyPart, bool> tourniqueted,
        Dictionary<TargetBodyPart, bool> unfinishedSurgery,
        List<ProtoId<OrganCategoryPrototype>> missingOrgans,
        Dictionary<TargetBodyPart, bool> blockedHealing,
        DefibrillationReadiness defibrillation)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        ActiveMode = activeMode;
        Body = body;
        Bleeding = bleeding;
        SystemicBleeding = systemicBleeding;
        Tourniqueted = tourniqueted;
        UnfinishedSurgery = unfinishedSurgery;
        MissingOrgans = missingOrgans;
        BlockedHealing = blockedHealing;
        Defibrillation = defibrillation;
    }
}

// Body Mode message
[Serializable, NetSerializable]
public sealed class HealthAnalyzerBodyMessage : HealthAnalyzerBaseMessage
{
    public readonly bool Unrevivable;
    public readonly NetEntity? SelectedPart;
    public readonly Dictionary<NetEntity, List<WoundableTraumaData>> Traumas;

    public HealthAnalyzerBodyMessage(
        NetEntity? targetEntity,
        float temperature,
        float bloodLevel,
        bool? scanMode,
        bool unrevivable,
        Dictionary<TargetBodyPart, WoundableSeverity>? body,
        Dictionary<TargetBodyPart, bool> bleeding,
        bool systemicBleeding,
        Dictionary<TargetBodyPart, bool> tourniqueted,
        Dictionary<TargetBodyPart, bool> unfinishedSurgery,
        List<ProtoId<OrganCategoryPrototype>> missingOrgans,
        Dictionary<TargetBodyPart, bool> blockedHealing,
        DefibrillationReadiness defibrillation,
        Dictionary<NetEntity, List<WoundableTraumaData>> traumas,
        NetEntity? selectedPart)
        : base(targetEntity, temperature, bloodLevel, scanMode, HealthAnalyzerMode.Body, body, bleeding, systemicBleeding, tourniqueted, unfinishedSurgery, missingOrgans, blockedHealing, defibrillation)
    {
        Unrevivable = unrevivable;
        SelectedPart = selectedPart;
        Traumas = traumas;
    }
}

[Serializable, NetSerializable]
public struct WoundableTraumaData
{
    public string TraumaType;
    public FixedPoint2 Severity;
    public string? SeverityString; // Used mostly in Bone Damage traumas to keep track of the secondary severity.

    public WoundableTraumaData(string traumaType, FixedPoint2 severity, string? severityString = null)
    {
        TraumaType = traumaType;
        Severity = severity;
        SeverityString = severityString;
    }
}

// Organs Mode message
[Serializable, NetSerializable]
public sealed class HealthAnalyzerOrgansMessage : HealthAnalyzerBaseMessage
{
    public readonly Dictionary<NetEntity, OrganTraumaData> Organs;

    public HealthAnalyzerOrgansMessage(
        NetEntity? targetEntity,
        float temperature,
        float bloodLevel,
        bool? scanMode,
        Dictionary<TargetBodyPart, WoundableSeverity>? body,
        Dictionary<TargetBodyPart, bool> bleeding,
        bool systemicBleeding,
        Dictionary<TargetBodyPart, bool> tourniqueted,
        Dictionary<TargetBodyPart, bool> unfinishedSurgery,
        List<ProtoId<OrganCategoryPrototype>> missingOrgans,
        Dictionary<TargetBodyPart, bool> blockedHealing,
        DefibrillationReadiness defibrillation,
        Dictionary<NetEntity, OrganTraumaData> organs)
        : base(targetEntity, temperature, bloodLevel, scanMode, HealthAnalyzerMode.Organs, body, bleeding, systemicBleeding, tourniqueted, unfinishedSurgery, missingOrgans, blockedHealing, defibrillation)
    {
        Organs = organs;
    }
}

// Chemicals Mode message
[Serializable, NetSerializable]
public sealed class HealthAnalyzerChemicalsMessage : HealthAnalyzerBaseMessage
{
    public readonly Dictionary<NetEntity, NamedSolution> Solutions;

    public HealthAnalyzerChemicalsMessage(
        NetEntity? targetEntity,
        float temperature,
        float bloodLevel,
        bool? scanMode,
        Dictionary<TargetBodyPart, WoundableSeverity>? body,
        Dictionary<TargetBodyPart, bool> bleeding,
        bool systemicBleeding,
        Dictionary<TargetBodyPart, bool> tourniqueted,
        Dictionary<TargetBodyPart, bool> unfinishedSurgery,
        List<ProtoId<OrganCategoryPrototype>> missingOrgans,
        Dictionary<TargetBodyPart, bool> blockedHealing,
        DefibrillationReadiness defibrillation,
        Dictionary<NetEntity, NamedSolution> solutions)
        : base(targetEntity, temperature, bloodLevel, scanMode, HealthAnalyzerMode.Chemicals, body, bleeding, systemicBleeding, tourniqueted, unfinishedSurgery, missingOrgans, blockedHealing, defibrillation)
    {
        Solutions = solutions;
    }
}

[Serializable, NetSerializable]
public struct NamedSolution
{
    public string Name;
    public Solution Solution;

    public NamedSolution(string name, Solution solution)
    {
        Name = name;
        Solution = solution;
    }
}

// Mode selection message (from client to server)
[Serializable, NetSerializable]
public sealed class HealthAnalyzerModeSelectedMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Owner;
    public readonly HealthAnalyzerMode Mode;

    public HealthAnalyzerModeSelectedMessage(NetEntity owner, HealthAnalyzerMode mode)
    {
        Owner = owner;
        Mode = mode;
    }
}

// Part selection message (from client to server)
[Serializable, NetSerializable]
public sealed class HealthAnalyzerPartSelectedMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Owner;
    public readonly TargetBodyPart? BodyPart;

    public HealthAnalyzerPartSelectedMessage(NetEntity owner, TargetBodyPart? bodyPart)
    {
        Owner = owner;
        BodyPart = bodyPart;
    }
}

[Serializable, NetSerializable]
public struct OrganTraumaData
{
    public FixedPoint2 Integrity;
    public FixedPoint2 IntegrityCap;
    public OrganSeverity Severity;
    public List<(string Name, FixedPoint2 Value)> Modifiers;

    public OrganTraumaData(FixedPoint2 integrity,
        FixedPoint2 integrityCap,
        OrganSeverity severity,
        List<(string Name, FixedPoint2 Value)> modifiers)
    {
        Integrity = integrity;
        IntegrityCap = integrityCap;
        Severity = severity;
        Modifiers = modifiers;
    }
}

[Serializable, NetSerializable]
public enum HealthAnalyzerMode
{
    Body,
    Organs,
    Chemicals,
}
