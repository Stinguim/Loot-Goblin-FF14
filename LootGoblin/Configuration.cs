using Dalamud.Configuration;
using System;

namespace LootGoblin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary>
    /// If true, the "Loot Results" window opens automatically when a
    /// new chest is detected (PostSetup from addon NeedGreed).
    /// </summary>
    public bool AutoOpenOnNewChest { get; set; } = true;

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
