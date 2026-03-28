using Microsoft.Extensions.Logging;
using MenuModel = Sharp.Modules.MenuManager.Shared.Menu;
using Ptr.Shared.Extensions;
using Sharp.Extensions.CommandManager;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using WeaponSkin.Menu.Managers;
using WeaponSkin.Menu.Persistence;
using WeaponSkin.Shared;

namespace WeaponSkin.Menu.Modules;

internal sealed class MenuCommands(
    InterfaceBridge bridge,
    ICommandManager commands,
    ICatalogManager catalog,
    IMenuConfigManager config,
    ILiveApplyService liveApply,
    IOriginalWeaponSkinRefreshManager originalRefresh,
    IPlayerOperationQueue operationQueue,
    IPlayerInfoManager playerInfo,
    IWeaponSkinStorage storage,
    ITextManager text,
    ILogger<MenuCommands> logger) : IModule
{
    private const float DefaultWear = 0.01f;
    private const float DefaultSeed = 0f;

    private IMenuManager? _menuManager;

    public bool Init()
    {
        RegisterMenuCommand("ws", OpenMainMenu);
        RegisterMenuCommand("ws_menu", OpenMainMenu);
        RegisterMenuCommand("ws_knife", client => OpenSubMenu(client, BuildKnifeMenu));
        RegisterMenuCommand("ws_gloves", client => OpenSubMenu(client, BuildGloveMenu));
        RegisterMenuCommand("ws_agents", client => OpenSubMenu(client, BuildAgentMenu));
        RegisterMenuCommand("ws_music", client => OpenSubMenu(client, BuildMusicMenu));
        RegisterMenuCommand("ws_pins", client => OpenSubMenu(client, BuildPinMenu));
        RegisterMenuCommand("ws_stattrak", ToggleStatTrak);
        RegisterMenuCommand("ws_st", ToggleStatTrak);
        return true;
    }

    public void OnAllModulesLoaded()
    {
        _menuManager = bridge.GetMenuManager();
    }

    public void OnLibraryConnected(string name)
    {
        if (name.Contains("MenuManager", StringComparison.OrdinalIgnoreCase))
        {
            _menuManager = bridge.GetMenuManager();
        }
    }

    public void OnLibraryDisconnect(string name)
    {
        if (name.Contains("MenuManager", StringComparison.OrdinalIgnoreCase))
        {
            _menuManager = null;
        }
    }

    private void RegisterMenuCommand(string name, Action<IGameClient> action)
    {
        commands.RegisterClientCommand(name, (client, command) =>
        {
            if (IsValidPlayer(client))
            {
                action(client);
            }
        });
    }

    private void OpenMainMenu(IGameClient client)
    {
        if (_menuManager is null)
        {
            text.Notify(client, "ws.chat.menu_unavailable");
            return;
        }

        _menuManager.DisplayMenu(client, BuildMainMenu());
    }

    private void OpenSubMenu(IGameClient client, Func<IGameClient, MenuModel> menuFactory)
    {
        if (_menuManager is null)
        {
            text.Notify(client, "ws.chat.menu_unavailable");
            return;
        }

        _menuManager.DisplayMenu(client, menuFactory(client));
    }

    private MenuModel BuildMainMenu()
    {
        return MenuModel.Create()
            .Title(client => text.Get(client, "ws.menu.title"))
            .SubMenu(client => text.Get(client, "ws.menu.weapon_skins"), BuildWeaponGroupMenu)
            .SubMenu(client => text.Get(client, "ws.menu.knives"), BuildKnifeMenu)
            .SubMenu(client => text.Get(client, "ws.menu.gloves"), BuildGloveMenu)
            .SubMenu(client => text.Get(client, "ws.menu.agents"), BuildAgentMenu)
            .SubMenu(client => text.Get(client, "ws.menu.music"), BuildMusicMenu)
            .SubMenu(client => text.Get(client, "ws.menu.pins"), BuildPinMenu)
            .Item(client => text.Get(client, "ws.menu.stattrak"), controller =>
            {
                ToggleStatTrak(controller.Client);
                controller.Refresh();
            })
            .Build();
    }

    private MenuModel BuildWeaponGroupMenu(IGameClient client)
    {
        var builder = MenuModel.Create()
            .Title(_ => text.Get(client, "ws.menu.weapon_skins"));

        foreach (var group in catalog.GetWeaponPaints(client))
        {
            builder.SubMenu(group.DisplayName, _ => BuildWeaponPaintMenu(group));
        }

        return builder
            .BackItem(current => text.Get(current, "ws.menu.back"))
            .Build();
    }

    private MenuModel BuildWeaponPaintMenu(WeaponPaintCatalog group)
    {
        var builder = MenuModel.Create()
            .Title(_ => group.DisplayName)
            .Item((IGameClient client, ref MenuItemContext context) =>
            {
                var selected = playerInfo.GetPlayerWeaponSkin(client, group.ItemId) is null;
                context.Title = selected ? $"* {text.Get(client, "ws.menu.default_weapon_skin")}" : text.Get(client, "ws.menu.default_weapon_skin");
                context.Action = controller =>
                {
                    playerInfo.ClearPlayerWeaponSkin(controller.Client, group.ItemId);
                    Persist(controller.Client,
                            repository => repository.ClearWeaponCosmetic(controller.Client.SteamId, group.ItemId),
                            "clear weapon cosmetic",
                            LiveApplyTarget.Weapons,
                            current => NotifySaveState(current, LiveApplyTarget.Weapons),
                            weaponItemId: group.ItemId);
                    controller.Refresh();
                };
            });

        foreach (var paint in group.Paints)
        {
            builder.Item((IGameClient client, ref MenuItemContext context) =>
            {
                var selected = playerInfo.GetPlayerWeaponSkin(client, group.ItemId)?.PaintId == paint.PaintId;
                context.Title = selected ? $"* {paint.Name}" : paint.Name;
                context.Action = controller =>
                {
                    var current = playerInfo.GetPlayerWeaponSkin(controller.Client, group.ItemId);
                    var updated = new WeaponCosmetics
                    {
                        ItemId = group.ItemId,
                        PaintId = paint.PaintId,
                        Wear = current?.Wear ?? DefaultWear,
                        Seed = current?.Seed ?? DefaultSeed,
                        StatTrak = current?.StatTrak,
                        NameTag = current?.NameTag ?? string.Empty,
                        Stickers = current?.Stickers is { } stickers ? [.. stickers] : new Sticker?[5],
                        Keychain = current?.Keychain,
                    };

                    playerInfo.SetPlayerWeaponSkin(controller.Client, updated);
                    Persist(controller.Client,
                            repository => repository.SaveWeaponCosmetic(controller.Client.SteamId, updated),
                            "save weapon cosmetic",
                            LiveApplyTarget.Weapons,
                            current => NotifySaveState(current, LiveApplyTarget.Weapons),
                            weaponItemId: group.ItemId);
                    controller.Refresh();
                };
            });
        }

        return builder
            .BackItem(client => text.Get(client, "ws.menu.back"))
            .Build();
    }

    private MenuModel BuildKnifeMenu(IGameClient client)
    {
        var displayTeam = ResolveDisplayTeam(client);
        var builder = MenuModel.Create()
            .Title(_ => text.Get(client, "ws.menu.knives"));

        builder.Item((IGameClient current, ref MenuItemContext context) =>
        {
            var selected = playerInfo.GetPlayerKnife(current, displayTeam) is null;

            context.Title = selected ? $"* {text.Get(current, "ws.menu.default_knife")}" : text.Get(current, "ws.menu.default_knife");
            context.Action = controller =>
            {
                var targetTeams = ResolveTargetTeams(controller.Client);

                foreach (var team in targetTeams)
                {
                    playerInfo.SetPlayerKnife(controller.Client, team, null);
                }

                Persist(controller.Client,
                    async repository =>
                    {
                        foreach (var team in targetTeams)
                        {
                            await repository.ClearKnife(controller.Client.SteamId, team).ConfigureAwait(false);
                        }
                    },
                    "clear knife selection",
                    LiveApplyTarget.Weapons,
                    current => NotifySaveState(current, LiveApplyTarget.Weapons),
                    refreshKnife: true);
                controller.Refresh();
            };
        });

        foreach (var option in catalog.GetKnives(client).Where(static option => option.ItemId is not null))
        {
            builder.SubMenu((IGameClient current) =>
                {
                    var selected = playerInfo.GetPlayerKnife(current, displayTeam) == option.ItemId;
                    return selected ? $"* {option.Name}" : option.Name;
                },
                current => BuildKnifePaintMenu(current, option));
        }

        return builder
            .BackItem(current => text.Get(current, "ws.menu.back"))
            .Build();
    }

    private MenuModel BuildKnifePaintMenu(IGameClient client, KnifeOption option)
    {
        if (option.ItemId is not { } knifeItemId)
        {
            throw new InvalidOperationException("Knife submenu requires a concrete item definition.");
        }

        var builder = MenuModel.Create()
            .Title(_ => option.Name)
            .Item((IGameClient client, ref MenuItemContext context) =>
            {
                var displayTeam = ResolveDisplayTeam(client);
                var selected = playerInfo.GetPlayerKnife(client, displayTeam) == knifeItemId
                               && playerInfo.GetPlayerWeaponSkin(client, knifeItemId) is null;

                context.Title = selected
                    ? $"* {text.Get(client, "ws.menu.default_knife_finish")}"
                    : text.Get(client, "ws.menu.default_knife_finish");
                context.Action = controller =>
                {
                    var targetTeams = ResolveTargetTeams(controller.Client);

                    foreach (var team in targetTeams)
                    {
                        playerInfo.SetPlayerKnife(controller.Client, team, knifeItemId);
                    }

                    playerInfo.ClearPlayerWeaponSkin(controller.Client, knifeItemId);

                    Persist(controller.Client,
                        async repository =>
                        {
                            foreach (var team in targetTeams)
                            {
                                await repository.SaveKnife(controller.Client.SteamId, team, knifeItemId).ConfigureAwait(false);
                            }

                            await repository.ClearWeaponCosmetic(controller.Client.SteamId, knifeItemId).ConfigureAwait(false);
                        },
                        "save knife default finish",
                        LiveApplyTarget.Weapons,
                        current => NotifySaveState(current, LiveApplyTarget.Weapons),
                        refreshKnife: true);
                    controller.Refresh();
                };
            });

        foreach (var paint in catalog.GetKnifePaints(client, knifeItemId))
        {
            builder.Item((IGameClient client, ref MenuItemContext context) =>
            {
                var displayTeam = ResolveDisplayTeam(client);
                var selected = playerInfo.GetPlayerKnife(client, displayTeam) == knifeItemId
                               && playerInfo.GetPlayerWeaponSkin(client, knifeItemId)?.PaintId == paint.PaintId;

                context.Title = selected ? $"* {paint.Name}" : paint.Name;
                context.Action = controller =>
                {
                    var targetTeams = ResolveTargetTeams(controller.Client);
                    var updated = CreateOrUpdateCosmetic(controller.Client, knifeItemId, paint.PaintId);

                    foreach (var team in targetTeams)
                    {
                        playerInfo.SetPlayerKnife(controller.Client, team, knifeItemId);
                    }

                    playerInfo.SetPlayerWeaponSkin(controller.Client, updated);

                    Persist(controller.Client,
                        async repository =>
                        {
                            foreach (var team in targetTeams)
                            {
                                await repository.SaveKnife(controller.Client.SteamId, team, knifeItemId).ConfigureAwait(false);
                            }

                            await repository.SaveWeaponCosmetic(controller.Client.SteamId, updated).ConfigureAwait(false);
                        },
                        "save knife skin",
                        LiveApplyTarget.Weapons,
                        current => NotifySaveState(current, LiveApplyTarget.Weapons),
                        refreshKnife: true);
                    controller.Refresh();
                };
            });
        }

        return builder
            .BackItem(client => text.Get(client, "ws.menu.back"))
            .Build();
    }

    private MenuModel BuildGloveMenu(IGameClient client)
    {
        var displayTeam = ResolveDisplayTeam(client);
        var builder = MenuModel.Create()
            .Title(_ => text.Get(client, "ws.menu.gloves"));

        foreach (var option in catalog.GetGloves(client))
        {
            builder.Item((IGameClient current, ref MenuItemContext context) =>
            {
                var currentItem = playerInfo.GetPlayerGloves(current, displayTeam);
                var currentCosmetic = playerInfo.GetPlayerWeaponSkin(current, option.ItemId);
                var selected = (int)option.ItemId == 0
                    ? currentItem is null
                    : currentItem is not null
                      && (int)currentItem.Value == (int)option.ItemId
                      && currentCosmetic?.PaintId == option.PaintId;

                context.Title = selected ? $"* {option.Name}" : option.Name;
                context.Action = controller =>
                {
                    var targetTeams = ResolveTargetTeams(controller.Client);
                    WeaponCosmetics? cosmetics = null;

                    foreach (var team in targetTeams)
                    {
                        if ((int)option.ItemId == 0)
                        {
                            playerInfo.SetPlayerGloves(controller.Client, team, null);
                            continue;
                        }

                        cosmetics ??= CreateOrUpdateCosmetic(controller.Client, option.ItemId, option.PaintId);
                        playerInfo.SetPlayerGloves(controller.Client, team, (EconGlovesId)option.ItemId);
                        playerInfo.SetPlayerWeaponSkin(controller.Client, cosmetics);
                    }

                    Persist(controller.Client,
                            async repository =>
                            {
                                foreach (var team in targetTeams)
                                {
                                    if ((int)option.ItemId == 0)
                                    {
                                        await repository.ClearGloves(controller.Client.SteamId, team).ConfigureAwait(false);
                                        continue;
                                    }

                                    await repository.SaveGloves(controller.Client.SteamId, team, option.ItemId).ConfigureAwait(false);
                                }

                                if (cosmetics is not null)
                                {
                                    await repository.SaveWeaponCosmetic(controller.Client.SteamId, cosmetics).ConfigureAwait(false);
                                }
                            },
                            (int)option.ItemId == 0 ? "clear gloves" : "save gloves",
                            LiveApplyTarget.Gloves,
                            current => NotifySaveState(current, LiveApplyTarget.Gloves));
                    controller.Refresh();
                };
            });
        }

        return builder
            .BackItem(current => text.Get(current, "ws.menu.back"))
            .Build();
    }

    private MenuModel BuildAgentMenu(IGameClient client)
    {
        var displayTeam = ResolveDisplayTeam(client);
        var builder = MenuModel.Create()
            .Title(_ => text.Get(client, "ws.menu.agents"));

        foreach (var option in catalog.GetAgents(client, displayTeam))
        {
            builder.Item((IGameClient current, ref MenuItemContext context) =>
            {
                var selected = option.ItemId == 0
                    ? playerInfo.GetPlayerAgent(current, displayTeam) is null
                    : playerInfo.GetPlayerAgent(current, displayTeam) == option.ItemId;

                context.Title = selected ? $"* {option.Name}" : option.Name;
                context.Action = controller =>
                {
                    var targetTeams = ResolveTargetTeams(controller.Client);

                    foreach (var team in targetTeams)
                    {
                        if (option.ItemId == 0)
                        {
                            playerInfo.SetPlayerAgent(controller.Client, team, null);
                        }
                        else
                        {
                            playerInfo.SetPlayerAgent(controller.Client, team, option.ItemId);
                        }
                    }

                    Persist(controller.Client,
                            async repository =>
                            {
                                foreach (var team in targetTeams)
                                {
                                    if (option.ItemId == 0)
                                    {
                                        await repository.ClearAgent(controller.Client.SteamId, team).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        await repository.SaveAgent(controller.Client.SteamId, team, option.ItemId).ConfigureAwait(false);
                                    }
                                }
                            },
                            option.ItemId == 0 ? "clear agent" : "save agent",
                            LiveApplyTarget.Agents,
                            current => NotifySaveState(current, LiveApplyTarget.Agents));
                    controller.Refresh();
                };
            });
        }

        return builder
            .BackItem(current => text.Get(current, "ws.menu.back"))
            .Build();
    }

    private MenuModel BuildMusicMenu(IGameClient client)
    {
        var displayTeam = ResolveDisplayTeam(client);
        var builder = MenuModel.Create()
            .Title(_ => text.Get(client, "ws.menu.music"));

        foreach (var option in catalog.GetMusicKits(client))
        {
            builder.Item((IGameClient current, ref MenuItemContext context) =>
            {
                var selected = option.ItemId == 0
                    ? playerInfo.GetPlayerMusicKit(current, displayTeam) is null
                    : playerInfo.GetPlayerMusicKit(current, displayTeam) == option.ItemId;

                context.Title = selected ? $"* {option.Name}" : option.Name;
                context.Action = controller =>
                {
                    var targetTeams = ResolveTargetTeams(controller.Client);

                    foreach (var team in targetTeams)
                    {
                        if (option.ItemId == 0)
                        {
                            playerInfo.SetPlayerMusicKit(controller.Client, team, null);
                        }
                        else
                        {
                            playerInfo.SetPlayerMusicKit(controller.Client, team, option.ItemId);
                        }
                    }

                    Persist(controller.Client,
                            async repository =>
                            {
                                foreach (var team in targetTeams)
                                {
                                    if (option.ItemId == 0)
                                    {
                                        await repository.ClearMusicKit(controller.Client.SteamId, team).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        await repository.SaveMusicKit(controller.Client.SteamId, team, option.ItemId).ConfigureAwait(false);
                                    }
                                }
                            },
                            option.ItemId == 0 ? "clear music kit" : "save music kit",
                            LiveApplyTarget.MusicKit,
                            current => NotifySaveState(current, LiveApplyTarget.MusicKit));
                    controller.Refresh();
                };
            });
        }

        return builder
            .BackItem(current => text.Get(current, "ws.menu.back"))
            .Build();
    }

    private MenuModel BuildPinMenu(IGameClient client)
    {
        var displayTeam = ResolveDisplayTeam(client);
        var builder = MenuModel.Create()
            .Title(_ => text.Get(client, "ws.menu.pins"));

        foreach (var option in catalog.GetMedals(client))
        {
            builder.Item((IGameClient current, ref MenuItemContext context) =>
            {
                var selected = option.ItemId == 0
                    ? playerInfo.GetPlayerMedal(current, displayTeam) is null
                    : playerInfo.GetPlayerMedal(current, displayTeam) == option.ItemId;

                context.Title = selected ? $"* {option.Name}" : option.Name;
                context.Action = controller =>
                {
                    var targetTeams = ResolveTargetTeams(controller.Client);

                    foreach (var team in targetTeams)
                    {
                        if (option.ItemId == 0)
                        {
                            playerInfo.SetPlayerMedal(controller.Client, team, null);
                        }
                        else
                        {
                            playerInfo.SetPlayerMedal(controller.Client, team, option.ItemId);
                        }
                    }

                    Persist(controller.Client,
                            async repository =>
                            {
                                foreach (var team in targetTeams)
                                {
                                    if (option.ItemId == 0)
                                    {
                                        await repository.ClearMedal(controller.Client.SteamId, team).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        await repository.SaveMedal(controller.Client.SteamId, team, option.ItemId).ConfigureAwait(false);
                                    }
                                }
                            },
                            option.ItemId == 0 ? "clear pin" : "save pin",
                            LiveApplyTarget.Medal,
                            current => NotifySaveState(current, LiveApplyTarget.Medal));
                    controller.Refresh();
                };
            });
        }

        return builder
            .BackItem(current => text.Get(current, "ws.menu.back"))
            .Build();
    }

    private void ToggleStatTrak(IGameClient client)
    {
        if (client.GetPlayerPawn()?.GetActiveWeapon() is not { } weapon)
        {
            text.Notify(client, "ws.chat.no_active_weapon");
            return;
        }

        var itemId = (EconItemId)weapon.ItemDefinitionIndex;

        if (playerInfo.ToggleStatTrak(client, itemId) is not { } cosmetics)
        {
            text.Notify(client, "ws.chat.no_skin_selected");
            return;
        }

        Persist(client,
                repository => repository.SaveWeaponCosmetic(client.SteamId, cosmetics),
                "toggle stattrak",
                LiveApplyTarget.Weapons,
                current => text.Notify(current, cosmetics.StatTrak is null ? "ws.chat.stattrak_disabled" : "ws.chat.stattrak_enabled"),
                weaponItemId: itemId);
    }

    private WeaponCosmetics CreateOrUpdateCosmetic(IGameClient client, EconItemId itemId, ushort paintId)
    {
        var current = playerInfo.GetPlayerWeaponSkin(client, itemId);

        return new WeaponCosmetics
        {
            ItemId = itemId,
            PaintId = paintId,
            Wear = current?.Wear ?? DefaultWear,
            Seed = current?.Seed ?? DefaultSeed,
            StatTrak = current?.StatTrak,
            NameTag = current?.NameTag ?? string.Empty,
            Stickers = current?.Stickers is { } stickers ? [.. stickers] : new Sticker?[5],
            Keychain = current?.Keychain,
        };
    }

    private void Persist(
        IGameClient client,
        Func<IWeaponSkinStorage, Task> action,
        string operation,
        LiveApplyTarget liveApplyTarget,
        Action<IGameClient>? onSuccess = null,
        EconItemId? weaponItemId = null,
        bool refreshKnife = false)
    {
        var steamId = client.SteamId;

        _ = operationQueue.RunAsync(steamId, async () =>
        {
            try
            {
                await action(storage).ConfigureAwait(false);
                await SyncOriginalWeaponSkinAsync(steamId, false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to {operation}", operation);

                if (bridge.ClientManager.GetGameClient(steamId) is { } current)
                {
                    await playerInfo.RefreshInventoryAsync(current, false).ConfigureAwait(false);

                    await bridge.ModSharp.InvokeFrameActionAsync(() =>
                    {
                        if (bridge.ClientManager.GetGameClient(steamId) is { } target)
                        {
                            text.Notify(target, "ws.chat.save_failed");
                        }
                    }).ConfigureAwait(false);
                }

                return;
            }

            try
            {
                var filteredLiveApplyTarget = config.FilterLiveApplyTargets(liveApplyTarget);
                if (filteredLiveApplyTarget != LiveApplyTarget.None)
                {
                    await liveApply.ApplyAsync(steamId, filteredLiveApplyTarget, weaponItemId, refreshKnife).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Saved {operation} for {steamId}, but live apply failed", operation, steamId);
            }

            if (onSuccess is not null && bridge.ClientManager.GetGameClient(steamId) is not null)
            {
                await bridge.ModSharp.InvokeFrameActionAsync(() =>
                {
                    if (bridge.ClientManager.GetGameClient(steamId) is { } target)
                    {
                        onSuccess(target);
                    }
                }).ConfigureAwait(false);
            }
        });
    }

    private async Task SyncOriginalWeaponSkinAsync(SteamID steamId, bool refreshLocalCache)
    {
        if (!await originalRefresh.RefreshInventoryAsync(steamId).ConfigureAwait(false)
            && config.Current.UseClientCommandFallback)
        {
            await bridge.ModSharp.InvokeFrameActionAsync(() =>
            {
                if (bridge.ClientManager.GetGameClient(steamId) is { } target)
                {
                    target.FakeCommand("ws_refresh");
                }
            }).ConfigureAwait(false);
        }

        if (!refreshLocalCache)
        {
            return;
        }

        if (bridge.ClientManager.GetGameClient(steamId) is { } current)
        {
            await playerInfo.RefreshInventoryAsync(current, false).ConfigureAwait(false);
        }
    }

    private void NotifySaveState(IGameClient client, LiveApplyTarget target)
    {
        var filteredTarget = config.FilterLiveApplyTargets(target);
        text.Notify(client, filteredTarget != LiveApplyTarget.None && CanApplyImmediately(client, filteredTarget)
            ? "ws.chat.saved"
            : "ws.chat.saved_next_spawn");
    }

    private static bool CanApplyImmediately(IGameClient client, LiveApplyTarget target)
    {
        if (!target.HasFlag(LiveApplyTarget.Weapons)
            && !target.HasFlag(LiveApplyTarget.Gloves)
            && !target.HasFlag(LiveApplyTarget.Agents))
        {
            return true;
        }

        return client.GetPlayerPawn() is { IsAlive: true, Team: CStrikeTeam.CT or CStrikeTeam.TE };
    }

    private static CStrikeTeam ResolveDisplayTeam(IGameClient client)
    {
        var currentTeam = client.GetPlayerPawn()?.Team ?? client.GetPlayerController()?.PendingTeamNum;

        return currentTeam is CStrikeTeam.CT or CStrikeTeam.TE
            ? currentTeam.Value
            : CStrikeTeam.CT;
    }

    private CStrikeTeam[] ResolveTargetTeams(IGameClient client)
    {
        var currentTeam = client.GetPlayerPawn()?.Team ?? client.GetPlayerController()?.PendingTeamNum;

        return currentTeam switch
            {
                CStrikeTeam.CT => [CStrikeTeam.CT],
                CStrikeTeam.TE => [CStrikeTeam.TE],
                _ => config.Current.WriteBothTeamsWhenSpectator
                    ? [CStrikeTeam.CT, CStrikeTeam.TE]
                    : [CStrikeTeam.CT],
            };
    }

    private static bool IsValidPlayer(IGameClient client)
        => client is { IsFakeClient: false, IsConnected: true, IsInGame: true };
}
