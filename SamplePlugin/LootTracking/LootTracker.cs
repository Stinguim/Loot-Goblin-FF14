using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SamplePlugin.LootTracking;

public sealed class LootTracker : IDisposable
{
    private const string AddonName = "NeedGreed";

    private readonly IAddonLifecycle addonLifecycle;
    private readonly IPluginLog log;

    public LootTracker(IAddonLifecycle addonLifecycle, IPluginLog log)
    {
        this.addonLifecycle = addonLifecycle;
        this.log = log;

        this.addonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, OnNeedGreedSetup);
        this.addonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, OnNeedGreedRefresh);
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(OnNeedGreedSetup, OnNeedGreedRefresh);
    }

    private unsafe void OnNeedGreedSetup(AddonEvent type, AddonArgs args)
    {
        log.Information("[LootTracker] NeedGreed appeared (PostSetup).");

        if (args is AddonSetupArgs setupArgs)
        {
            log.Information($"[LootTracker] AtkValueCount: {setupArgs.AtkValueCount}");

            var values = (AtkValue*)setupArgs.AtkValues;
            for (var i = 0; i < setupArgs.AtkValueCount; i++)
            {
                log.Information($"[LootTracker] [{i}] Type={values[i].Type} Value={values[i].GetValueAsString()}");
            }
        }
    }

    private unsafe void OnNeedGreedRefresh(AddonEvent type, AddonArgs args)
    {
        log.Information("[LootTracker] NeedGreed updated (PostRefresh).");

        if (args is AddonRefreshArgs refreshArgs)
        {
            log.Information($"[LootTracker] AtkValueCount: {refreshArgs.AtkValueCount}");

            var values = (AtkValue*)refreshArgs.AtkValues;
            for (var i = 0; i < refreshArgs.AtkValueCount; i++)
            {
                log.Information($"[LootTracker] [{i}] Type={values[i].Type} Value={values[i].GetValueAsString()}");
            }
        }
    }
}