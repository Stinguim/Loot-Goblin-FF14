using System.Collections.Generic;

namespace SamplePlugin.LootTracking;

public enum RollType { Need, Greed, Pass }

public sealed class LootRoll
{
    public required string PlayerName { get; init; }
    public required RollType Type { get; set; }
    public int? Value { get; set; }
}

public sealed class LootItem
{
    public required uint ItemId { get; init; }
    public string ItemName { get; set; } = string.Empty;
    public uint IconId { get; set; }
    public List<LootRoll> Rolls { get; } = new();
    public string? Winner { get; set; }
    public bool IsComplete { get; set; }
}

public sealed class LootSession
{
    public Dictionary<uint, LootItem> Items { get; } = new();
}