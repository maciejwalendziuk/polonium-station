using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Medical.Surgery;

[Serializable, NetSerializable]
public enum SurgeryUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiState(Dictionary<NetEntity, List<SurgeryEntry>> choices) : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, List<SurgeryEntry>> Choices = choices;
}

/// <summary>
/// One surgery offered for a part. <see cref="BlockReasons"/> is empty for a surgery that can be
/// started now, or holds the LocIds of what must be fixed first - the UI greys the surgery and shows
/// them on hover rather than hiding it.
/// </summary>
[Serializable, NetSerializable]
public struct SurgeryEntry(EntProtoId surgery, List<string>? blockReasons)
{
    public EntProtoId Surgery = surgery;
    public List<string>? BlockReasons = blockReasons;

    public readonly bool Blocked => BlockReasons is { Count: > 0 };
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SurgeryStepChosenBuiMsg(NetEntity part, EntProtoId surgery, EntProtoId step, bool isBody) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly EntProtoId Step = step;

    // Used as a marker for whether or not we're hijacking surgery by applying it on the body itself.
    public readonly bool IsBody = isBody;
}
