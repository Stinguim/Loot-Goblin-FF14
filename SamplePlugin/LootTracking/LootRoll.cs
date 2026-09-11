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
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de roll efetuado (Need, Greed ou Pass).
    /// </summary>
    public RollType Type { get; set; }

    /// <summary>
    /// O valor tirado nos dados (1 a 99). Permanece null se o jogador der Pass ou ainda não rolou.
    /// </summary>
    public int? Value { get; set; }

    /// <summary>
    /// ClassJob (id da sheet ClassJob) do jogador no momento do roll, se tiver
    /// sido possível resolver via IPartyList/ClientState. Usado para desenhar
    /// o ícone da classe ao lado do nome. Fica null se não for possível resolver
    /// (ex.: jogador fora do grupo).
    /// </summary>
    public uint? ClassJobId { get; set; }
}