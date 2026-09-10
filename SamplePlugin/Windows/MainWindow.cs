using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("Loot Results##LootViewMainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var session = plugin.LootTracker.CurrentSession;

        if (session.Items.Count == 0)
        {
            ImGui.TextDisabled("No loot items to showcase.");
            return;
        }

        foreach (var item in session.Items.Values)
        {
            ImGui.Text($"Item {item.ItemId}");
            ImGui.Separator();

            foreach (var roll in item.Rolls)
            {
                var valueText = roll.Value is uint.MaxValue or null ? "-" : roll.Value.ToString();
                ImGui.Text($"{roll.Type}  {roll.PlayerName}  {valueText}");
            }

            if (item.IsComplete && item.Winner != null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f), $"Winner: {item.Winner}");
            }

            ImGui.Spacing();
        }
    }
}
