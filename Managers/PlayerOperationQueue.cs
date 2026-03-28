using System.Collections.Concurrent;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace WeaponSkin.Menu.Managers;

internal interface IPlayerOperationQueue
{
    Task RunAsync(SteamID steamId, Func<Task> action);
}

internal sealed class PlayerOperationQueue(
    InterfaceBridge bridge) : IPlayerOperationQueue, IManager, IClientListener
{
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = [];

    public int ListenerVersion => IClientListener.ApiVersion;

    public int ListenerPriority => 0;

    public bool Init()
    {
        bridge.ClientManager.InstallClientListener(this);
        return true;
    }

    public void Shutdown()
    {
        bridge.ClientManager.RemoveClientListener(this);
        _locks.Clear();
    }

    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        _locks.TryRemove((ulong)client.SteamId, out _);
    }

    public async Task RunAsync(SteamID steamId, Func<Task> action)
    {
        var gate = _locks.GetOrAdd((ulong)steamId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);

        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
