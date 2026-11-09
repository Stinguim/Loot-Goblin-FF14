using System;
using System.Text.RegularExpressions;

using Dalamud.Game.Chat;

namespace LootGoblin.LootTracking;

/// <summary>
/// Converts loot messages into <see cref="LootChatEvent"/> instances.
/// Primary path: <see cref="ParseLogMessage"/> (language independent, by LogMessage id).
/// Fallback path: <see cref="Parse"/> (English text only).
/// </summary>
public static class LootMessageParser
{
    private const uint ItemIdModulo = 1_000_000; // strips HQ/collectable offsets

    public static LootChatEvent? ParseLogMessage(
        ILogMessage message,
        LootLogMessageDescriptor descriptor)
    {
        if (!message.TryGetIntParameter(descriptor.ItemParameterIndex, out var rawItemId) ||
            rawItemId <= 0)
        {
            return null;
        }

        var itemId = (uint)rawItemId % ItemIdModulo;

        int? rollValue = null;

        if (descriptor.Kind == LootChatEventKind.RollValue)
        {
            if (descriptor.RollType == null)
                return null;

            if (descriptor.ValueParameterIndex >= 0)
            {
                if (!message.TryGetIntParameter(descriptor.ValueParameterIndex, out var value))
                    return null;

                rollValue = value;
            }
        }

        var playerName = ResolvePlayerName(message, descriptor);

        if (string.IsNullOrEmpty(playerName))
            return null;

        return new LootChatEvent(
            descriptor.Kind,
            playerName,
            itemId,
            descriptor.RollType?.ToString(),
            rollValue);
    }

    /// <summary>
    /// Builds "Name@World" from the log message entity.
    /// Entity names are ReadOnlySeString values, so they must be converted explicitly.
    /// </summary>
    private static string ResolvePlayerName(
        ILogMessage message,
        LootLogMessageDescriptor descriptor)
    {
        var entity = descriptor.PlayerFromTarget
            ? message.TargetEntity
            : message.SourceEntity;

        if (entity == null)
            return string.Empty;

        var name = entity.Name.ToString();
        var world = entity.HomeWorld.ValueNullable?.Name.ToString();

        return string.IsNullOrEmpty(world)
            ? name
            : $"{name}@{world}";
    }

    private static readonly RegexOptions Options =
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant;

    private static readonly Regex YouRollValueRegex = new(
        @"^You roll (?<type>Need|Greed|Pass) on the .+\. (?<value>\d+)!$",
        Options);

    private static readonly Regex RollValueRegex = new(
        @"^(?<player>.+?) rolls? (?<type>Need|Greed|Pass) on the .+\. (?<value>\d+)!$",
        Options);

    private static readonly Regex YouCastLotRegex = new(
        @"^You cast your lot for the",
        Options);

    private static readonly Regex CastLotRegex = new(
        @"^(?<player>.+?) casts (his|her|their) lot for the",
        Options);

    private static readonly Regex ObtainedRegex = new(
        @"^(?<player>.+?) obtains? a",
        Options);

    public static LootChatEvent? Parse(
        string text,
        uint itemId,
        string? playerNameFromPayload)
    {
        var youRoll = YouRollValueRegex.Match(text);

        if (youRoll.Success &&
            int.TryParse(youRoll.Groups["value"].Value, out var ownValue))
        {
            return new LootChatEvent(
                LootChatEventKind.RollValue,
                "You",
                itemId,
                youRoll.Groups["type"].Value,
                ownValue);
        }

        var roll = RollValueRegex.Match(text);

        if (roll.Success &&
            int.TryParse(roll.Groups["value"].Value, out var value))
        {
            return new LootChatEvent(
                LootChatEventKind.RollValue,
                PreferPayloadName(playerNameFromPayload, roll.Groups["player"].Value),
                itemId,
                roll.Groups["type"].Value,
                value);
        }

        if (YouCastLotRegex.IsMatch(text))
        {
            return new LootChatEvent(
                LootChatEventKind.CastLot,
                "You",
                itemId,
                null,
                null);
        }

        var cast = CastLotRegex.Match(text);

        if (cast.Success)
        {
            return new LootChatEvent(
                LootChatEventKind.CastLot,
                PreferPayloadName(playerNameFromPayload, cast.Groups["player"].Value),
                itemId,
                null,
                null);
        }

        var obtain = ObtainedRegex.Match(text);

        if (obtain.Success)
        {
            var matchedPlayer = obtain.Groups["player"].Value;

            var playerName = matchedPlayer.Equals("You", StringComparison.OrdinalIgnoreCase)
                ? "You"
                : PreferPayloadName(playerNameFromPayload, matchedPlayer);

            return new LootChatEvent(
                LootChatEventKind.Obtained,
                playerName,
                itemId,
                null,
                null);
        }

        return null;
    }

    private static string PreferPayloadName(string? payloadName, string matchedName)
        => !string.IsNullOrWhiteSpace(payloadName) ? payloadName : matchedName;
}