using System.Collections.Concurrent;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;

namespace WeaponSkin.Menu.Managers;

internal interface IPlayerLanguageManager
{
    string GetCatalogLanguage(IGameClient? client);
}

internal sealed class PlayerLanguageManager(
    InterfaceBridge bridge) : IPlayerLanguageManager, IManager, IClientListener
{
    private const double InitialSnapshotRetrySeconds = 1.0;

    private static readonly IReadOnlyDictionary<string, string> SteamToCatalogLanguage =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["english"] = "en",
            ["korean"] = "ko",
            ["koreana"] = "ko",
            ["schinese"] = "zh-CN",
            ["tchinese"] = "zh-TW",
            ["japanese"] = "ja",
            ["russian"] = "ru",
            ["german"] = "de",
            ["french"] = "fr",
            ["spanish"] = "es-ES",
            ["latam"] = "es-MX",
            ["portuguese"] = "pt-PT",
            ["brazilian"] = "pt-BR",
            ["italian"] = "it",
            ["polish"] = "pl",
            ["turkish"] = "tr",
            ["dutch"] = "nl",
            ["danish"] = "da",
            ["finnish"] = "fi",
            ["norwegian"] = "no",
            ["swedish"] = "sv",
            ["czech"] = "cs",
            ["hungarian"] = "hu",
            ["romanian"] = "ro",
            ["bulgarian"] = "bg",
            ["greek"] = "el",
            ["ukrainian"] = "uk",
            ["thai"] = "th",
            ["vietnamese"] = "vi",
            ["indonesian"] = "en",
        };

    private readonly ConcurrentDictionary<ulong, string> _catalogLanguages = [];
    private bool _initialSnapshotCompleted;

    public int ListenerVersion => IClientListener.ApiVersion;

    public int ListenerPriority => 0;

    public bool Init()
    {
        bridge.ClientManager.InstallClientListener(this);
        return true;
    }

    public void OnAllModulesLoaded()
    {
        QueueInitialSnapshot();
    }

    public void Shutdown()
    {
        bridge.ClientManager.RemoveClientListener(this);
        _catalogLanguages.Clear();
    }

    public void OnClientPutInServer(IGameClient client)
    {
        QueueLanguageQuery(client);
    }

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        _catalogLanguages.TryRemove((ulong)client.SteamId, out _);
    }

    public string GetCatalogLanguage(IGameClient? client)
    {
        if (client is null)
        {
            return "en";
        }

        return _catalogLanguages.GetValueOrDefault((ulong)client.SteamId, "en");
    }

    private void QueueLanguageQuery(IGameClient client)
    {
        if (!IsValidPlayer(client))
        {
            return;
        }

        bridge.ModSharp.PushTimer(() => QueryPlayerLanguage(client), 1.0, GameTimerFlags.StopOnMapEnd);
    }

    private void QueueInitialSnapshot()
    {
        if (_initialSnapshotCompleted)
        {
            return;
        }

        try
        {
            foreach (var client in bridge.ClientManager.GetGameClients(true))
            {
                QueueLanguageQuery(client);
            }

            _initialSnapshotCompleted = true;
        }
        catch (InvalidOperationException)
        {
            bridge.ModSharp.PushTimer(QueueInitialSnapshot, InitialSnapshotRetrySeconds, GameTimerFlags.StopOnMapEnd);
        }
    }

    private void QueryPlayerLanguage(IGameClient client)
    {
        if (!IsValidPlayer(client))
        {
            return;
        }

        bridge.ClientManager.QueryConVar(client, "cl_language", OnLanguageQueryResult);
    }

    private void OnLanguageQueryResult(IGameClient client, QueryConVarValueStatus status, string name, string value)
    {
        if (status != QueryConVarValueStatus.ValueIntact || !IsValidPlayer(client))
        {
            return;
        }

        _catalogLanguages[(ulong)client.SteamId] = SteamToCatalogLanguage.GetValueOrDefault(value, "en");
    }

    private static bool IsValidPlayer(IGameClient client)
        => client is { IsValid: true, IsFakeClient: false, IsConnected: true, IsInGame: true };
}
