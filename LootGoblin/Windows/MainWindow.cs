
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;

using LootGoblin.LootTracking;

namespace LootGoblin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private (int ChestNumber, uint ItemId)? selectedKey;

    public MainWindow(Plugin plugin)
        : base(
            "Loot Results##LootViewMainWindow",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

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

        using (var child = ImRaii.Child(
            "ItemList",
            new Vector2(220, 0),
            true))
        {
            if (!child)
                return;

            // Display the most recent chest at the top.
            for (var c = session.Chests.Count - 1; c >= 0; c--)
            {
                var chest = session.Chests[c];
                var isMostRecentChest = c == session.Chests.Count - 1;
                var header = $"Chest {chest.ChestNumber} — {chest.DungeonName} ({chest.Items.Count})";

                // Only the most recent chest is expanded by default.
                // Older chests remain collapsed to keep long sessions manageable.
                var flags = isMostRecentChest
                    ? ImGuiTreeNodeFlags.DefaultOpen
                    : ImGuiTreeNodeFlags.None;

                if (ImGui.CollapsingHeader($"{header}##chestheader{chest.ChestNumber}", flags))
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

                        // The ##id suffix ensures unique identifiers when the same item
                        // appears in different chests.
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
        }

        ImGui.SameLine();

        using (var child = ImRaii.Child(
            "ItemDetails",
            new Vector2(0, 0),
            true))
        {
            if (!child)
                return;

            var selectedItem = FindSelectedItem(session);

            if (selectedItem != null)
            {
                DrawItemDetails(selectedItem);
            }
            else
            {
                ImGui.TextDisabled("Select an item to see details.");
            }
        }
    }

    private LootItem? FindSelectedItem(LootSession session)
    {
        if (selectedKey == null)
            return null;

        var chest = session.Chests.FirstOrDefault(
            c => c.ChestNumber == selectedKey.Value.ChestNumber);

        if (chest == null)
            return null;

        return chest.Items.TryGetValue(selectedKey.Value.ItemId, out var item)
            ? item
            : null;
    }

    private void DrawItemDetails(LootItem item)
    {
        var hasRollInfo = item.Rolls.Count != 0;

        // If the item has been resolved (Obtained) but no Need/Greed/Pass
        // events were recorded, display a clear message instead of empty sections.
        if (item.IsComplete && !hasRollInfo)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Not enough info, or everyone passed on this item.");
        }
        else
        {
            DrawRollSection(
                "Need:",
                item.Rolls.Where(r => r.Type == RollType.Need),
                new Vector4(0.95f, 0.35f, 0.35f, 1f),
                item.Winner);

            ImGui.Spacing();

            DrawRollSection(
                "Greed:",
                item.Rolls.Where(r => r.Type == RollType.Greed),
                new Vector4(0.4f, 0.85f, 0.4f, 1f),
                item.Winner);

            ImGui.Spacing();

            DrawRollSection(
                "Pass:",
                item.Rolls.Where(r => r.Type == RollType.Pass),
                new Vector4(0.6f, 0.6f, 0.6f, 1f),
                item.Winner);
        }

        if (item.IsComplete && item.Winner != null)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawCenteredIcon(item.IconId, new Vector2(96, 96));

            ImGui.Spacing();

            DrawCenteredText(
                $"Winner: {item.Winner}",
                new Vector4(1f, 0.85f, 0.2f, 1f));
        }
    }

    /// <summary>
    /// Draws a game icon (item or class) using its IconId.
    /// Silently fails if the IconId is 0 or the texture provider cannot load it.
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
            // If the icon fails to load, simply skip drawing it.
        }
    }

    /// <summary>
    /// Draws an icon centered horizontally within the available space.
    /// Used for the large Winner icon.
    /// </summary>
    private static void DrawCenteredIcon(uint iconId, Vector2 size)
    {
        if (iconId == 0)
            return;

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var offsetX = (availableWidth - size.X) * 0.5f;

        if (offsetX > 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        }

        DrawIcon(iconId, size);
    }

    /// <summary>
    /// Draws horizontally centered text within the available space.
    /// </summary>
    private static void DrawCenteredText(string text, Vector4 color)
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var textWidth = ImGui.CalcTextSize(text).X;
        var offsetX = (availableWidth - textWidth) * 0.5f;

        if (offsetX > 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        }

        ImGui.TextColored(color, text);
    }

    private void DrawRollSection(
        string label,
        IEnumerable<LootRoll> rollsToDisplay,
        Vector4 headerColor,
        string? winnerName)
    {
        // Sort rolls from highest to lowest.
        // Players who have not rolled yet (Value null) are placed at the bottom.
        var rolls = rollsToDisplay
            .OrderByDescending(r => r.Value ?? -1)
            .ToList();

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
            var isWinner = winnerName != null && roll.PlayerName == winnerName;

            if (roll.ClassJobId.HasValue)
            {
                DrawIcon(ClassJobIconBase + roll.ClassJobId.Value, new Vector2(16, 16));

                if (ImGui.IsItemHovered())
                {
                    var jobName = ResolveJobName(roll.ClassJobId.Value);

                    if (!string.IsNullOrEmpty(jobName))
                    {
                        ImGui.SetTooltip(jobName);
                    }
                }

                ImGui.SameLine();
            }

            var valueText = roll.Value.HasValue
                ? roll.Value.Value.ToString()
                : "-";

            if (isWinner)
            {
                ImGui.PushStyleColor(
                    ImGuiCol.Text,
                    new Vector4(1f, 0.85f, 0.2f, 1f));
            }

            ImGui.Text($"{roll.PlayerName}:");
            ImGui.SameLine();
            ImGui.Text(valueText);

            if (isWinner)
            {
                ImGui.PopStyleColor();
            }
        }

        ImGui.Unindent();
    }

    /// <summary>
    /// Resolves the ClassJob name (e.g. "Warrior") for the icon tooltip.
    /// Returns an empty string if the job cannot be resolved.
    /// </summary>
    private static string ResolveJobName(uint classJobId)
    {
        try
        {
            var row = Plugin.DataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(classJobId);
            return row?.Name.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Base offset for class/job icons in the game icon table.
    /// IconId = 62100 + ClassJobId.
    /// </summary>
    private const uint ClassJobIconBase = 62100;
}
