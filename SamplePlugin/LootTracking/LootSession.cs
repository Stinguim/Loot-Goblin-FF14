using System.Collections.Generic;

namespace SamplePlugin.LootTracking;

/// <summary>
/// Sessão de loot atual, dividida em "baús" (cada abertura da janela NeedGreed
/// do jogo corresponde a um novo <see cref="LootChest"/>).
/// </summary>
public sealed class LootSession
{
    private readonly List<LootChest> chests = new();
    private int chestCounter;

    public IReadOnlyList<LootChest> Chests => chests;

    /// <summary>
    /// Baú atualmente "aberto" (o mais recente a receber PostSetup do addon).
    /// </summary>
    public LootChest? CurrentChest { get; private set; }

    /// <summary>
    /// Inicia um novo baú (chamado quando o addon NeedGreed abre) e passa a ser o atual.
    /// </summary>
    public LootChest StartNewChest(string dungeonName)
    {
        chestCounter++;

        var chest = new LootChest
        {
            ChestNumber = chestCounter,
            DungeonName = dungeonName
        };

        chests.Add(chest);
        CurrentChest = chest;

        return chest;
    }

    public void Clear()
    {
        chests.Clear();
        CurrentChest = null;
        chestCounter = 0;
    }
}