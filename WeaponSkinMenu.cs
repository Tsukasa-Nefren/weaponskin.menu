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
        if (!RunLifecycle(_serviceProvider.GetServices<IManager>(), static service => service.Init(), "Init"))
        {
            return false;
        }

        if (!RunLifecycle(_serviceProvider.GetServices<IModule>(), static service => service.Init(), "Init"))
        {
            return false;
        }

        _serviceProvider.LoadAllSharpExtensions();
        return true;
    }

    public void PostInit()
    {
        InvokeLifecycle(_serviceProvider.GetServices<IManager>(), static service => service.OnPostInit(), "PostInit");
        InvokeLifecycle(_serviceProvider.GetServices<IModule>(), static service => service.OnPostInit(), "PostInit");
    }

    public void OnAllModulesLoaded()
    {
        InvokeLifecycle(_serviceProvider.GetServices<IManager>(), static service => service.OnAllModulesLoaded(), "OnAllModulesLoaded");
        InvokeLifecycle(_serviceProvider.GetServices<IModule>(), static service => service.OnAllModulesLoaded(), "OnAllModulesLoaded");
        _logger.LogInformation("WeaponSkin.Menu fully initialized and all modules are loaded");
    }

    public void OnLibraryConnected(string name)
    {
        InvokeLifecycle(_serviceProvider.GetServices<IManager>(), service => service.OnLibraryConnected(name), "OnLibraryConnected");
        InvokeLifecycle(_serviceProvider.GetServices<IModule>(), service => service.OnLibraryConnected(name), "OnLibraryConnected");
    }

    public void OnLibraryDisconnect(string name)
    {
        InvokeLifecycle(_serviceProvider.GetServices<IManager>(), service => service.OnLibraryDisconnect(name), "OnLibraryDisconnect");
        InvokeLifecycle(_serviceProvider.GetServices<IModule>(), service => service.OnLibraryDisconnect(name), "OnLibraryDisconnect");
    }

    public void Shutdown()
    {
        InvokeLifecycle(_serviceProvider.GetServices<IManager>(), static service => service.Shutdown(), "Shutdown");
        InvokeLifecycle(_serviceProvider.GetServices<IModule>(), static service => service.Shutdown(), "Shutdown");
        _serviceProvider.ShutdownAllSharpExtensions();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddManagerDi();
        services.AddModuleDi();
    }

    private bool RunLifecycle<TService>(
        IEnumerable<TService> services,
        Func<TService, bool> action,
        string phase)
    {
        foreach (var service in services)
        {
            try
            {
                if (action(service))
                {
                    _logger.LogInformation("{phase} succeeded for {service}", phase, service!.GetType().FullName);
                    continue;
                }

                _logger.LogError("{phase} failed for {service}", phase, service!.GetType().FullName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{phase} crashed for {service}", phase, service!.GetType().FullName);
                return false;
            }
        }

        return true;
    }

    private void InvokeLifecycle<TService>(
        IEnumerable<TService> services,
        Action<TService> action,
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
                _logger.LogError(ex, "{phase} crashed for {service}", phase, service!.GetType().FullName);
            }
        }
    }

}
