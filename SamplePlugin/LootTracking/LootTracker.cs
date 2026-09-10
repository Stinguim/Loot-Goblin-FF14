using System;
using System.Linq;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text.RegularExpressions;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SamplePlugin.LootTracking;

public sealed class LootTracker : IDisposable
{
    private const string AddonName = "NeedGreed";

    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    public LootSession CurrentSession { get; } = new();

    public LootTracker(
        IAddonLifecycle addonLifecycle,
        IChatGui chatGui,
        IPluginLog log)
    {
        this.addonLifecycle = addonLifecycle;
        this.chatGui = chatGui;
        this.log = log;

        this.addonLifecycle.RegisterListener(
            AddonEvent.PostSetup,
            AddonName,
            OnNeedGreedSetup);

        this.addonLifecycle.RegisterListener(
            AddonEvent.PostRefresh,
            AddonName,
            OnNeedGreedRefresh);

        this.chatGui.ChatMessage += OnChatMessage;
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

        chatGui.ChatMessage -= OnChatMessage;
    }

    private void OnNeedGreedSetup(AddonEvent type, AddonArgs args)
    {
        log.Information("[LootTracker] NeedGreed appeared (PostSetup).");
    }

    private unsafe void OnNeedGreedRefresh(
        AddonEvent type,
        AddonArgs args)
    {
        if (args.Addon.Address == nint.Zero)
            return;

        var addon = (AddonNeedGreed*)args.Addon.Address;

        if (!addon->IsReady)
            return;

        log.Information(
            $"[LootTracker] NumItems: {addon->NumItems}");

        var count = Math.Min(
            addon->NumItems,
            addon->Items.Length);

        for (var i = 0; i < count; i++)
        {
            ref var item = ref addon->Items[i];

            if (item.ItemId == 0)
                continue;

            log.Information(
                $"[LootTracker] " +
                $"Item[{i}] " +
                $"Id={item.ItemId} " +
                $"Count={item.ItemCount} " +
                $"Roll={item.Roll} " +
                $"Name={item.ItemName}");
        }
    }

private static readonly Regex CastLotRegex = new(@"^(?<player>.+?) casts (his|her) lot for the", RegexOptions.Compiled);
private static readonly Regex YouCastLotRegex = new(@"^You cast your lot for the", RegexOptions.Compiled);
private static readonly Regex RollValueRegex = new(@"^(?<player>.+?) rolls? (?<type>Need|Greed) on the .+\. (?<value>\d+)!$", RegexOptions.Compiled);
private static readonly Regex YouRollValueRegex = new(@"^You roll (?<type>Need|Greed) on the .+\. (?<value>\d+)!$", RegexOptions.Compiled);
private static readonly Regex ObtainedRegex = new(@"^(?<player>.+?) obtains? a", RegexOptions.Compiled);
    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        if (chatMessage.LogKind != XivChatType.LootRoll && chatMessage.LogKind != XivChatType.LootNotice)
            return;

        var itemPayload = chatMessage.Message.Payloads.OfType<ItemPayload>().FirstOrDefault();
        if (itemPayload == null)
            return;

        var text = chatMessage.Message.TextValue;
        LootChatEvent? evt = null;

        if (YouRollValueRegex.Match(text) is { Success: true } youRoll)
            evt = new LootChatEvent(LootChatEventKind.RollValue, "You", itemPayload.ItemId, youRoll.Groups["type"].Value, int.Parse(youRoll.Groups["value"].Value));
        else if (RollValueRegex.Match(text) is { Success: true } roll)
            evt = new LootChatEvent(LootChatEventKind.RollValue, roll.Groups["player"].Value, itemPayload.ItemId, roll.Groups["type"].Value, int.Parse(roll.Groups["value"].Value));
        else if (YouCastLotRegex.IsMatch(text))
            evt = new LootChatEvent(LootChatEventKind.CastLot, "You", itemPayload.ItemId, null, null);
        else if (CastLotRegex.Match(text) is { Success: true } cast)
            evt = new LootChatEvent(LootChatEventKind.CastLot, cast.Groups["player"].Value, itemPayload.ItemId, null, null);
        else if (ObtainedRegex.Match(text) is { Success: true } obtain)
            evt = new LootChatEvent(LootChatEventKind.Obtained, obtain.Groups["player"].Value, itemPayload.ItemId, null, null);

        if (evt == null)
            return;

        if (!CurrentSession.Items.TryGetValue(evt.ItemId, out var item))
        {
            item = new LootItem { ItemId = evt.ItemId };
            CurrentSession.Items[evt.ItemId] = item;
        }

        switch (evt.Kind)
        {
            case LootChatEventKind.RollValue:
                var roll = item.Rolls.Find(r => r.PlayerName == evt.PlayerName);
                if (roll == null)
                {
                    roll = new LootRoll { PlayerName = evt.PlayerName, Type = evt.RollType == "Need" ? RollType.Need : RollType.Greed };
                    item.Rolls.Add(roll);
                }
                roll.Value = evt.RollValue;
                break;

            case LootChatEventKind.Obtained:
                item.Winner = evt.PlayerName;
                item.IsComplete = true;
                break;
        }

        log.Information($"[LootTracker] Session now has {CurrentSession.Items.Count} item(s) tracked.");
            }
}