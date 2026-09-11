using System.Collections.Concurrent;

namespace SamplePlugin.LootTracking;

public sealed class LootItem
{
    public uint ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public uint IconId { get; set; }

    /// <summary>
    /// Coleção thread-safe contendo todos os rolls efetuados para este item.
    /// </summary>
    public ConcurrentBag<LootRoll> Rolls { get; } = new();

    /// <summary>
    /// Jogadores que "cast their lot" para este item (i.e. participaram na decisão),
    /// usado para inferir quem deu Pass — o jogo não gera mensagem de chat para Pass,
    /// por isso quem participou mas nunca aparece em Rolls com Need/Greed é assumido
    /// como Pass assim que o item é obtido.
    /// </summary>
    public ConcurrentDictionary<string, byte> Participants { get; } = new();

    public string? Winner { get; set; }

    public bool IsComplete { get; set; }
}