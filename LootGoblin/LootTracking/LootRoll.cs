namespace SamplePlugin.LootTracking;

public enum RollType
{
    Need,
    Greed,
    Pass
}

public sealed class LootRoll
{
    /// <summary>
    /// Player's name (including world/server when available).
    /// Never null; defaults to an empty string.
    /// </summary>
    public string PlayerName { get; private set; } = string.Empty;

    /// <summary>
    /// The roll type performed by the player (Need, Greed, or Pass).
    /// </summary>
    public RollType Type { get; private set; }

    /// <summary>
    /// The numeric roll result (1–99).
    /// Only valid when Type = Need or Greed.
    /// Null when the player passed or when the roll has not yet occurred.
    /// </summary>
    public int? Value { get; private set; }

    /// <summary>
    /// The player's ClassJob ID (from the ClassJob sheet) at the moment of the roll.
    /// Null when the job could not be resolved.
    /// </summary>
    public uint? ClassJobId { get; private set; }

    /// <summary>
    /// Safe constructor ensuring consistent and validated roll data.
    /// </summary>
    public LootRoll(string playerName, RollType type, int? value = null, uint? classJobId = null)
    {
        PlayerName = playerName ?? string.Empty;
        Type = type;
        ClassJobId = classJobId;

        // Automatic validation of roll value
        if (type == RollType.Pass)
        {
            Value = null; // Pass rolls never have a numeric value
        }
        else
        {
            // Need/Greed → optional value, but must be valid if provided
            if (value is < 1 or > 99)
            {
                Value = null; // Invalid value → ignore
            }
            else
            {
                Value = value;
            }
        }
    }

    /// <summary>
    /// Safely updates the roll value.
    /// </summary>
    public void SetValue(int? newValue)
    {
        if (Type == RollType.Pass)
        {
            Value = null;
            return;
        }

        if (newValue is < 1 or > 99)
        {
            Value = null;
            return;
        }

        Value = newValue;
    }

    /// <summary>
    /// Safely updates the player's name.
    /// </summary>
    public void SetPlayerName(string name)
    {
        PlayerName = name ?? string.Empty;
    }

    /// <summary>
    /// Updates the ClassJobId.
    /// </summary>
    public void SetClassJob(uint? classJobId)
    {
        ClassJobId = classJobId;
    }
}
