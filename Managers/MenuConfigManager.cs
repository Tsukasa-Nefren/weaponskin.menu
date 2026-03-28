using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WeaponSkin.Menu.Managers;

internal enum MenuApplyMode
{
    Deferred,
    Live,
}

internal sealed record MenuModuleConfig(
    MenuApplyMode WeaponApplyMode,
    MenuApplyMode TeamApplyMode,
    bool UseClientCommandFallback,
    bool WriteBothTeamsWhenSpectator)
{
    public static MenuModuleConfig Default { get; } = new(
        WeaponApplyMode: MenuApplyMode.Deferred,
        TeamApplyMode: MenuApplyMode.Live,
        UseClientCommandFallback: true,
        WriteBothTeamsWhenSpectator: true);
}

internal interface IMenuConfigManager
{
    MenuModuleConfig Current { get; }

    LiveApplyTarget FilterLiveApplyTargets(LiveApplyTarget requested);
}

internal sealed class MenuConfigManager(
    InterfaceBridge bridge,
    ILogger<MenuConfigManager> logger) : IMenuConfigManager, IManager
{
    private const string ConfigFileName = "weaponskin.menu.jsonc";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public MenuModuleConfig Current { get; private set; } = MenuModuleConfig.Default;

    public bool Init()
    {
        Current = LoadConfiguration();
        logger.LogInformation(
            "WeaponSkin.Menu config loaded. WeaponApplyMode={weaponMode}, TeamApplyMode={teamMode}, UseClientCommandFallback={fallback}, WriteBothTeamsWhenSpectator={bothTeams}",
            Current.WeaponApplyMode,
            Current.TeamApplyMode,
            Current.UseClientCommandFallback,
            Current.WriteBothTeamsWhenSpectator);
        return true;
    }

    public LiveApplyTarget FilterLiveApplyTargets(LiveApplyTarget requested)
    {
        var filtered = LiveApplyTarget.None;

        if (requested.HasFlag(LiveApplyTarget.Weapons) && Current.WeaponApplyMode == MenuApplyMode.Live)
        {
            filtered |= LiveApplyTarget.Weapons;
        }

        if (Current.TeamApplyMode == MenuApplyMode.Live)
        {
            if (requested.HasFlag(LiveApplyTarget.Gloves))
            {
                filtered |= LiveApplyTarget.Gloves;
            }

            if (requested.HasFlag(LiveApplyTarget.Agents))
            {
                filtered |= LiveApplyTarget.Agents;
            }

            if (requested.HasFlag(LiveApplyTarget.MusicKit))
            {
                filtered |= LiveApplyTarget.MusicKit;
            }

            if (requested.HasFlag(LiveApplyTarget.Medal))
            {
                filtered |= LiveApplyTarget.Medal;
            }
        }

        return filtered;
    }

    private MenuModuleConfig LoadConfiguration()
    {
        var configPath = Path.Combine(bridge.SharpPath, "configs", ConfigFileName);

        if (!File.Exists(configPath))
        {
            logger.LogInformation("WeaponSkin.Menu config not found at {path}. Using defaults.", configPath);
            return MenuModuleConfig.Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath), JsonOptions);
            var root = document.RootElement;

            var apply = root.TryGetProperty("Apply", out var applyElement) ? applyElement : default;
            var sync = root.TryGetProperty("Sync", out var syncElement) ? syncElement : default;
            var selection = root.TryGetProperty("Selection", out var selectionElement) ? selectionElement : default;

            return new MenuModuleConfig(
                WeaponApplyMode: ParseApplyMode(apply, "Weapons", MenuModuleConfig.Default.WeaponApplyMode),
                TeamApplyMode: ParseApplyMode(apply, "TeamCosmetics", MenuModuleConfig.Default.TeamApplyMode),
                UseClientCommandFallback: ParseBool(sync, "UseClientCommandFallback", MenuModuleConfig.Default.UseClientCommandFallback),
                WriteBothTeamsWhenSpectator: ParseBool(selection, "WriteBothTeamsWhenSpectator", MenuModuleConfig.Default.WriteBothTeamsWhenSpectator));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read WeaponSkin.Menu config. Using defaults.");
            return MenuModuleConfig.Default;
        }
    }

    private static MenuApplyMode ParseApplyMode(JsonElement section, string propertyName, MenuApplyMode defaultValue)
    {
        if (section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return defaultValue;
        }

        return Enum.TryParse<MenuApplyMode>(property.GetString(), true, out var mode)
            ? mode
            : defaultValue;
    }

    private static bool ParseBool(JsonElement section, string propertyName, bool defaultValue)
    {
        if (section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return defaultValue;
        }

        return property.GetBoolean();
    }
}
