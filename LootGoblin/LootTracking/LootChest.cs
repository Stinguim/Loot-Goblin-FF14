using System;
using System.Collections.Concurrent;

namespace SamplePlugin.LootTracking;

/// <summary>
/// Representa uma única abertura da janela de loot (Need/Greed) — normalmente
/// um baú ou drop de dungeon/trial/raid — com o nome da dungeon/zona atual
/// e os itens que apareceram nesse baú.
/// </summary>
public sealed class LootChest
{
    /// <summary>
    /// Número sequencial dentro da sessão atual (1, 2, 3, ...), usado para
    /// identificar o baú na UI ("Chest 1", "Chest 2", ...).
    /// </summary>
    public required int ChestNumber { get; init; }

    /// <summary>
    /// Nome da dungeon/trial/raid onde este baú foi aberto.
    /// </summary>
    public required string DungeonName { get; init; }

    public DateTime OpenedAt { get; init; } = DateTime.Now;

    public ConcurrentDictionary<uint, LootItem> Items { get; } = new();
}
