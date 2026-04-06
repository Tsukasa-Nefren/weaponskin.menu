using System.Threading;
using Sharp.Shared.GameEntities;

namespace WeaponSkin.Menu.Managers;

internal static class GloveVisualRefresh
{
    private const string TemporaryRefreshModel = "characters/models/tm_jumpsuit/tm_jumpsuit_varianta.vmdl";
    private static long _nextSyntheticItemId = DateTime.UtcNow.Ticks;

    public static void PrepareModelRefresh(IPlayerPawn pawn)
    {
        var currentModel = pawn.GetBodyComponent()?.GetSceneNode()?.AsSkeletonInstance?.GetModelState().ModelName;

        if (string.IsNullOrWhiteSpace(currentModel))
        {
            return;
        }

        pawn.SetModel(TemporaryRefreshModel);
        pawn.SetModel(currentModel);
    }

    public static void Apply(IPlayerPawn pawn, ulong steamId, int gloveId, int prefab, float wear, int seed)
    {
        pawn.GiveGloves(gloveId, prefab, wear, seed);

        var econGloves = pawn.GetEconGloves();
        if (econGloves is null)
        {
            pawn.AcceptInput("SetBodygroup", value: "default_gloves,1");
            return;
        }

        var itemId = unchecked((ulong)Interlocked.Increment(ref _nextSyntheticItemId));

        econGloves.SetItemDefinitionIndexLocal((ushort)gloveId);
        econGloves.SetAccountIdLocal(unchecked((uint)steamId));
        econGloves.SetItemIdLowLocal((uint)(itemId & 0xFFFFFFFF));
        econGloves.SetItemIdHighLocal((uint)(itemId >> 32));
        econGloves.SetInitializedLocal(true);

        econGloves.NetworkStateChanged("m_iItemDefinitionIndex", true);
        econGloves.NetworkStateChanged("m_iAccountID", true);
        econGloves.NetworkStateChanged("m_iItemIDLow", true);
        econGloves.NetworkStateChanged("m_iItemIDHigh", true);
        econGloves.NetworkStateChanged("m_bInitialized", true);

        pawn.AcceptInput("SetBodygroup", value: "default_gloves,1");
    }
}
