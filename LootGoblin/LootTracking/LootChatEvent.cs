namespace SamplePlugin.LootTracking;

public enum LootChatEventKind
{
    CastLot,
    RollValue,
    Obtained,
    Unable,
}

public sealed record LootChatEvent(
    LootChatEventKind Kind,
    string PlayerName,
    uint ItemId,
    string? RollType,
    int? RollValue);