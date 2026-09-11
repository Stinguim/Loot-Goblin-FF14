using System;
using System.Linq;
using System.Text.RegularExpressions;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.UI;

using Lumina.Excel.Sheets;

namespace SamplePlugin.LootTracking;

public sealed class LootTracker : IDisposable
{
    private const string AddonName = "NeedGreed";

    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui chatGui;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IPluginLog log;

    public LootSession CurrentSession { get; } = new();

    private bool chestIsOpen;
    private DateTime? chestClosedAt;

    private static readonly TimeSpan ChestReopenGracePeriod =
        TimeSpan.FromSeconds(5);

    public event System.Action? ChestOpened;

    private static readonly Regex RollValueRegex = new(
        @"^(?<player>.+?) rolls? (?<type>Need|Greed|Pass) on the .+\. (?<value>\d+)!$",
        RegexOptions.Compiled);

    private static readonly Regex YouRollValueRegex = new(
        @"^You roll (?<type>Need|Greed|Pass) on the .+\. (?<value>\d+)!$",
        RegexOptions.Compiled);

    private static readonly Regex CastLotRegex = new(
        @"^(?<player>.+?) casts (his|her|their) lot for the",
        RegexOptions.Compiled);

    private static readonly Regex YouCastLotRegex = new(
        @"^You cast your lot for the",
        RegexOptions.Compiled);

    private static readonly Regex ObtainedRegex = new(
        @"^(?<player>.+?) obtains? a",
        RegexOptions.Compiled);

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

        addonLifecycle.RegisterListener(
            AddonEvent.PostSetup,
            AddonName,
            OnNeedGreedSetup);

        addonLifecycle.RegisterListener(
            AddonEvent.PostRefresh,
            AddonName,
            OnNeedGreedRefresh);

        addonLifecycle.RegisterListener(
            AddonEvent.PostDraw,
            AddonName,
            OnNeedGreedRefresh);

        addonLifecycle.RegisterListener(
            AddonEvent.PreFinalize,
            AddonName,
            OnNeedGreedFinalize);

        chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(
            AddonEvent.PostSetup,
            AddonName,
            OnNeedGreedSetup);

        addonLifecycle.UnregisterListener(
            AddonEvent.PostRefresh,
            AddonName,
            OnNeedGreedRefresh);

        addonLifecycle.UnregisterListener(
            AddonEvent.PostDraw,
            AddonName,
            OnNeedGreedRefresh);

        addonLifecycle.UnregisterListener(
            AddonEvent.PreFinalize,
            AddonName,
            OnNeedGreedFinalize);

        chatGui.ChatMessage -= OnChatMessage;
    }

    private void OnNeedGreedSetup(
        AddonEvent type,
        AddonArgs args)
    {
        var withinGracePeriod =
            chestClosedAt.HasValue &&
            DateTime.Now - chestClosedAt.Value < ChestReopenGracePeriod;

        if (!chestIsOpen &&
            (CurrentSession.CurrentChest == null ||
             !withinGracePeriod))
        {
            var dungeonName = GetCurrentDutyName();

            CurrentSession.StartNewChest(dungeonName);

            log.Information(
                $"[LootTracker] NeedGreed opened. Chest: {dungeonName}");

            ChestOpened?.Invoke();
        }
        else if (!chestIsOpen)
        {
            log.Information(
                "[LootTracker] NeedGreed reopened quickly. Reusing previous chest.");
        }

        chestIsOpen = true;
    }

    private void OnNeedGreedFinalize(
        AddonEvent type,
        AddonArgs args)
    {
        log.Information(
            "[LootTracker] NeedGreed closing.");

        chestIsOpen = false;
        chestClosedAt = DateTime.Now;
    }

    private string GetCurrentDutyName()
    {
        try
        {
            var territoryId = clientState.TerritoryType;

            var territorySheet =
                dataManager.GetExcelSheet<TerritoryType>();

            var territoryRow =
                territorySheet.GetRowOrDefault(territoryId);

            if (territoryRow == null)
                return "Unknown Location";

            var placeName =
                territoryRow.Value.PlaceName.ValueNullable?.Name.ToString();

            return string.IsNullOrWhiteSpace(placeName)
                ? "Unknown Location"
                : placeName;
        }
        catch (Exception ex)
        {
            log.Warning(
                ex,
                "[LootTracker] Failed to resolve current duty name.");

            return "Unknown Location";
        }
    }

    private unsafe void OnNeedGreedRefresh(
        AddonEvent type,
        AddonArgs args)
    {
        if (args.Addon.Address == nint.Zero)
            return;

        var addon =
            (AddonNeedGreed*)args.Addon.Address;

        var count =
            Math.Min((int)addon->NumItems, 16);

        var chest =
            CurrentSession.CurrentChest ??
            CurrentSession.StartNewChest(
                GetCurrentDutyName());

        for (var i = 0; i < count; i++)
        {
            var item = addon->Items[i];

            if (item.ItemId == 0)
                continue;

            var isNewItem =
                !chest.Items.ContainsKey(item.ItemId);

            var trackedItem =
                chest.Items.GetOrAdd(
                    item.ItemId,
                    id => new LootItem
                    {
                        ItemId = id
                    });

            trackedItem.IconId = item.IconId;

            if (string.IsNullOrEmpty(
                    trackedItem.ItemName))
            {
                trackedItem.ItemName =
                    ResolveItemName(item.ItemId);
            }

            if (isNewItem)
            {
                log.Information(
                    $"[LootTracker] New item tracked: " +
                    $"Id={item.ItemId} " +
                    $"Name={trackedItem.ItemName}");
            }
        }
    }

    private uint? ResolveClassJobId(
        string playerName)
    {
        try
        {
            if (playerName == "You")
            {
                return objectTable.LocalPlayer?
                    .ClassJob.RowId;
            }

            foreach (var member in partyList)
            {
                var memberName =
                    $"{member.Name}@{member.World.Value.Name}";

                if (memberName == playerName)
                {
                    return member.ClassJob.RowId;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(
                ex,
                $"[LootTracker] Failed to resolve class job for {playerName}.");
        }

        return null;
    }

    private string ResolveItemName(uint itemId)
    {
        try
        {
            var itemSheet =
                dataManager.GetExcelSheet<Item>();

            var row =
                itemSheet.GetRowOrDefault(itemId);

            return row?.Name.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            log.Warning(
                ex,
                $"[LootTracker] Failed to resolve item name for ItemId={itemId}.");

            return string.Empty;
        }
    }

    private void OnChatMessage(
        IHandleableChatMessage message)
    {
        var type = message.LogKind;
        var chatMessage = message.Message;

        if (type != XivChatType.LootRoll &&
            type != XivChatType.LootNotice)
        {
            return;
        }

        var itemPayload =
            chatMessage.Payloads
                .OfType<ItemPayload>()
                .FirstOrDefault();

        if (itemPayload == null)
            return;

        var playerPayload =
            chatMessage.Payloads
                .OfType<PlayerPayload>()
                .FirstOrDefault();

        var playerNameFromPayload =
            playerPayload != null
                ? $"{playerPayload.PlayerName}@{playerPayload.World.Value.Name}"
                : null;

        var text =
            chatMessage.TextValue;

        LootChatEvent? evt = null;

        // -------------------------------------------------------------
        // YOU ROLL
        // -------------------------------------------------------------

        var youRoll =
            YouRollValueRegex.Match(text);

        if (youRoll.Success)
        {
            if (!int.TryParse(
                    youRoll.Groups["value"].Value,
                    out var value))
            {
                return;
            }

            evt = new LootChatEvent(
                LootChatEventKind.RollValue,
                "You",
                itemPayload.ItemId,
                youRoll.Groups["type"].Value,
                value);
        }
        else
        {
            // ---------------------------------------------------------
            // OTHER PLAYER ROLLS
            // ---------------------------------------------------------

            var roll =
                RollValueRegex.Match(text);

            if (roll.Success)
            {
                if (!int.TryParse(
                        roll.Groups["value"].Value,
                        out var value))
                {
                    return;
                }

                var playerName =
                    playerNameFromPayload ??
                    roll.Groups["player"].Value;

                evt = new LootChatEvent(
                    LootChatEventKind.RollValue,
                    playerName,
                    itemPayload.ItemId,
                    roll.Groups["type"].Value,
                    value);
            }
            else if (YouCastLotRegex.IsMatch(text))
            {
                // -----------------------------------------------------
                // YOU CAST LOT
                // -----------------------------------------------------

                evt = new LootChatEvent(
                    LootChatEventKind.CastLot,
                    "You",
                    itemPayload.ItemId,
                    null,
                    null);
            }
            else
            {
                // -----------------------------------------------------
                // OTHER PLAYER CAST LOT
                // -----------------------------------------------------

                var cast =
                    CastLotRegex.Match(text);

                if (cast.Success)
                {
                    var playerName =
                        playerNameFromPayload ??
                        cast.Groups["player"].Value;

                    evt = new LootChatEvent(
                        LootChatEventKind.CastLot,
                        playerName,
                        itemPayload.ItemId,
                        null,
                        null);
                }
                else
                {
                    // -------------------------------------------------
                    // ITEM OBTAINED
                    // -------------------------------------------------

                    var obtain =
                        ObtainedRegex.Match(text);

                    if (obtain.Success)
                    {
                        var matchedPlayer =
                            obtain.Groups["player"].Value;

                        var playerName =
                            matchedPlayer.Equals(
                                "You",
                                StringComparison.OrdinalIgnoreCase)
                                ? "You"
                                : playerNameFromPayload ??
                                  matchedPlayer;

                        evt = new LootChatEvent(
                            LootChatEventKind.Obtained,
                            playerName,
                            itemPayload.ItemId,
                            null,
                            null);
                    }
                }
            }
        }

        if (evt == null)
            return;

        ProcessLootEvent(evt);
    }

    private void ProcessLootEvent(
        LootChatEvent evt)
    {
        log.Information(
            $"[LootTracker] Loot event received: {evt}");

        var chest =
            CurrentSession.CurrentChest ??
            CurrentSession.StartNewChest(
                GetCurrentDutyName());

        var item =
            chest.Items.GetOrAdd(
                evt.ItemId,
                id => new LootItem
                {
                    ItemId = id
                });

        switch (evt.Kind)
        {
            case LootChatEventKind.RollValue:
            {
                if (evt.RollType == null ||
                    !Enum.TryParse<RollType>(
                        evt.RollType,
                        true,
                        out var rollType))
                {
                    return;
                }

                var existingRoll =
                    item.Rolls.FirstOrDefault(
                        r => r.PlayerName == evt.PlayerName);

                if (existingRoll != null)
                {
                    // Type tem private set, portanto não o alteramos
                    // diretamente. O jogo normalmente envia o roll final
                    // uma única vez por jogador.

                    existingRoll.SetValue(
                        evt.RollValue);

                    existingRoll.SetClassJob(
                        ResolveClassJobId(
                            evt.PlayerName));
                }
                else
                {
                    item.Rolls.Add(
                        new LootRoll(
                            evt.PlayerName,
                            rollType,
                            evt.RollValue,
                            ResolveClassJobId(
                                evt.PlayerName)));
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
                item.Participants.TryAdd(
                    evt.PlayerName,
                    0);

                break;
            }

            case LootChatEventKind.Unable:
            default:
                break;
        }
    }

    private void InferPassForRemainingParticipants(
        LootItem item)
    {
        foreach (var participant in item.Participants.Keys)
        {
            var alreadyRolled =
                item.Rolls.Any(
                    r => r.PlayerName == participant);

            if (alreadyRolled)
                continue;

            item.Rolls.Add(
                new LootRoll(
                    participant,
                    RollType.Pass,
                    null,
                    ResolveClassJobId(
                        participant)));
        }
    }
}