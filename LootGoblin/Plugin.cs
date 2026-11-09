
using Dalamud.Game.Command;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using LootGoblin.Windows;
using LootGoblin.LootTracking;
using System.IO;

namespace LootGoblin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/lootcheck";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("LootGoblin");

    private MainWindow MainWindow { get; init; }

    internal LootTracker LootTracker { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var goatImagePath = Path.Combine(
            PluginInterface.AssemblyLocation.Directory?.FullName!,
            "goat.png"
        );

        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(MainWindow);

        LootTracker = new LootTracker(
            AddonLifecycle,
            ChatGui,
            DataManager,
            ClientState,
            ObjectTable,
            PartyList,
            Log
        );

        LootTracker.ChestOpened += OnChestOpened;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens/Closes the loot results window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // Both configuration and main UI buttons open the main window.
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        LootTracker.ChestOpened -= OnChestOpened;
        LootTracker.Dispose();

        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnChestOpened()
    {
        if (Configuration.AutoOpenOnNewChest)
        {
            MainWindow.IsOpen = true;
        }
    }

    private void ToggleConfigUi()
    {
        MainWindow.Toggle();
    }

    private void ToggleMainUi()
    {
        MainWindow.Toggle();
    }
}
