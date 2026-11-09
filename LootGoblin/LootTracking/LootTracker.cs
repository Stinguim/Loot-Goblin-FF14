using System;
using System.Linq;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.UI;

using Lumina.Excel.Sheets;

namespace LootGoblin.LootTracking;

/// <summary>
/// Core loot-tracking engine. Monitors Need/Greed addon lifecycle events,
/// reads loot-roll log messages, and updates the active <see cref="LootSession"/>.
/// </summary>
public sealed class LootTracker : IDisposable
{
    private const string AddonName = "NeedGreed";

    private static readonly TimeSpan ChestReopenGracePeriod = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DrawRefreshInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DiagnosticTail = TimeSpan.FromSeconds(30);

    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui chatGui;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IPluginLog log;

    private bool chestIsOpen;
    private DateTime? chestClosedAt;
    private DateTime lastDrawRefresh = DateTime.MinValue;

    /// <summary>The currently active loot session.</summary>
    public LootSession CurrentSession { get; } = new();

    /// <summary>
    /// Debug aid: while a chest is open (and shortly after), logs the id and
    /// format of every game log message. Use it to fill <see cref="LootLogMessageIds"/>.
    /// </summary>
    public bool LogRawLootMessages { get; set; }

    /// <summary>Fired whenever a new chest is detected and initialized.</summary>
    public event System.Action? ChestOpened;

    public LootTracker(
        IAddonLifecycle addonLifecycle,
        IChatGui chatGui,
        IDataManager dataManager,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IPluginLog log)
    {
        this.addonLifecycle = addonLifecycle;
        this.chatGui = chatGui;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.log = log;

        addonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, OnNeedGreedSetup);
        addonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, OnNeedGreedRefresh);
        addonLifecycle.RegisterListener(AddonEvent.PostDraw, AddonName, OnNeedGreedDraw);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnNeedGreedFinalize);

        chatGui.LogMessage += OnLogMessage;
        chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, OnNeedGreedSetup);
        addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, AddonName, OnNeedGreedRefresh);
        addonLifecycle.UnregisterListener(AddonEvent.PostDraw, AddonName, OnNeedGreedDraw);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, OnNeedGreedFinalize);

        chatGui.LogMessage -= OnLogMessage;
        chatGui.ChatMessage -= OnChatMessage;
    }

    private void OnNeedGreedSetup(AddonEvent type, AddonArgs args)
    {
        var withinGracePeriod =
            chestClosedAt.HasValue &&
            DateTime.UtcNow - chestClosedAt.Value < ChestReopenGracePeriod;

        if (!chestIsOpen &&
            (CurrentSession.CurrentChest == null || !withinGracePeriod))
        {
            var dungeonName = GetCurrentDutyName();

            CurrentSession.StartNewChest(dungeonName);

            log.Information($"[LootTracker] NeedGreed opened. Chest: {dungeonName}");

            ChestOpened?.Invoke();
        }
        else if (!chestIsOpen)
        {
            log.Information(
                "[LootTracker] NeedGreed reopened quickly. Reusing previous chest.");
        }

        chestIsOpen = true;
    }

    private void OnNeedGreedFinalize(AddonEvent type, AddonArgs args)
    {
        log.Information("[LootTracker] NeedGreed closing.");

        chestIsOpen = false;
        chestClosedAt = DateTime.UtcNow;
    }

    private void OnNeedGreedDraw(AddonEvent type, AddonArgs args)
    {
        // PostDraw fires every frame; the item list does not need to be re-read that often.
        var now = DateTime.UtcNow;

        if (now - lastDrawRefresh < DrawRefreshInterval)
            return;

        lastDrawRefresh = now;

        OnNeedGreedRefresh(type, args);
    }

    private unsafe void OnNeedGreedRefresh(AddonEvent type, AddonArgs args)
    {
        if (args.Addon.Address == nint.Zero)
            return;

        var addon = (AddonNeedGreed*)args.Addon.Address;

        var count = Math.Min((int)addon->NumItems, 16);

        var chest =
            CurrentSession.CurrentChest ??
            CurrentSession.StartNewChest(GetCurrentDutyName());

        for (var i = 0; i < count; i++)
        {
            var item = addon->Items[i];

            if (item.ItemId == 0)
                continue;

            var isNewItem = !chest.Items.ContainsKey(item.ItemId);

            var trackedItem =
                chest.Items.GetOrAdd(
                    item.ItemId,
                    id => new LootItem { ItemId = id });

            trackedItem.IconId = item.IconId;

            if (string.IsNullOrEmpty(trackedItem.ItemName))
                trackedItem.ItemName = ResolveItemName(item.ItemId);

            if (isNewItem)
            {
                log.Information(
                    $"[LootTracker] New item tracked: " +
                    $"Id={item.ItemId} Name={trackedItem.ItemName}");
            }
        }
    }

    /// <summary>
    /// Language-independent path: classifies messages by LogMessage row id.
    /// </summary>
    private void OnLogMessage(ILogMessage message)
    {
        if (LogRawLootMessages && IsDiagnosticWindowOpen())
        {
            log.Information(
                $"[LootTracker][diag] id={message.LogMessageId} " +
                $"format={message.FormatLogMessageForDebugging()}");
        }

        if (!LootLogMessageIds.TryGet(message.LogMessageId, out var descriptor))
            return;

        var parsed = LootMessageParser.ParseLogMessage(message, descriptor);

        if (parsed == null)
        {
            log.Debug(
                $"[LootTracker] Could not read parameters for LogMessage {message.LogMessageId}.");

            return;
        }

        // Safety net: parameter indices are not verified, so only accept
        // events for items that are actually in the current chest.
        var chest = CurrentSession.CurrentChest;

        if (chest == null || !chest.Items.ContainsKey(parsed.ItemId))
        {
            log.Debug(
                $"[LootTracker] Ignored LogMessage {message.LogMessageId}: " +
                $"item {parsed.ItemId} is not in the current chest.");

            return;
        }

        var evt = new LootChatEvent(
            parsed.Kind,
            NormalizePlayerName(parsed.PlayerName),
            parsed.ItemId,
            parsed.RollType,
            parsed.RollValue);

        log.Debug($"[LootTracker] Parsed log message: {evt}");

        ProcessLootEvent(evt);
    }

    /// <summary>
    /// The text-based path reports the local player as "You"; keep the same
    /// convention for the log message path.
    /// </summary>
    private string NormalizePlayerName(string playerName)
    {
        var local = objectTable.LocalPlayer;

        if (local == null || string.IsNullOrEmpty(playerName))
            return playerName;

        var localName = $"{local.Name}@{local.HomeWorld.Value.Name}";

        return playerName == localName ? "You" : playerName;
    }

    /// <summary>
    /// English-only text fallback. Disabled once LootLogMessageIds is populated,
    /// so the same message is never processed twice.
    /// </summary>
    private void OnChatMessage(IHandleableChatMessage message)
    {
        var type = message.LogKind;
        var chatMessage = message.Message;

        if (type != XivChatType.LootRoll &&
            type != XivChatType.LootNotice)
        {
            return;
        }

        var itemPayload = chatMessage.Payloads.OfType<ItemPayload>().FirstOrDefault();

        if (itemPayload == null)
            return;

        var playerPayload = chatMessage.Payloads.OfType<PlayerPayload>().FirstOrDefault();

        var playerNameFromPayload = playerPayload != null
            ? $"{playerPayload.PlayerName}@{playerPayload.World.Value.Name}"
            : null;

        var text = chatMessage.TextValue;

        var evt = LootMessageParser.Parse(text, itemPayload.ItemId, playerNameFromPayload);

        if (evt == null)
        {
            log.Debug($"[LootTracker] Unrecognized loot message: {text}");
            return;
        }

        log.Debug($"[LootTracker] Parsed loot message: {evt}");

        ProcessLootEvent(evt);
    }

    private void ProcessLootEvent(LootChatEvent evt)
    {
        log.Information($"[LootTracker] Loot event received: {evt}");

        var chest =
            CurrentSession.CurrentChest ??
            CurrentSession.StartNewChest(GetCurrentDutyName());

        var item =
            chest.Items.GetOrAdd(
                evt.ItemId,
                id => new LootItem { ItemId = id });

        switch (evt.Kind)
        {
            case LootChatEventKind.RollValue:
            {
                if (evt.RollType == null ||
                    !Enum.TryParse<RollType>(evt.RollType, true, out var rollType))
                {
                    return;
                }

                var existingRoll =
                    item.Rolls.FirstOrDefault(r => r.PlayerName == evt.PlayerName);

                if (existingRoll != null)
                {
                    // Type has a private setter; only value and job can be updated.
                    existingRoll.SetValue(evt.RollValue);
                    existingRoll.SetClassJob(ResolveClassJobId(evt.PlayerName));
                }
                else
                {
                    item.Rolls.Add(
                        new LootRoll(
                            evt.PlayerName,
                            rollType,
                            evt.RollValue,
                            ResolveClassJobId(evt.PlayerName)));
                }

                break;
            }

            case LootChatEventKind.Obtained:
            {
                item.Winner = evt.PlayerName;
                item.IsComplete = true;

                InferPassForRemainingParticipants(item);

                break;
            }

            case LootChatEventKind.CastLot:
            {
                item.Participants.TryAdd(evt.PlayerName, 0);
                break;
            }

            case LootChatEventKind.Unable:
            default:
                break;
        }
    }

    private void InferPassForRemainingParticipants(LootItem item)
    {
        foreach (var participant in item.Participants.Keys)
        {
            if (item.Rolls.Any(r => r.PlayerName == participant))
                continue;

            item.Rolls.Add(
                new LootRoll(
                    participant,
                    RollType.Pass,
                    null,
                    ResolveClassJobId(participant)));
        }
    }

    private bool IsDiagnosticWindowOpen()
        => chestIsOpen ||
           (chestClosedAt.HasValue &&
            DateTime.UtcNow - chestClosedAt.Value < DiagnosticTail);

    private string GetCurrentDutyName()
    {
        try
        {
            var territorySheet = dataManager.GetExcelSheet<TerritoryType>();
            var territoryRow = territorySheet.GetRowOrDefault(clientState.TerritoryType);

            if (territoryRow == null)
                return "Unknown Location";

            var placeName = territoryRow.Value.PlaceName.ValueNullable?.Name.ToString();

            return string.IsNullOrWhiteSpace(placeName) ? "Unknown Location" : placeName;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[LootTracker] Failed to resolve current duty name.");
            return "Unknown Location";
        }
    }

    private uint? ResolveClassJobId(string playerName)
    {
        try
        {
            if (playerName == "You")
                return objectTable.LocalPlayer?.ClassJob.RowId;

            foreach (var member in partyList)
            {
                var memberName = $"{member.Name}@{member.World.Value.Name}";

                if (memberName == playerName)
                    return member.ClassJob.RowId;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"[LootTracker] Failed to resolve class job for {playerName}.");
        }

        return null;
    }

    private string ResolveItemName(uint itemId)
    {
        try
        {
            var itemSheet = dataManager.GetExcelSheet<Item>();
            var row = itemSheet.GetRowOrDefault(itemId);

            return row?.Name.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"[LootTracker] Failed to resolve item name for ItemId={itemId}.");
            return string.Empty;
        }
    }
}