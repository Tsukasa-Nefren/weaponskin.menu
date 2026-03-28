using System.Globalization;
using Ptr.Shared.Extensions;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Objects;

namespace WeaponSkin.Menu.Managers;

internal interface ITextManager
{
    string Get(IGameClient? client, string key, params object?[] args);

    void Notify(IGameClient client, string key, params object?[] args);
}

internal sealed class TextManager(InterfaceBridge bridge) : ITextManager, IManager
{
    private const string LocaleFileName = "WeaponSkin.Menu";
    private static readonly string ChatPrefix = $" [{ChatColor.Green}WS{ChatColor.White}] ";

    private static readonly IReadOnlyDictionary<string, string> FallbackTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ws.menu.title"] = "WeaponSkin",
        ["ws.menu.weapon_skins"] = "Weapon Skins",
        ["ws.menu.default_weapon_skin"] = "Default",
        ["ws.menu.knives"] = "Knives",
        ["ws.menu.default_knife"] = "Default Knife",
        ["ws.menu.default_knife_finish"] = "Default Finish",
        ["ws.menu.gloves"] = "Gloves",
        ["ws.menu.agents"] = "Agents",
        ["ws.menu.music"] = "Music Kits",
        ["ws.menu.pins"] = "Pins",
        ["ws.menu.refresh"] = "Refresh WeaponSkin",
        ["ws.menu.stattrak"] = "Toggle StatTrak",
        ["ws.menu.back"] = "Back",
        ["ws.chat.menu_unavailable"] = "MenuManager is not loaded on this server.",
        ["ws.chat.refresh_started"] = "Refreshing WeaponSkin inventory.",
        ["ws.chat.refresh_done"] = "WeaponSkin inventory refreshed.",
        ["ws.chat.saved"] = "Selection saved.",
        ["ws.chat.saved_next_spawn"] = "Selection saved and synced with WeaponSkin. Some changes still apply on next spawn or when you receive the weapon again.",
        ["ws.chat.save_failed"] = "Failed to save your selection. Local menu data was reloaded from WeaponSkin storage.",
        ["ws.chat.no_active_weapon"] = "You need an active weapon first.",
        ["ws.chat.no_skin_selected"] = "No saved skin was found for the active weapon.",
        ["ws.chat.stattrak_enabled"] = "StatTrak enabled for the active weapon.",
        ["ws.chat.stattrak_disabled"] = "StatTrak disabled for the active weapon.",
    };

    private ILocalizerManager? _localizerManager;

    public bool Init()
    {
        EnsureLocaleFile();
        return true;
    }

    public void OnAllModulesLoaded()
    {
        ReloadLocalizer();
    }

    public void OnLibraryConnected(string name)
    {
        if (name.Contains("LocalizerManager", StringComparison.OrdinalIgnoreCase))
        {
            ReloadLocalizer();
        }
    }

    public void OnLibraryDisconnect(string name)
    {
        if (name.Contains("LocalizerManager", StringComparison.OrdinalIgnoreCase))
        {
            _localizerManager = null;
        }
    }

    public string Get(IGameClient? client, string key, params object?[] args)
    {
        if (client is not null && _localizerManager is not null)
        {
            try
            {
                if (_localizerManager.TryGetLocalizer(client, out var localizer))
                {
                    var localized = args.Length > 0
                        ? localizer.Format(key, args)
                        : localizer.TryGet(key);

                    if (!string.IsNullOrWhiteSpace(localized) && !localized.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return localized;
                    }
                }
            }
            catch
            {
            }
        }

        if (!FallbackTexts.TryGetValue(key, out var template))
        {
            template = key;
        }

        return args.Length == 0
            ? template
            : string.Format(CultureInfo.InvariantCulture, template, args);
    }

    public void Notify(IGameClient client, string key, params object?[] args)
    {
        client.PrintSayText2($"{ChatPrefix}{Get(client, key, args)}");
    }

    private void ReloadLocalizer()
    {
        _localizerManager = bridge.GetLocalizerManager();
        _localizerManager?.LoadLocaleFile(LocaleFileName, true);
    }

    private void EnsureLocaleFile()
    {
        var sourcePath = Path.Combine(bridge.ModuleDirectory, "locale", $"{LocaleFileName}.json");

        if (!File.Exists(sourcePath))
        {
            return;
        }

        var localeDirectory = Path.Combine(bridge.SharpPath, "locales");
        Directory.CreateDirectory(localeDirectory);

        var targetPath = Path.Combine(localeDirectory, $"{LocaleFileName}.json");

        if (!File.Exists(targetPath))
        {
            File.Copy(sourcePath, targetPath);
        }
    }
}
