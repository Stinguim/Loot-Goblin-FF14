namespace SamplePlugin.LootTracking;

public enum LootChatEventKind
{
    /// <summary>
    /// A player declared their intent (Need/Greed/Pass) for an item.
    /// </summary>
    CastLot,

    /// <summary>
    /// A numeric roll value (1–99) was shown in chat.
    /// </summary>
    RollValue,

    /// <summary>
    /// The item was obtained by a player (final resolution).
    /// </summary>
    Obtained,

    /// <summary>
    /// The player was unable to obtain the item (inventory full, etc.).
    /// </summary>
    Unable,
}

/// <summary>
/// Represents a parsed chat event related to loot resolution.
/// Each event corresponds to a single line of chat extracted from the game.
/// </summary>
public sealed record LootChatEvent(
    LootChatEventKind Kind,

    /// <summary>
    /// Name of the player involved in the event.
    /// </summary>
    string PlayerName,

    /// <summary>
    /// ID of the item referenced by the chat message.
    /// </summary>
    uint ItemId,

    /// <summary>
    /// Roll type string as parsed from chat (e.g., "Need", "Greed", "Pass").
    /// May be null depending on the event type.
    /// </summary>
    string? RollType,

    /// <summary>
    /// Numeric roll value (1–99) when applicable.
    /// Null for events that do not include a roll number.
    /// </summary>
    int? RollValue);
