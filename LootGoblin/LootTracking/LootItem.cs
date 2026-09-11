using System.Collections.Concurrent;

namespace SamplePlugin.LootTracking;

public sealed class LootItem
{
    public uint ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public uint IconId { get; set; }

    /// <summary>
    /// Thread‑safe collection containing all rolls made for this item.
    /// Each entry represents a player's Need/Greed/Pass decision.
    /// </summary>
    public ConcurrentBag<LootRoll> Rolls { get; } = new();

    /// <summary>
    /// Set of players who participated in the loot decision for this item.
    /// Used to infer Pass results, since the game does not emit chat messages
    /// for Pass. Any participant who never appears in <see cref="Rolls"/>
    /// with Need or Greed is assumed to have passed once the item is resolved.
    /// </summary>
    public ConcurrentDictionary<string, byte> Participants { get; } = new();

    /// <summary>
    /// The name of the player who won the item, once determined.
    /// Null until the roll is fully resolved.
    /// </summary>
    public string? Winner { get; set; }

    /// <summary>
    /// Indicates whether all rolls for this item have been finalized
    /// and the winner is known.
    /// </summary>
    public bool IsComplete { get; set; }
}
