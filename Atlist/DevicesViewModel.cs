using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atlist.ViewModels;

/// <summary>
/// Drives the "My devices" page.
///
/// How status checking works: the app visits each paired device in turn —
/// connect, ask "SCREEN?", read the reply, disconnect — then moves to the next.
/// One connection at a time keeps the Bluetooth code simple and reliable
/// with HM-10 modules. This runs when the page opens, when the user pulls
/// down to refresh, and every 60 seconds while the page is on screen.
/// While connected, it also delivers any messages waiting for that device.
/// </summary>
public partial class DevicesViewModel : ObservableObject
{
    private static readonly TimeSpan AutoRefreshEvery = TimeSpan.FromSeconds(60);

    private readonly DeviceLink _link;
    private readonly MessageService _messages;
    private readonly PairedDeviceStore _store;
    private CancellationTokenSource? _pageCts;
    private bool _checkInProgress;

    public DevicesViewModel(DeviceLink link, MessageService messages, PairedDeviceStore store)
    {
        _link = link;
        _messages = messages;
        _store = store;
    }

    public ObservableCollection<DeviceCard> Devices { get; } = [];

    /// <summary>Bound to the RefreshView's spinner.</summary>
    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string summaryText = string.Empty;

    [ObservableProperty]
    private bool hasNoDevices;

    // ---------- Lifecycle (called from the page) ----------

    public void Start()
    {
        Stop(); // in case OnAppearing fires twice
        LoadFromStore();

        _pageCts = new CancellationTokenSource();
        _ = AutoRefreshLoopAsync(_pageCts.Token);
    }

    public void Stop()
    {
        _pageCts?.Cancel();
        _pageCts = null;
    }

    /// <summary>
    /// Builds the card list from saved devices. Keeps existing cards (and their
    /// last known screen) so the list doesn't flicker when you come back to the page.
    /// </summary>
    private void LoadFromStore()
    {
        var saved = _store.GetAll();

        foreach (var gone in Devices.Where(c => saved.All(s => s.Id != c.Id)).ToList())
            Devices.Remove(gone);

        foreach (var info in saved)
        {
            var card = Devices.FirstOrDefault(c => c.Id == info.Id);
            if (card is null)
            {
                Devices.Add(new DeviceCard(info.Id, info.DisplayName, info.AdvertisedName, info.Location, info.LastSeen));
            }
            else
            {
                card.DisplayName = info.DisplayName;
                card.Location = info.Location;
            }
        }

        HasNoDevices = Devices.Count == 0;
        UpdateSummary();
    }

    private async Task AutoRefreshLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await CheckAllDevicesAsync(ct);
                await Task.Delay(AutoRefreshEvery, ct);
            }
        }
        catch (OperationCanceledException) { /* page closed */ }
    }

    // ---------- Checking devices ----------

    private async Task CheckAllDevicesAsync(CancellationToken ct)
    {
        if (_checkInProgress)
        {
            IsRefreshing = false; // a check is already running; its results will appear shortly
            return;
        }
        _checkInProgress = true;

        try
        {
            if (!_link.IsBluetoothOn)
            {
                SummaryText = "Bluetooth is off. Turn it on to reach your devices.";
                return;
            }

            // ToList(): copy, so a card removed mid-check doesn't break the loop.
            foreach (var card in Devices.ToList())
            {
                ct.ThrowIfCancellationRequested();
                await CheckDeviceAsync(card, ct);
                UpdateSummary();
            }
        }
        finally
        {
            _checkInProgress = false;
            IsRefreshing = false;
        }
    }

    private async Task CheckDeviceAsync(DeviceCard card, CancellationToken pageCt)
    {
        card.IsChecking = true;

        try
        {
            await _link.RunAsync(card.Id, async link =>
            {
                // Deliver first, so the screen mirror shows the message that just arrived.
                try { await _messages.DeliverWaitingOnOpenLinkAsync(link, card.Id); }
                catch { /* stays Waiting; the next check tries again */ }

                ApplyScreenReply(card, await link.AskAsync("SCREEN?"));
            }, pageCt);

            card.Reachability = DeviceReachability.InRange;
            card.LastSeen = DateTimeOffset.Now;
            _store.UpdateLastSeen(card.Id, card.LastSeen.Value);
        }
        catch (OperationCanceledException) when (pageCt.IsCancellationRequested)
        {
            throw; // page closed: stop the whole loop, don't mark the device offline
        }
        catch
        {
            // Timeout, device unplugged, too far away... all look the same to the phone.
            card.Reachability = DeviceReachability.OutOfRange;
        }
        finally
        {
            card.IsChecking = false;
        }
    }

    /// <summary>
    /// Expected reply: "SCREEN|line 1|line 2|color", e.g. "SCREEN|TAKE MEDS|With breakfast|blue".
    /// The color part is optional.
    /// </summary>
    private static void ApplyScreenReply(DeviceCard card, string reply)
    {
        var parts = reply.Split('|');
        if (parts.Length < 3 || parts[0] != "SCREEN") return; // answered, but in an unexpected format

        card.ScreenLine1 = parts[1];
        card.ScreenLine2 = parts[2];
        card.ScreenColor = LcdColors.FromName(parts.Length > 3 ? parts[3] : null);
    }

    private void UpdateSummary()
    {
        if (Devices.Count == 0)
        {
            SummaryText = "No devices yet";
            return;
        }

        var inRange = Devices.Count(d => d.IsInRange);
        SummaryText = $"{inRange} of {Devices.Count} in range";
    }

    // ---------- Commands ----------

    /// <summary>Pull-to-refresh.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_pageCts is null) { IsRefreshing = false; return; }
        IsRefreshing = true;
        try { await CheckAllDevicesAsync(_pageCts.Token); }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private Task AddDeviceAsync() => Shell.Current.GoToAsync("//connect");

    /// <summary>Switches to the Send tab with this device already chosen.</summary>
    [RelayCommand]
    private Task SendMessageAsync(DeviceCard card) =>
        Shell.Current.GoToAsync($"//send?deviceId={card.Id}");

    [RelayCommand]
    private Task OpenSettingsAsync(DeviceCard card) =>
        Shell.Current.GoToAsync($"settings?deviceId={card.Id}");
}
