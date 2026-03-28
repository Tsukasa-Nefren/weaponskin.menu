using Microsoft.Extensions.DependencyInjection;

namespace WeaponSkin.Menu;

internal static class DependencyInjections
{
    public static void ImplSingleton<TService1, TService2, TImpl>(this IServiceCollection services)
        where TImpl : class, TService1, TService2
        where TService1 : class
        where TService2 : class
    {
        services.AddSingleton<TImpl>();

        services.AddSingleton<TService1>(x => x.GetRequiredService<TImpl>());
        services.AddSingleton<TService2>(x => x.GetRequiredService<TImpl>());
    }
}
