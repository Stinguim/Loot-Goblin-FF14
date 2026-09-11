using System.IO;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

using SamplePlugin.LootTracking;
using SamplePlugin.Windows;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService]
    internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IPlayerState PlayerState { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    internal static IPartyList PartyList { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/lootcheck";

    public Configuration Configuration { get; }

    public WindowSystem WindowSystem { get; } = new("SamplePlugin");

    private ConfigWindow ConfigWindow { get; }
    private MainWindow MainWindow { get; }

    internal LootTracker LootTracker { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration
            ?? new Configuration();

        var goatImagePath = Path.Combine(
            PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty,
            "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        // Inicializa o LootTracker.
        LootTracker = new LootTracker(
            AddonLifecycle,
            ChatGui,
            DataManager,
            ClientState,
            ObjectTable,
            PartyList,
            Log);

        // Abre a janela quando um novo baú é detetado.
        LootTracker.ChestOpened += OnChestOpened;

        // Slash command.
        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens/Closes the loot results window."
            });

        // UI.
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information(
            $"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        // Remove eventos do UI.
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        // Remove o evento do LootTracker.
        LootTracker.ChestOpened -= OnChestOpened;

        // Dispose do LootTracker.
        LootTracker.Dispose();

        // Remove command.
        CommandManager.RemoveHandler(CommandName);

        // Dispose das janelas.
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnChestOpened()
    {
        MainWindow.IsOpen = true;
    }

    public void ToggleConfigUi()
    {
        ConfigWindow.Toggle();
    }

    public void ToggleMainUi()
    {
        MainWindow.Toggle();
    }
}