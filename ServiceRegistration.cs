using Microsoft.Extensions.DependencyInjection;
using WeaponSkin.Menu.Managers;
using WeaponSkin.Menu.Modules;

namespace WeaponSkin.Menu;

internal static class ServiceRegistration
{
    public static void AddServices(IServiceCollection services)
    {
        AddDualSingleton<IMenuConfigManager, IManager, MenuConfigManager>(services);
        AddDualSingleton<IPlayerOperationQueue, IManager, PlayerOperationQueue>(services);
        AddDualSingleton<IPlayerLanguageManager, IManager, PlayerLanguageManager>(services);
        AddDualSingleton<IOriginalWeaponSkinRefreshManager, IManager, OriginalWeaponSkinRefreshManager>(services);
        AddDualSingleton<ICatalogManager, IManager, CatalogManager>(services);
        AddDualSingleton<IWeaponSkinStorage, IManager, WeaponSkinStorage>(services);
        AddDualSingleton<IPlayerInfoManager, IManager, PlayerInfoManager>(services);
        AddDualSingleton<ITextManager, IManager, TextManager>(services);
        services.AddSingleton<ILiveApplyService, LiveApplyService>();
        services.AddSingleton<IModule, GloveSpawnModule>();
        services.AddSingleton<IModule, MenuCommands>();
    }

    private static void AddDualSingleton<TService1, TService2, TImpl>(IServiceCollection services)
        where TImpl : class, TService1, TService2
        where TService1 : class
        where TService2 : class
    {
        services.AddSingleton<TImpl>();
        services.AddSingleton<TService1>(provider => provider.GetRequiredService<TImpl>());
        services.AddSingleton<TService2>(provider => provider.GetRequiredService<TImpl>());
    }
}
