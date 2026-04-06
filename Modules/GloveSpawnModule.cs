using Sharp.Shared.Enums;
using Sharp.Shared.HookParams;
using WeaponSkin.Menu.Managers;

namespace WeaponSkin.Menu.Modules;

internal sealed class GloveSpawnModule(
    InterfaceBridge bridge,
    IPlayerInfoManager playerInfo) : IModule
{
    public bool Init()
    {
        bridge.HookManager.PlayerSpawnPost.InstallForward(OnPlayerSpawnPost);
        return true;
    }

    public void Shutdown()
    {
        bridge.HookManager.PlayerSpawnPost.RemoveForward(OnPlayerSpawnPost);
    }

    private void OnPlayerSpawnPost(IPlayerSpawnForwardParams @params)
    {
        var client = @params.Client;

        if (client.IsFakeClient)
        {
            return;
        }

        var steamId = client.SteamId;
        bridge.ModSharp.InvokeFrameAction(() =>
        {
            if (bridge.ClientManager.GetGameClient(steamId) is not { IsFakeClient: false, IsConnected: true, IsInGame: true } current
                || current.GetPlayerController()?.GetPlayerPawn() is not { } pawn
                || !pawn.IsValid()
                || !pawn.IsAlive
                || pawn.Team is not (CStrikeTeam.CT or CStrikeTeam.TE))
            {
                return;
            }

            if (playerInfo.GetPlayerGloves(current, pawn.Team) is not { } gloves
                || playerInfo.GetPlayerWeaponSkin(current, (EconItemId)gloves) is not { } cosmetics)
            {
                return;
            }

            GloveVisualRefresh.PrepareModelRefresh(pawn);
            GloveVisualRefresh.Apply(pawn, (ulong)current.SteamId, (int)gloves, cosmetics.PaintId, cosmetics.Wear, (int)cosmetics.Seed);
        });
    }
}
