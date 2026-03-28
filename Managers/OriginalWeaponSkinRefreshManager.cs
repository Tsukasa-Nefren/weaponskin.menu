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

    private object? _cachedPlayerInfo;
    private MethodInfo? _cachedRefreshMethod;

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

    public Task<bool> RefreshInventoryAsync(SteamID steamId)
        => bridge.ModSharp.InvokeFrameActionAsync(() =>
        {
            if (bridge.ClientManager.GetGameClient(steamId) is not { IsFakeClient: false, IsConnected: true, IsInGame: true } client)
            {
                return false;
            }

            if (!TryResolveRefreshInvoker(out var playerInfo, out var refreshMethod))
            {
                return false;
            }

            try
            {
                refreshMethod.Invoke(playerInfo, [client]);
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
        });

    private bool TryResolveRefreshInvoker(out object playerInfo, out MethodInfo refreshMethod)
    {
        if (_cachedPlayerInfo is not null && _cachedRefreshMethod is not null)
        {
            playerInfo = _cachedPlayerInfo;
            refreshMethod = _cachedRefreshMethod;
            return true;
        }

        var managerType = bridge.SharpModuleManager.GetType();
        var modulesField = managerType.GetField("_modules", BindingFlags.Instance | BindingFlags.NonPublic);

        if (modulesField?.GetValue(bridge.SharpModuleManager) is not System.Collections.IEnumerable modules)
        {
            logger.LogWarning("Failed to inspect SharpModuleManager modules while resolving WeaponSkin refresh.");
            playerInfo = null!;
            refreshMethod = null!;
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

            if (TryResolvePlayerInfo(moduleInstance, out playerInfo, out refreshMethod))
            {
                _cachedPlayerInfo = playerInfo;
                _cachedRefreshMethod = refreshMethod;
                return true;
            }

            break;
        }

        logger.LogWarning("WeaponSkin refresh method could not be resolved from the loaded WeaponSkin module.");
        playerInfo = null!;
        refreshMethod = null!;
        return false;
    }

    private static bool TryResolvePlayerInfo(object moduleInstance, out object playerInfo, out MethodInfo refreshMethod)
    {
        var moduleType = moduleInstance.GetType();
        var serviceProviderField = moduleType.GetField(ServiceProviderFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        var serviceProvider = serviceProviderField?.GetValue(moduleInstance) as IServiceProvider;

        if (serviceProvider is null)
        {
            playerInfo = null!;
            refreshMethod = null!;
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
            return false;
        }

        refreshMethod = playerInfo.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
            {
                if (!method.Name.Equals(RefreshInventoryMethodName, StringComparison.Ordinal))
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(IGameClient);
            })!;

        return refreshMethod is not null;
    }

    private void ClearCache()
    {
        _cachedPlayerInfo = null;
        _cachedRefreshMethod = null;
    }
}
