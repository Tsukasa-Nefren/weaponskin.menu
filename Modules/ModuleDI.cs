using Microsoft.Extensions.DependencyInjection;

namespace WeaponSkin.Menu.Modules;

internal static class ModuleDI
{
    public static void AddModuleDi(this IServiceCollection services)
    {
        services.AddSingleton<IModule, MenuCommands>();
    }
}
