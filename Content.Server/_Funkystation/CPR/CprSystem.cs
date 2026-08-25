using Content.Shared._Funkystation.Cpr;
using Robust.Shared.Player;

namespace Content.Server._Funkystation.Cpr;

public sealed class CprSystem : SharedCprSystem
{
    public override void DoLunge(EntityUid user)
    {
        var filter = Filter.PvsExcept(user, entityManager: EntityManager);

        RaiseNetworkEvent(new CprLungeEvent(GetNetEntity(user)), filter);
    }
}
