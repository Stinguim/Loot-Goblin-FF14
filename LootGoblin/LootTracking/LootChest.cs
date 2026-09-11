using System;
using System.Collections.Concurrent;

namespace SamplePlugin.LootTracking;

/// <summary>
/// Represents a single instance of the Need/Greed loot window — typically
/// corresponding to a chest or drop from a dungeon, trial, or raid.
/// Stores the zone/dungeon name and all items that appeared in this chest.
/// </summary>
public sealed class LootChest
{
    /// <summary>
    /// Sequential number within the current session (1, 2, 3, ...),
    /// used to identify the chest in the UI ("Chest 1", "Chest 2", etc.).
    /// </summary>
    public required int ChestNumber { get; init; }

    /// <summary>
    /// Name of the dungeon, trial, or raid where this chest was opened.
    /// </summary>
    public required string DungeonName { get; init; }

    /// <summary>
    /// Timestamp indicating when the chest was opened.
    /// Defaults to the current time.
    /// </summary>
    public DateTime OpenedAt { get; init; } = DateTime.Now;

    /// <summary>
    /// Thread‑safe collection of all items contained in this chest,
    /// keyed by their ItemId.
    /// </summary>
    public ConcurrentDictionary<uint, LootItem> Items { get; } = new();
}
