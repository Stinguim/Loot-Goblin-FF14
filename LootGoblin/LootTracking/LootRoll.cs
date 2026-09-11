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
    /// Nome do jogador (incluindo servidor se disponível).
    /// Nunca é null; default = string.Empty.
    /// </summary>
    public string PlayerName { get; private set; } = string.Empty;

    /// <summary>
    /// Tipo de roll efetuado (Need, Greed ou Pass).
    /// </summary>
    public RollType Type { get; private set; }

    /// <summary>
    /// Valor tirado nos dados (1 a 99).
    /// Só é válido quando Type = Need ou Greed.
    /// Fica null quando Pass ou quando ainda não rolou.
    /// </summary>
    public int? Value { get; private set; }

    /// <summary>
    /// ClassJob (id da sheet ClassJob) do jogador no momento do roll.
    /// Null quando não foi possível resolver.
    /// </summary>
    public uint? ClassJobId { get; private set; }

    /// <summary>
    /// Construtor seguro que garante integridade dos dados.
    /// </summary>
    public LootRoll(string playerName, RollType type, int? value = null, uint? classJobId = null)
    {
        PlayerName = playerName ?? string.Empty;
        Type = type;
        ClassJobId = classJobId;

        // Validação automática do valor
        if (type == RollType.Pass)
        {
            Value = null; // Pass nunca tem valor
        }
        else
        {
            // Need/Greed → valor opcional mas deve ser válido se existir
            if (value is < 1 or > 99)
            {
                Value = null; // valor inválido → ignora
            }
            else
            {
                Value = value;
            }
        }
    }

    /// <summary>
    /// Atualiza o valor do roll de forma segura.
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
    /// Atualiza o nome do jogador de forma segura.
    /// </summary>
    public void SetPlayerName(string name)
    {
        PlayerName = name ?? string.Empty;
    }

    /// <summary>
    /// Atualiza o ClassJobId.
    /// </summary>
    public void SetClassJob(uint? classJobId)
    {
        ClassJobId = classJobId;
    }
}
