using System.Reflection;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace WeaponSkin.Menu.Managers;

internal interface IOriginalWeaponSkinRefreshManager
{
    Task<bool> RefreshInventoryAsync(SteamID steamId);
}

internal sealed class OriginalWeaponSkinRefreshManager(
    InterfaceBridge bridge,
    ILogger<OriginalWeaponSkinRefreshManager> logger) : IOriginalWeaponSkinRefreshManager, IManager
{
    private const string WeaponSkinModuleName = "WeaponSkin";
    private const string ServiceProviderFieldName = "_serviceProvider";
    private const string PlayerInfoInterfaceName = "WeaponSkin.Managers.IPlayerInfoManager";
    private const string PlayerInfoConcreteName = "WeaponSkin.Managers.PlayerInfoManager";
    private const string RefreshInventoryMethodName = "RefreshInventory";
    private const string GetPlayerInventoryMethodName = "GetPlayerInventory";

    private object? _cachedPlayerInfo;
    private MethodInfo? _cachedRefreshMethod;
    private RefreshInvocationKind _cachedRefreshInvocationKind;

    public bool Init() => true;

    public void OnLibraryConnected(string name)
    {
        if (name.Equals(WeaponSkinModuleName, StringComparison.OrdinalIgnoreCase))
        {
            ClearCache();
        }
    }

    public void OnLibraryDisconnect(string name)
    {
        if (name.Equals(WeaponSkinModuleName, StringComparison.OrdinalIgnoreCase))
        {
            ClearCache();
        }
    }

    public void Shutdown()
        => ClearCache();

    public async Task<bool> RefreshInventoryAsync(SteamID steamId)
    {
        Task? refreshTask = null;

        var invoked = await bridge.ModSharp.InvokeFrameActionAsync(() =>
        {
            if (bridge.ClientManager.GetGameClient(steamId) is not { IsFakeClient: false, IsConnected: true, IsInGame: true } client)
            {
                return false;
            }

            if (!TryResolveRefreshInvoker(out var playerInfo, out var refreshMethod, out var refreshInvocationKind))
            {
                return false;
            }

            try
            {
                refreshTask = refreshMethod.Invoke(playerInfo, BuildRefreshArguments(refreshInvocationKind, client)) as Task;
                return true;
            }
            catch (TargetInvocationException ex)
            {
                ClearCache();
                logger.LogError(ex.InnerException ?? ex, "Failed to invoke WeaponSkin refresh for {steamId}", steamId);
                return false;
            }
            catch (Exception ex)
            {
                ClearCache();
                logger.LogError(ex, "Failed to invoke WeaponSkin refresh for {steamId}", steamId);
                return false;
            }
        }).ConfigureAwait(false);

        if (!invoked || refreshTask is null)
        {
            return invoked;
        }

        try
        {
            await refreshTask.ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex.GetBaseException(), "Failed to await WeaponSkin refresh for {steamId}", steamId);
            return false;
        }
    }

    private bool TryResolveRefreshInvoker(out object playerInfo, out MethodInfo refreshMethod, out RefreshInvocationKind refreshInvocationKind)
    {
        if (_cachedPlayerInfo is not null && _cachedRefreshMethod is not null)
        {
            playerInfo = _cachedPlayerInfo;
            refreshMethod = _cachedRefreshMethod;
            refreshInvocationKind = _cachedRefreshInvocationKind;
            return true;
        }

        var managerType = bridge.SharpModuleManager.GetType();
        var modulesField = managerType.GetField("_modules", BindingFlags.Instance | BindingFlags.NonPublic);

        if (modulesField?.GetValue(bridge.SharpModuleManager) is not System.Collections.IEnumerable modules)
        {
            logger.LogWarning("Failed to inspect SharpModuleManager modules while resolving WeaponSkin refresh.");
            playerInfo = null!;
            refreshMethod = null!;
            refreshInvocationKind = default;
            return false;
        }

        foreach (var module in modules)
        {
            if (module is null)
            {
                continue;
            }

            var moduleType = module.GetType();
            var moduleName = moduleType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(module) as string;

            if (!string.Equals(moduleName, WeaponSkinModuleName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var moduleInstance = moduleType.GetProperty("Instance", BindingFlags.Instance | BindingFlags.Public)?.GetValue(module);
            if (moduleInstance is null)
            {
                break;
            }

            if (TryResolvePlayerInfo(moduleInstance, out playerInfo, out refreshMethod, out refreshInvocationKind))
            {
                _cachedPlayerInfo = playerInfo;
                _cachedRefreshMethod = refreshMethod;
                _cachedRefreshInvocationKind = refreshInvocationKind;
                return true;
            }

            break;
        }

        logger.LogWarning("WeaponSkin refresh method could not be resolved from the loaded WeaponSkin module.");
        playerInfo = null!;
        refreshMethod = null!;
        refreshInvocationKind = default;
        return false;
    }

    private static bool TryResolvePlayerInfo(
        object moduleInstance,
        out object playerInfo,
        out MethodInfo refreshMethod,
        out RefreshInvocationKind refreshInvocationKind)
    {
        var moduleType = moduleInstance.GetType();
        var serviceProviderField = moduleType.GetField(ServiceProviderFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        var serviceProvider = serviceProviderField?.GetValue(moduleInstance) as IServiceProvider;

        if (serviceProvider is null)
        {
            playerInfo = null!;
            refreshMethod = null!;
            refreshInvocationKind = default;
            return false;
        }

        var assembly = moduleType.Assembly;
        var interfaceType = assembly.GetType(PlayerInfoInterfaceName, false);
        var concreteType = assembly.GetType(PlayerInfoConcreteName, false);

        playerInfo = null!;

        if (interfaceType is not null)
        {
            playerInfo = serviceProvider.GetService(interfaceType)!;
        }

        if (playerInfo is null && concreteType is not null)
        {
            playerInfo = serviceProvider.GetService(concreteType)!;
        }

        if (playerInfo is null)
        {
            refreshMethod = null!;
            refreshInvocationKind = default;
            return false;
        }

        return TryResolveRefreshMethod(playerInfo, out refreshMethod, out refreshInvocationKind);
    }

    private static bool TryResolveRefreshMethod(
        object playerInfo,
        out MethodInfo refreshMethod,
        out RefreshInvocationKind refreshInvocationKind)
    {
        var playerInfoType = playerInfo.GetType();

        refreshMethod = playerInfoType.GetMethod(
            GetPlayerInventoryMethodName,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [typeof(SteamID), typeof(bool)],
            null)!;

        if (refreshMethod is not null)
        {
            refreshInvocationKind = RefreshInvocationKind.SteamIdWithNotifyFlag;
            return true;
        }

        refreshMethod = playerInfoType.GetMethod(
            RefreshInventoryMethodName,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(IGameClient), typeof(bool)],
            null)!;

        if (refreshMethod is not null)
        {
            refreshInvocationKind = RefreshInvocationKind.ClientWithNotifyFlag;
            return true;
        }

        refreshMethod = playerInfoType.GetMethod(
            RefreshInventoryMethodName,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(IGameClient)],
            null)!;

        if (refreshMethod is not null)
        {
            refreshInvocationKind = RefreshInvocationKind.Client;
            return true;
        }

        refreshInvocationKind = default;
        return false;
    }

    private static object?[] BuildRefreshArguments(RefreshInvocationKind refreshInvocationKind, IGameClient client)
        => refreshInvocationKind switch
        {
            RefreshInvocationKind.ClientWithNotifyFlag => [client, false],
            RefreshInvocationKind.SteamIdWithNotifyFlag => [client.SteamId, false],
            _ => [client]
        };

    internal enum RefreshInvocationKind
    {
        Client = 0,
        ClientWithNotifyFlag,
        SteamIdWithNotifyFlag
    }

    private void ClearCache()
    {
        _cachedPlayerInfo = null;
        _cachedRefreshMethod = null;
        _cachedRefreshInvocationKind = default;
    }
}
