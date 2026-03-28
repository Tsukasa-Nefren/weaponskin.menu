using Microsoft.Extensions.Logging;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace WeaponSkin.Menu;

internal interface IModule
{
    bool Init();

    void OnPostInit()
    {
    }

    void OnAllModulesLoaded()
    {
    }

    void OnLibraryConnected(string name)
    {
    }

    void OnLibraryDisconnect(string name)
    {
    }

    void Shutdown()
    {
    }
}

internal interface IManager
{
    bool Init();

    void OnPostInit()
    {
    }

    void OnAllModulesLoaded()
    {
    }

    void OnLibraryConnected(string name)
    {
    }

    void OnLibraryDisconnect(string name)
    {
    }

    void Shutdown()
    {
    }
}

internal sealed class InterfaceBridge
{
    private readonly ISharedSystem _sharedSystem;

    public InterfaceBridge(
        string dllPath,
        string sharpPath,
        Version version,
        ISharedSystem sharedSystem,
        bool hotReload,
        bool debug)
    {
        DllPath = dllPath;
        SharpPath = sharpPath;
        Version = version;
        _sharedSystem = sharedSystem;
        SharedSystem = sharedSystem;
        HotReload = hotReload;
        Debug = debug;

        ModSharp = sharedSystem.GetModSharp();
        ConVarManager = sharedSystem.GetConVarManager();
        EventManager = sharedSystem.GetEventManager();
        ClientManager = sharedSystem.GetClientManager();
        EntityManager = sharedSystem.GetEntityManager();
        FileManager = sharedSystem.GetFileManager();
        HookManager = sharedSystem.GetHookManager();
        SchemaManager = sharedSystem.GetSchemaManager();
        TransmitManager = sharedSystem.GetTransmitManager();
        SteamAPi = ModSharp.GetSteamGameServer();
        ModuleManager = sharedSystem.GetLibraryModuleManager();
        EconItemManager = sharedSystem.GetEconItemManager();
        PhysicsQueryManager = sharedSystem.GetPhysicsQueryManager();
        SoundManager = sharedSystem.GetSoundManager();
        SharpModuleManager = sharedSystem.GetSharpModuleManager();
    }

    public string DllPath { get; }

    public string SharpPath { get; }

    public string ModuleDirectory
        => Directory.Exists(DllPath)
            ? DllPath
            : Path.GetDirectoryName(DllPath) ?? DllPath;

    public Version Version { get; }

    public bool HotReload { get; }

    public bool Debug { get; }

    public ISharedSystem SharedSystem { get; }

    public IModSharp ModSharp { get; }

    public IConVarManager ConVarManager { get; }

    public IEventManager EventManager { get; }

    public IClientManager ClientManager { get; }

    public IEntityManager EntityManager { get; }

    public IEconItemManager EconItemManager { get; }

    public IFileManager FileManager { get; }

    public IHookManager HookManager { get; }

    public ISchemaManager SchemaManager { get; }

    public ITransmitManager TransmitManager { get; }

    public ISteamApi SteamAPi { get; }

    public IPhysicsQueryManager PhysicsQueryManager { get; }

    public ISoundManager SoundManager { get; }

    public ILibraryModuleManager ModuleManager { get; }

    public ISharpModuleManager SharpModuleManager { get; }

    public IGameRules GameRules => ModSharp.GetGameRules();

    public IGlobalVars GlobalVars => ModSharp.GetGlobals();

    public INetworkServer Server => ModSharp.GetIServer();

    public ILoggerFactory LoggerFactory => _sharedSystem.GetLoggerFactory();

    public IMenuManager? GetMenuManager()
    {
        try
        {
            return SharpModuleManager.GetRequiredSharpModuleInterface<IMenuManager>(IMenuManager.Identity).Instance;
        }
        catch
        {
            return null;
        }
    }

    public ILocalizerManager? GetLocalizerManager()
    {
        try
        {
            return SharpModuleManager.GetRequiredSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity).Instance;
        }
        catch
        {
            return null;
        }
    }
}
