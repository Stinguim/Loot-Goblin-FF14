using System.Collections.Generic;

namespace LootGoblin.LootTracking;

/// <summary>
/// Describes how a game LogMessage row maps to a loot event.
/// Parameter indices refer to the integer parameters of the log message.
/// A negative ValueParameterIndex means the message carries no roll value.
/// </summary>
public readonly record struct LootLogMessageDescriptor(
    LootChatEventKind Kind,
    RollType? RollType,
    int ItemParameterIndex,
    int ValueParameterIndex,
    bool PlayerFromTarget = false);

/// <summary>
/// Language-independent loot message table, keyed by LogMessage row id.
/// Row ids are identical in every client language, so no per-language text is needed.
/// </summary>
public static class LootLogMessageIds
{
    private static readonly Dictionary<uint, LootLogMessageDescriptor> Map = new()
    {
        // Row 5180: "You cast your lot for {item}." (same row for every player;
        // the subject comes from the source entity).
        // ItemParameterIndex is UNVERIFIED: the public data dump strips macros.
        // A wrong index is harmless, the event is simply ignored.
        [5180] = new(
            LootChatEventKind.CastLot,
            null,
            ItemParameterIndex: 0,
            ValueParameterIndex: -1),

        // Row 1231: "You roll {type} on {item}. {value}!" (one row for all roll types).
        // Disabled on purpose: the roll type is a parameter and its encoding
        // (and the parameter indices) are not verified yet.
        // [1231] = new(LootChatEventKind.RollValue, <type from parameter>, ...),

        // Obtained-item rows (62 = LootNotice) are not identified yet.
    };

    public static bool TryGet(uint logMessageId, out LootLogMessageDescriptor descriptor)
        => Map.TryGetValue(logMessageId, out descriptor);
}