using System.Collections.Generic;

namespace SamplePlugin.LootTracking;

/// <summary>
/// Represents the current loot session, divided into individual "chests".
/// Each time the game's NeedGreed window opens, a new <see cref="LootChest"/>
/// is created and tracked within this session.
/// </summary>
public sealed class LootSession
{
    private readonly List<LootChest> chests = new();
    private int chestCounter;

    public IReadOnlyList<LootChest> Chests => chests;

    /// <summary>
    /// The currently active chest — the most recent one that received
    /// PostSetup from the NeedGreed addon.
/// </summary>
    public LootChest? CurrentChest { get; private set; }

    /// <summary>
    /// Creates and registers a new chest when the NeedGreed addon opens.
    /// This chest becomes the active one for incoming loot events.
/// </summary>
    public LootChest StartNewChest(string dungeonName)
    {
        chestCounter++;

        var chest = new LootChest
        {
            ChestNumber = chestCounter,
            DungeonName = dungeonName
        };

        chests.Add(chest);
        CurrentChest = chest;

        return chest;
    }

    /// <summary>
    /// Clears all tracked chests and resets the session state.
    /// </summary>
    public void Clear()
    {
        chests.Clear();
        CurrentChest = null;
        chestCounter = 0;
    }
}
