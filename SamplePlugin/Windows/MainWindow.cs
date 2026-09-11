using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

using SamplePlugin.LootTracking;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;
    private (int ChestNumber, uint ItemId)? selectedKey;

    public MainWindow(Plugin plugin, string goatImagePath)
        : base(
            "Loot Results##LootViewMainWindow",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var session = plugin.LootTracker.CurrentSession;

        if (ImGui.Button("Clear"))
        {
            session.Clear();
            selectedKey = null;
        }

        ImGui.Separator();

        if (session.Chests.Count == 0)
        {
            ImGui.TextDisabled("No loot items to showcase.");
            return;
        }

        ImGui.BeginChild(
            "ItemList",
            new Vector2(220, 0),
            true);

        // Mostra o baú mais recente no topo.
        for (var c = session.Chests.Count - 1; c >= 0; c--)
        {
            var chest = session.Chests[c];
            var header = $"Chest {chest.ChestNumber} — {chest.DungeonName}";

            if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                if (chest.Items.IsEmpty)
                {
                    ImGui.TextDisabled("Waiting for items...");
                }

                foreach (var item in chest.Items.Values)
                {
                    var displayName = string.IsNullOrEmpty(item.ItemName)
                        ? $"Item {item.ItemId}"
                        : item.ItemName;

                    var key = (chest.ChestNumber, item.ItemId);
                    var isSelected = selectedKey == key;

                    if (isSelected)
                    {
                        ImGui.PushStyleColor(
                            ImGuiCol.Text,
                            new Vector4(0.4f, 1f, 0.4f, 1f));
                    }

                    // ##id garante um identificador único mesmo que o mesmo item
                    // apareça em baús diferentes.
                    if (ImGui.Selectable($"{displayName}##{chest.ChestNumber}_{item.ItemId}", isSelected))
                    {
                        selectedKey = key;
                    }

                    if (isSelected)
                    {
                        ImGui.PopStyleColor();
                    }
                }

                ImGui.Unindent();
            }
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild(
            "ItemDetails",
            new Vector2(0, 0),
            true);

        var selectedItem = FindSelectedItem(session);

        if (selectedItem != null)
        {
            DrawItemDetails(selectedItem);
        }
        else
        {
            ImGui.TextDisabled("Select an item to see details.");
        }

        ImGui.EndChild();
    }

    private LootItem? FindSelectedItem(LootSession session)
    {
        if (selectedKey == null)
            return null;

        var chest = session.Chests.FirstOrDefault(c => c.ChestNumber == selectedKey.Value.ChestNumber);

        if (chest == null)
            return null;

        return chest.Items.TryGetValue(selectedKey.Value.ItemId, out var item) ? item : null;
    }

    private void DrawItemDetails(LootItem item)
    {
        DrawRollSection(
            "Need:",
            item.Rolls.Where(r => r.Type == RollType.Need),
            new Vector4(0.95f, 0.35f, 0.35f, 1f));

        ImGui.Spacing();

        DrawRollSection(
            "Greed:",
            item.Rolls.Where(r => r.Type == RollType.Greed),
            new Vector4(0.4f, 0.85f, 0.4f, 1f));

        ImGui.Spacing();

        DrawRollSection(
            "Pass:",
            item.Rolls.Where(r => r.Type == RollType.Pass),
            new Vector4(0.6f, 0.6f, 0.6f, 1f));

        if (item.IsComplete && item.Winner != null)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawIcon(item.IconId, new Vector2(40, 40));

            ImGui.TextColored(
                new Vector4(1f, 0.85f, 0.2f, 1f),
                $"Winner: {item.Winner}");
        }
    }

    /// <summary>
    /// Desenha um ícone do jogo (item ou classe) a partir do IconId. Falha em
    /// silêncio se o IconId for 0 ou o texture provider não conseguir carregar.
    /// </summary>
    private static void DrawIcon(uint iconId, Vector2 size)
    {
        if (iconId == 0)
            return;

        try
        {
            var icon = Plugin.TextureProvider
                .GetFromGameIcon(new GameIconLookup(iconId))
                .GetWrapOrEmpty();

            ImGui.Image(icon.Handle, size);
        }
        catch
        {
            // Se o ícone não carregar por qualquer razão, simplesmente não desenha nada.
        }
    }

    private void DrawRollSection(
        string label,
        IEnumerable<LootRoll> rollsToDisplay,
        Vector4 headerColor)
    {
        var rolls = rollsToDisplay.ToList();

        ImGui.TextColored(headerColor, label);

        if (rolls.Count == 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("—");
            return;
        }

        ImGui.Indent();

        foreach (var roll in rolls)
        {
            if (roll.ClassJobId.HasValue)
            {
                DrawIcon(ClassJobIconBase + roll.ClassJobId.Value, new Vector2(16, 16));
                ImGui.SameLine();
            }

            var valueText = roll.Value.HasValue
                ? roll.Value.Value.ToString()
                : "-";

            ImGui.Text($"{roll.PlayerName}:");
            ImGui.SameLine();
            ImGui.Text(valueText);
        }

        ImGui.Unindent();
    }

    /// <summary>
    /// Offset dos ícones de classe/job na tabela de ícones do jogo
    /// (IconId = 62100 + ClassJobId).
    /// </summary>
    private const uint ClassJobIconBase = 62100;
}