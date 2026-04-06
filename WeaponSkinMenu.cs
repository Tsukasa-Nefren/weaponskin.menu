using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sharp.Extensions.CommandManager;
using Sharp.Shared;
using Sharp.Shared.Abstractions;
using WeaponSkin.Menu.Managers;
using WeaponSkin.Menu.Modules;

[assembly: DisableRuntimeMarshalling]

namespace WeaponSkin.Menu;

public sealed class WeaponSkinMenu : IModSharpModule
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<WeaponSkinMenu> _logger;

    public WeaponSkinMenu(
        ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version version,
        IConfiguration coreConfiguration,
        bool hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharedSystem);

        var loggerFactory = sharedSystem.GetLoggerFactory();
        _logger = loggerFactory.CreateLogger<WeaponSkinMenu>();

        var bridge = new InterfaceBridge(
            dllPath,
            sharpPath,
            version,
            sharedSystem,
            hotReload,
            sharedSystem.GetModSharp().HasCommandLine("-debug"));
        var services = new ServiceCollection();
        services.AddSingleton(bridge);
        services.AddSingleton(loggerFactory);
        services.AddSingleton(sharedSystem);
        services.AddLogging();
        services.AddCommandManager(sharedSystem);

        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    string IModSharpModule.DisplayName => "WeaponSkin.Menu";

    string IModSharpModule.DisplayAuthor => "Tsukasa";

    public bool Init()
    {
        var managers = _serviceProvider.GetServices<IManager>().ToArray();
        var modules = _serviceProvider.GetServices<IModule>().ToArray();

        if (!LifecycleRunner.Run(managers, static service => service.Init(), _logger, "Init"))
        {
            return false;
        }

        if (!LifecycleRunner.Run(modules, static service => service.Init(), _logger, "Init"))
        {
            return false;
        }

        _serviceProvider.LoadAllSharpExtensions();
        _logger.LogInformation(
            "WeaponSkin.Menu initialized {managerCount} managers and {moduleCount} modules",
            managers.Length,
            modules.Length);
        return true;
    }

    public void PostInit()
    {
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IManager>(), static service => service.OnPostInit(), _logger, "PostInit");
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IModule>(), static service => service.OnPostInit(), _logger, "PostInit");
    }

    public void OnAllModulesLoaded()
    {
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IManager>(), static service => service.OnAllModulesLoaded(), _logger, "OnAllModulesLoaded");
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IModule>(), static service => service.OnAllModulesLoaded(), _logger, "OnAllModulesLoaded");
        _logger.LogDebug("WeaponSkin.Menu fully initialized and all modules are loaded");
    }

    public void OnLibraryConnected(string name)
    {
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IManager>(), service => service.OnLibraryConnected(name), _logger, "OnLibraryConnected");
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IModule>(), service => service.OnLibraryConnected(name), _logger, "OnLibraryConnected");
    }

    public void OnLibraryDisconnect(string name)
    {
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IManager>(), service => service.OnLibraryDisconnect(name), _logger, "OnLibraryDisconnect");
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IModule>(), service => service.OnLibraryDisconnect(name), _logger, "OnLibraryDisconnect");
    }

    public void Shutdown()
    {
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IManager>(), static service => service.Shutdown(), _logger, "Shutdown");
        LifecycleRunner.Invoke(_serviceProvider.GetServices<IModule>(), static service => service.Shutdown(), _logger, "Shutdown");
        _serviceProvider.ShutdownAllSharpExtensions();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        ServiceRegistration.AddServices(services);
    }

    private static class LifecycleRunner
    {
        public static bool Run<TService>(
            IEnumerable<TService> services,
            Func<TService, bool> action,
            ILogger logger,
            string phase)
        {
            foreach (var service in services)
            {
                try
                {
                    if (action(service))
                    {
                        continue;
                    }

                    logger.LogError("{phase} failed for {service}", phase, service!.GetType().FullName);
                    return false;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{phase} crashed for {service}", phase, service!.GetType().FullName);
                    return false;
                }
            }

            return true;
        }

        public static void Invoke<TService>(
            IEnumerable<TService> services,
            Action<TService> action,
            ILogger logger,
            string phase)
        {
            foreach (var service in services)
            {
                try
                {
                    action(service);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{phase} crashed for {service}", phase, service!.GetType().FullName);
                }
            }
        }
    }
}
