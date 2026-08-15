using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Folio.Models;
using Folio.Services.Market;
using Folio.Services.Persistence;

namespace Folio.Services;

public interface IWatchService
{
    IReadOnlyList<WatchedAddress> Addresses { get; }

    WatchedAddress Add(WatchChain chain, string address, string label);
    void Remove(string id);
    Task RefreshAsync();

    event EventHandler? Changed;
}

/// <summary>
/// Manages watch-only addresses and refreshes their balances from public explorers. Balances
/// are persisted (last-known) so the screen works offline. Read-only — never holds keys.
/// </summary>
public sealed class WatchService : IWatchService
{
    private readonly IWatchStore _store;
    private readonly IChainBalanceClient _client;
    private readonly List<WatchedAddress> _addresses;

    public WatchService(IWatchStore store, IChainBalanceClient client)
    {
        _store = store;
        _client = client;
        _addresses = store.Load();
    }

    public IReadOnlyList<WatchedAddress> Addresses => _addresses;

    public event EventHandler? Changed;

    public WatchedAddress Add(WatchChain chain, string address, string label)
    {
        var watched = new WatchedAddress(
            Guid.NewGuid().ToString("N"), chain, address.Trim(),
            string.IsNullOrWhiteSpace(label) ? Shorten(address) : label.Trim(),
            DateTimeOffset.Now, null, null);

        _addresses.Add(watched);
        _store.Save(_addresses);
        Changed?.Invoke(this, EventArgs.Empty);
        _ = RefreshOneAsync(watched.Id);
        return watched;
    }

    public void Remove(string id)
    {
        _addresses.RemoveAll(a => a.Id == id);
        _store.Save(_addresses);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task RefreshAsync()
    {
        var changed = false;
        for (var i = 0; i < _addresses.Count; i++)
        {
            if (await FetchAsync(i))
            {
                changed = true;
            }
        }

        if (changed)
        {
            _store.Save(_addresses);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task RefreshOneAsync(string id)
    {
        var index = _addresses.FindIndex(a => a.Id == id);
        if (index >= 0 && await FetchAsync(index))
        {
            _store.Save(_addresses);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<bool> FetchAsync(int index)
    {
        var addr = _addresses[index];
        var balance = await _client.GetBalanceAsync(addr.Chain, addr.Address);
        if (balance is null)
        {
            return false;
        }

        _addresses[index] = addr with { LastBalance = balance, BalanceAt = DateTimeOffset.Now };
        return true;
    }

    private static string Shorten(string address) =>
        address.Length <= 14 ? address : $"{address[..6]}…{address[^5..]}";
}
