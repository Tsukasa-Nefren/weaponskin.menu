using Microsoft.Extensions.DependencyInjection;
using WeaponSkin.Menu.Persistence;

namespace WeaponSkin.Menu.Managers;

internal static class ManagerDI
{
    public static void AddManagerDi(this IServiceCollection services)
    {
        services.ImplSingleton<IMenuConfigManager, IManager, MenuConfigManager>();
        services.ImplSingleton<IPlayerOperationQueue, IManager, PlayerOperationQueue>();
        services.ImplSingleton<IPlayerLanguageManager, IManager, PlayerLanguageManager>();
        services.ImplSingleton<IOriginalWeaponSkinRefreshManager, IManager, OriginalWeaponSkinRefreshManager>();
        services.ImplSingleton<ICatalogManager, IManager, CatalogManager>();
        services.ImplSingleton<IWeaponSkinStorage, IManager, WeaponSkinStorage>();
        services.ImplSingleton<IPlayerInfoManager, IManager, PlayerInfoManager>();
        services.ImplSingleton<ITextManager, IManager, TextManager>();
        services.AddSingleton<ILiveApplyService, LiveApplyService>();
    }
}
