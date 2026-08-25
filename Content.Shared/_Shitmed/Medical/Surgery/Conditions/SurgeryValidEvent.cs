using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Conditions;

[ByRefEvent]
public record struct SurgeryValidEvent(EntityUid Body, EntityUid Part, bool Cancelled = false, ProtoId<OrganCategoryPrototype>? Category = default)
{
    public List<string>? BlockReasons = null;

    public void AddBlockReason(string locId) => (BlockReasons ??= new()).Add(locId);

    public readonly bool Blocked => Cancelled || BlockReasons is { Count: > 0 };
}
