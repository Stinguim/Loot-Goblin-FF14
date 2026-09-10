using System;
using System.Linq;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling.Payloads;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SamplePlugin.LootTracking;

public sealed class LootTracker : IDisposable
{
    private const string AddonName = "NeedGreed";

    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;

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

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        if (chatMessage.LogKind != XivChatType.LootRoll &&
            chatMessage.LogKind != XivChatType.LootNotice)
            return;

        var hasItem = chatMessage.Message.Payloads.Any(p => p is ItemPayload);
        if (!hasItem)
            return;

        log.Information($"[LootTracker] [{chatMessage.LogKind}] {chatMessage.Message.TextValue}");
    }
}