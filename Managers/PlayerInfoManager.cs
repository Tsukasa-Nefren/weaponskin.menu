using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using WeaponSkin.Shared;

namespace WeaponSkin.Menu.Managers;

internal interface IPlayerInfoManager
{
    EconItemId? GetPlayerKnife(IGameClient client, CStrikeTeam team);

    WeaponCosmetics? GetPlayerWeaponSkin(IGameClient client, EconItemId id);

    ushort? GetPlayerAgent(IGameClient client, CStrikeTeam team);

    ushort? GetPlayerMedal(IGameClient client, CStrikeTeam team);

    ushort? GetPlayerMusicKit(IGameClient client, CStrikeTeam team);

    EconGlovesId? GetPlayerGloves(IGameClient client, CStrikeTeam team);

    void SetPlayerKnife(IGameClient client, CStrikeTeam team, EconItemId? itemId);

    void SetPlayerGloves(IGameClient client, CStrikeTeam team, EconGlovesId? itemId);

    void SetPlayerAgent(IGameClient client, CStrikeTeam team, ushort? itemId);

    void SetPlayerMedal(IGameClient client, CStrikeTeam team, ushort? itemId);

    void SetPlayerMusicKit(IGameClient client, CStrikeTeam team, ushort? itemId);

    void SetPlayerWeaponSkin(IGameClient client, WeaponCosmetics cosmetics);

    void ClearPlayerWeaponSkin(IGameClient client, EconItemId itemId);

    WeaponCosmetics? ToggleStatTrak(IGameClient client, EconItemId itemId);

    void RefreshInventory(IGameClient client, bool notify = true);

    Task RefreshInventoryAsync(IGameClient client, bool notify = true);
}

internal sealed class PlayerInfoManager(
    InterfaceBridge bridge,
    IWeaponSkinStorage storage,
    IOriginalWeaponSkinRefreshManager originalRefresh,
    ITextManager text,
    ILogger<PlayerInfoManager> logger) : IPlayerInfoManager, IClientListener, IManager
{
    private const int TeamCtIndex = 0;
    private const int TeamTeIndex = 1;
    private const int TeamMaxCount = 2;

    private readonly EconItemId?[,] _playerKnives = new EconItemId?[PlayerSlot.MaxPlayerCount, TeamMaxCount];
    private readonly EconGlovesId?[,] _playerGloves = new EconGlovesId?[PlayerSlot.MaxPlayerCount, TeamMaxCount];
    private readonly ushort?[,] _playerAgents = new ushort?[PlayerSlot.MaxPlayerCount, TeamMaxCount];
    private readonly ushort?[,] _playerMedals = new ushort?[PlayerSlot.MaxPlayerCount, TeamMaxCount];
    private readonly ushort?[,] _playerMusicKits = new ushort?[PlayerSlot.MaxPlayerCount, TeamMaxCount];
    private readonly WeaponCosmetics[][] _weaponCosmetics = Enumerable.Repeat<WeaponCosmetics[]>([], PlayerSlot.MaxPlayerCount).ToArray();
    private readonly long[] _playerStateVersions = new long[PlayerSlot.MaxPlayerCount];

    public int ListenerVersion => IClientListener.ApiVersion;

    public int ListenerPriority => 0;

    public bool Init()
    {
        bridge.ClientManager.InstallClientListener(this);
        return true;
    }

    public void Shutdown()
    {
        bridge.ClientManager.RemoveClientListener(this);
    }

    public void OnAllModulesLoaded()
    {
        bridge.ModSharp.InvokeFrameAction(() =>
        {
            foreach (var client in bridge.ClientManager.GetGameClients(inGame: true))
            {
                if (ShouldTrack(client))
                {
                    _ = RefreshInventoryAsync(client, false);
                }
            }
        });
    }

    public void OnClientPutInServer(IGameClient client)
    {
        if (!ShouldTrack(client))
        {
            return;
        }

        ClearPlayerData(client.Slot);
        _ = RefreshInventoryAsync(client, false);
    }

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        ClearPlayerData(client.Slot);
    }

    public EconItemId? GetPlayerKnife(IGameClient client, CStrikeTeam team)
        => GetPlayerTeamItem(_playerKnives, client, team);

    public WeaponCosmetics? GetPlayerWeaponSkin(IGameClient client, EconItemId id)
    {
        foreach (var cosmetic in _weaponCosmetics[client.Slot])
        {
            if (cosmetic.ItemId == id)
            {
                return cosmetic;
            }
        }

        return null;
    }

    public ushort? GetPlayerAgent(IGameClient client, CStrikeTeam team)
        => GetPlayerTeamItem(_playerAgents, client, team);

    public ushort? GetPlayerMedal(IGameClient client, CStrikeTeam team)
        => GetPlayerTeamItem(_playerMedals, client, team);

    public ushort? GetPlayerMusicKit(IGameClient client, CStrikeTeam team)
        => GetPlayerTeamItem(_playerMusicKits, client, team);

    public EconGlovesId? GetPlayerGloves(IGameClient client, CStrikeTeam team)
        => GetPlayerTeamItem(_playerGloves, client, team);

    public void SetPlayerKnife(IGameClient client, CStrikeTeam team, EconItemId? itemId)
    {
        SetPlayerTeamItem(_playerKnives, client.Slot, team, itemId);
        BumpPlayerStateVersion(client.Slot);
    }

    public void SetPlayerGloves(IGameClient client, CStrikeTeam team, EconGlovesId? itemId)
    {
        SetPlayerTeamItem(_playerGloves, client.Slot, team, itemId);
        BumpPlayerStateVersion(client.Slot);
    }

    public void SetPlayerAgent(IGameClient client, CStrikeTeam team, ushort? itemId)
    {
        SetPlayerTeamItem(_playerAgents, client.Slot, team, itemId);
        BumpPlayerStateVersion(client.Slot);
    }

    public void SetPlayerMedal(IGameClient client, CStrikeTeam team, ushort? itemId)
    {
        SetPlayerTeamItem(_playerMedals, client.Slot, team, itemId);
        BumpPlayerStateVersion(client.Slot);
    }

    public void SetPlayerMusicKit(IGameClient client, CStrikeTeam team, ushort? itemId)
    {
        SetPlayerTeamItem(_playerMusicKits, client.Slot, team, itemId);
        BumpPlayerStateVersion(client.Slot);
    }

    public void SetPlayerWeaponSkin(IGameClient client, WeaponCosmetics cosmetics)
    {
        var slot = client.Slot;
        var current = _weaponCosmetics[slot];
        var index = Array.FindIndex(current, item => item.ItemId == cosmetics.ItemId);

        if (index >= 0)
        {
            current[index] = cosmetics;
            BumpPlayerStateVersion(slot);
            return;
        }

        _weaponCosmetics[slot] = [.. current, cosmetics];
        BumpPlayerStateVersion(slot);
    }

    public void ClearPlayerWeaponSkin(IGameClient client, EconItemId itemId)
    {
        _weaponCosmetics[client.Slot] = _weaponCosmetics[client.Slot]
            .Where(item => item.ItemId != itemId)
            .ToArray();
        BumpPlayerStateVersion(client.Slot);
    }

    public WeaponCosmetics? ToggleStatTrak(IGameClient client, EconItemId itemId)
    {
        var cosmetics = GetPlayerWeaponSkin(client, itemId);

        if (cosmetics is null)
        {
            return null;
        }

        cosmetics.StatTrak = cosmetics.StatTrak is null ? 0 : null;
        BumpPlayerStateVersion(client.Slot);
        return cosmetics;
    }

    public void RefreshInventory(IGameClient client, bool notify = true)
    {
        _ = RefreshInventoryAsync(client, notify);
    }

    public Task RefreshInventoryAsync(IGameClient client, bool notify = true)
    {
        if (!ShouldTrack(client))
        {
            return Task.CompletedTask;
        }

        if (notify)
        {
            text.Notify(client, "ws.chat.refresh_started");
        }

        return LoadInventoryAsync(client.SteamId, GetPlayerStateVersion(client.Slot), notify);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T? GetPlayerTeamItem<T>(T?[,] itemArray, IGameClient client, CStrikeTeam team)
        where T : struct
    {
        return team switch
        {
            CStrikeTeam.CT => itemArray[client.Slot, TeamCtIndex],
            CStrikeTeam.TE => itemArray[client.Slot, TeamTeIndex],
            _ => null,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetPlayerTeamItem<T>(T?[,] itemArray, PlayerSlot slot, CStrikeTeam team, T? value)
        where T : struct
    {
        switch (team)
        {
            case CStrikeTeam.CT:
                itemArray[slot, TeamCtIndex] = value;
                break;
            case CStrikeTeam.TE:
                itemArray[slot, TeamTeIndex] = value;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssignItems(TeamItem[] source, EconItemId?[,] target, PlayerSlot slot)
    {
        foreach (var item in source)
        {
            SetPlayerTeamItem(target, slot, item.Team, item.ItemId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssignItems(TeamItem[] source, EconGlovesId?[,] target, PlayerSlot slot)
    {
        foreach (var item in source)
        {
            SetPlayerTeamItem(target, slot, item.Team, (EconGlovesId)item.ItemId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssignItems(TeamItem[] source, ushort?[,] target, PlayerSlot slot)
    {
        foreach (var item in source)
        {
            SetPlayerTeamItem(target, slot, item.Team, (ushort)item.ItemId);
        }
    }

    private void ClearPlayerData(PlayerSlot slot)
    {
        _weaponCosmetics[slot] = [];
        _playerStateVersions[(int)slot] = 0;

        for (var index = 0; index < TeamMaxCount; index++)
        {
            _playerKnives[slot, index] = null;
            _playerGloves[slot, index] = null;
            _playerAgents[slot, index] = null;
            _playerMedals[slot, index] = null;
            _playerMusicKits[slot, index] = null;
        }
    }

    private async Task LoadInventoryAsync(SteamID steamId, long initialStateVersion, bool notify)
    {
        try
        {
            var cosmeticsTask = storage.GetPlayerWeaponCosmetics(steamId);
            var knivesTask = storage.GetPlayerTeamKnives(steamId);
            var glovesTask = storage.GetPlayerTeamGloves(steamId);
            var agentsTask = storage.GetPlayerTeamAgents(steamId);
            var musicTask = storage.GetPlayerTeamMusicKits(steamId);
            var medalsTask = storage.GetPlayerTeamMedals(steamId);

            await Task.WhenAll(cosmeticsTask, knivesTask, glovesTask, agentsTask, musicTask, medalsTask).ConfigureAwait(false);

            await bridge.ModSharp.InvokeFrameActionAsync(() =>
            {
                if (bridge.ClientManager.GetGameClient(steamId) is not { } target)
                {
                    return;
                }

                if (GetPlayerStateVersion(target.Slot) != initialStateVersion)
                {
                    logger.LogDebug("Skipped stale WeaponSkin.Menu inventory refresh for {steamId}", steamId);
                    return;
                }

                ClearPlayerData(target.Slot);
                _weaponCosmetics[target.Slot] = cosmeticsTask.Result;
                AssignItems(knivesTask.Result, _playerKnives, target.Slot);
                AssignItems(glovesTask.Result, _playerGloves, target.Slot);
                AssignItems(agentsTask.Result, _playerAgents, target.Slot);
                AssignItems(musicTask.Result, _playerMusicKits, target.Slot);
                AssignItems(medalsTask.Result, _playerMedals, target.Slot);

                if (notify)
                {
                    text.Notify(target, "ws.chat.refresh_done");
                }
            }).ConfigureAwait(false);

            await originalRefresh.RefreshInventoryAsync(steamId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load inventory for {steamId}", steamId);
        }
    }

    private static bool ShouldTrack(IGameClient client)
        => client is { IsFakeClient: false, IsConnected: true, IsInGame: true };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetPlayerStateVersion(PlayerSlot slot)
        => Volatile.Read(ref _playerStateVersions[(int)slot]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BumpPlayerStateVersion(PlayerSlot slot)
        => Interlocked.Increment(ref _playerStateVersions[(int)slot]);
}
