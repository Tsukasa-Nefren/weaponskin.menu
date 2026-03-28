using Microsoft.Extensions.Logging;
using Ptr.Shared.Extensions;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.GameObjects;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace WeaponSkin.Menu.Managers;

[Flags]
internal enum LiveApplyTarget
{
    None = 0,
    Weapons = 1 << 0,
    Gloves = 1 << 1,
    Agents = 1 << 2,
    MusicKit = 1 << 3,
    Medal = 1 << 4,
    All = Weapons | Gloves | Agents | MusicKit | Medal,
}

internal interface ILiveApplyService
{
    Task ApplyAsync(
        SteamID steamId,
        LiveApplyTarget targets,
        EconItemId? weaponItemId = null,
        bool refreshKnife = false);
}

internal sealed class LiveApplyService(
    InterfaceBridge bridge,
    IPlayerInfoManager playerInfo,
    ILogger<LiveApplyService> logger) : ILiveApplyService
{
    private const int MedalRankIndex = 5;
    private static readonly TimeSpan WeaponRefreshDelay = TimeSpan.FromMilliseconds(200);

    public async Task ApplyAsync(
        SteamID steamId,
        LiveApplyTarget targets,
        EconItemId? weaponItemId = null,
        bool refreshKnife = false)
    {
        if (targets == LiveApplyTarget.None
            || bridge.ClientManager.GetGameClient(steamId) is not { } client
            || !IsValidClient(client))
        {
            return;
        }

        await bridge.ModSharp.InvokeFrameActionAsync(() => ApplyImmediate(client, targets)).ConfigureAwait(false);

        if (!targets.HasFlag(LiveApplyTarget.Weapons))
        {
            return;
        }

        try
        {
            await Task.Delay(WeaponRefreshDelay).ConfigureAwait(false);

            if (bridge.ClientManager.GetGameClient(steamId) is not { } timedClient || !IsValidClient(timedClient))
            {
                return;
            }

            await bridge.ModSharp.InvokeFrameActionAsync(() =>
            {
                if (bridge.ClientManager.GetGameClient(steamId) is { } current && IsValidClient(current))
                {
                    RefreshWeapons(current, weaponItemId, refreshKnife);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to live refresh weapons for {steamId}", steamId);
        }
    }

    private void ApplyImmediate(IGameClient client, LiveApplyTarget targets)
    {
        if (!IsValidClient(client)
            || client.GetPlayerController() is not { } controller
            || !controller.IsValid())
        {
            return;
        }

        var team = ResolveCurrentTeam(controller);

        if (team is not (CStrikeTeam.CT or CStrikeTeam.TE))
        {
            return;
        }

        if (targets.HasFlag(LiveApplyTarget.MusicKit))
        {
            ApplyMusicKit(client, controller, team);
        }

        if (targets.HasFlag(LiveApplyTarget.Medal))
        {
            ApplyMedal(client, controller, team);
        }

        if (controller.GetPlayerPawn() is not { } pawn
            || !pawn.IsValid()
            || !pawn.IsAlive)
        {
            return;
        }

        if (targets.HasFlag(LiveApplyTarget.Agents))
        {
            ApplyAgent(client, controller, pawn, team);
        }

        if (targets.HasFlag(LiveApplyTarget.Gloves))
        {
            ApplyGloves(client, controller, pawn, team);
        }
    }

    private void ApplyMusicKit(IGameClient client, IPlayerController controller, CStrikeTeam team)
    {
        if (controller.GetInventoryService() is not { } inventory)
        {
            return;
        }

        inventory.MusicId = playerInfo.GetPlayerMusicKit(client, team)
                            ?? GetLoadoutItemDefinition(controller, team, LoadoutSlot.MusicKit)
                            ?? 0;
    }

    private void ApplyMedal(IGameClient client, IPlayerController controller, CStrikeTeam team)
    {
        if (controller.GetInventoryService() is not { } inventory)
        {
            return;
        }

        var ranks = inventory.GetSchemaFixedArray<uint>("m_rank");
        ranks[MedalRankIndex] = playerInfo.GetPlayerMedal(client, team)
                               ?? GetLoadoutItemDefinition(controller, team, LoadoutSlot.Flair0)
                               ?? 0;
        inventory.NetworkStateChanged("m_rank");
    }

    private void ApplyAgent(IGameClient client, IPlayerController controller, IPlayerPawn pawn, CStrikeTeam team)
    {
        var agentId = playerInfo.GetPlayerAgent(client, team)
                      ?? GetLoadoutItemDefinition(controller, team, LoadoutSlot.ClothingCustomPlayer);

        if (agentId is null
            || bridge.EconItemManager.GetEconItemDefinitionByIndex(agentId.Value) is not
                { DefaultLoadoutSlot: (int)LoadoutSlot.ClothingCustomPlayer, BaseDisplayModel: { Length: > 0 } model })
        {
            return;
        }

        pawn.SetNetVar("m_nCharacterDefIndex", agentId.Value);
        pawn.SetModel(model);
    }

    private void ApplyGloves(IGameClient client, IPlayerController controller, IPlayerPawn pawn, CStrikeTeam team)
    {
        if (playerInfo.GetPlayerGloves(client, team) is { } glovesId
            && playerInfo.GetPlayerWeaponSkin(client, (EconItemId)glovesId) is { } cosmetics)
        {
            pawn.GiveGloves(glovesId, cosmetics.PaintId, cosmetics.Wear, (int)cosmetics.Seed);
            return;
        }

        var defaultGloveId = GetLoadoutItemDefinition(controller, team, LoadoutSlot.ClothingHands);
        if (defaultGloveId is null)
        {
            return;
        }

        pawn.GiveGloves((EconGlovesId)defaultGloveId.Value, 0, 0f, 0);
    }

    private void RefreshWeapons(IGameClient client, EconItemId? weaponItemId, bool refreshKnife)
    {
        if (!IsValidClient(client)
            || client.GetPlayerController() is not { } controller
            || !controller.IsValid()
            || controller.GetPlayerPawn() is not { } pawn
            || !pawn.IsValid()
            || !pawn.IsAlive
            || pawn.Team is not (CStrikeTeam.CT or CStrikeTeam.TE)
            || pawn.GetWeaponService() is not { } weaponService)
        {
            return;
        }

        var activeWeapon = weaponService.ActiveWeapon;
        var snapshots = new List<WeaponSnapshot>();

        foreach (var handle in weaponService.GetMyWeapons())
        {
            var weapon = bridge.EntityManager.FindEntityByHandle(handle);
            var snapshot = weapon is null ? null : CreateSnapshot(weapon, activeWeapon);

            if (weapon is null
                || !weapon.IsValid()
                || weapon.Slot is not (GearSlot.Rifle or GearSlot.Pistol or GearSlot.Knife)
                || snapshot is null)
            {
                continue;
            }

            if (!ShouldRefreshWeapon(snapshot, weaponItemId, refreshKnife))
            {
                continue;
            }

            snapshots.Add(snapshot);
        }

        if (snapshots.Count == 0)
        {
            return;
        }

        foreach (var snapshot in snapshots)
        {
            pawn.RemovePlayerItem(snapshot.Weapon);
        }

        IBaseWeapon? weaponToSelect = null;

        if (snapshots.Any(static snapshot => snapshot.Slot == GearSlot.Knife))
        {
            var defaultKnifeClassname = pawn.Team == CStrikeTeam.TE ? "weapon_knife_t" : "weapon_knife";
            var recreatedKnife = pawn.GiveNamedItem(defaultKnifeClassname);

            if (recreatedKnife is not null
                && recreatedKnife.IsValid()
                && snapshots.Any(static snapshot => snapshot.Slot == GearSlot.Knife && snapshot.WasActive))
            {
                weaponToSelect = recreatedKnife;
            }
        }

        foreach (var snapshot in snapshots.Where(static snapshot => snapshot.Slot is GearSlot.Rifle or GearSlot.Pistol))
        {
            var recreatedWeapon = pawn.GiveNamedItem(snapshot.Classname);

            if (recreatedWeapon is null || !recreatedWeapon.IsValid())
            {
                continue;
            }

            RestoreAmmo(recreatedWeapon, snapshot);

            if (snapshot.WasActive)
            {
                weaponToSelect = recreatedWeapon;
            }
        }

        if (weaponToSelect is not null && weaponToSelect.IsValid())
        {
            pawn.SwitchWeapon(weaponToSelect);
        }
    }

    private static void RestoreAmmo(IBaseWeapon weapon, WeaponSnapshot snapshot)
    {
        if (snapshot.Clip >= 0)
        {
            weapon.SetClip(snapshot.Clip);
        }

        if (snapshot.ReserveAmmo >= 0)
        {
            weapon.SetReserveAmmo(snapshot.ReserveAmmo);
        }
    }

    private static WeaponSnapshot? CreateSnapshot(IBaseWeapon weapon, IBaseWeapon? activeWeapon)
    {
        var classname = weapon.GetWeaponClassname();

        if (string.IsNullOrWhiteSpace(classname))
        {
            return null;
        }

        return new WeaponSnapshot(
            weapon,
            (EconItemId)weapon.ItemDefinitionIndex,
            weapon.Slot,
            classname,
            weapon.Clip,
            weapon.ReserveAmmo,
            activeWeapon is not null && weapon.Handle == activeWeapon.Handle);
    }

    private static bool ShouldRefreshWeapon(WeaponSnapshot snapshot, EconItemId? weaponItemId, bool refreshKnife)
    {
        if (refreshKnife)
        {
            return snapshot.Slot == GearSlot.Knife;
        }

        if (weaponItemId is null)
        {
            return true;
        }

        return snapshot.ItemId == weaponItemId.Value;
    }

    private ushort? GetLoadoutItemDefinition(IPlayerController controller, CStrikeTeam team, LoadoutSlot slot)
    {
        return controller.GetItemInLoadoutFromInventory(team, (int)slot) is { ItemDefinitionIndex: > 0 } item
            ? item.ItemDefinitionIndex
            : null;
    }

    private static CStrikeTeam ResolveCurrentTeam(IPlayerController controller)
    {
        var pawnTeam = controller.GetPlayerPawn()?.Team;

        return pawnTeam is CStrikeTeam.CT or CStrikeTeam.TE
            ? pawnTeam.Value
            : controller.PendingTeamNum;
    }

    private static bool IsValidClient(IGameClient client)
        => client is { IsValid: true, IsFakeClient: false, IsConnected: true, IsInGame: true };

    private sealed record WeaponSnapshot(
        IBaseWeapon Weapon,
        EconItemId ItemId,
        GearSlot Slot,
        string Classname,
        int Clip,
        int ReserveAmmo,
        bool WasActive);
}
