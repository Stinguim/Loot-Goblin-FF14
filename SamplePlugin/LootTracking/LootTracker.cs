using System;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Game.Chat;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace SamplePlugin.LootTracking;

public sealed class LootTracker : IDisposable
{
    private const string AddonName = "NeedGreed";

    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui chatGui;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IPluginLog log;

    public LootSession CurrentSession { get; } = new();

    /// <summary>
    /// True enquanto o addon NeedGreed estiver "aberto" para o baú atual — usado
    /// para não criar um baú novo sempre que a janela reabre para mostrar rolls
    /// de outros jogadores (o PostSetup dispara em cada reabertura, não só
    /// quando surge loot novo).
    /// </summary>
    private bool chestIsOpen;

    /// <summary>
    /// Momento em que o baú atual fechou (PreFinalize). Se a janela reabrir
    /// pouco depois disso (ex.: o jogador voltou a clicar na aba de loot),
    /// tratamos como o mesmo baú em vez de criar um novo.
    /// </summary>
    private DateTime? chestClosedAt;

    private static readonly TimeSpan ChestReopenGracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Disparado sempre que um novo baú é aberto (PostSetup do addon NeedGreed),
    /// para que a UI possa, por exemplo, abrir-se automaticamente.
    /// </summary>
    public event System.Action? ChestOpened;

    private static readonly Regex RollValueRegex = new(
        @"^(?<player>.+?) rolls? (?<type>Need|Greed|Pass) on the .+\. (?<value>\d+)!$",
        RegexOptions.Compiled);

    private static readonly Regex YouRollValueRegex = new(
        @"^You roll (?<type>Need|Greed|Pass) on the .+\. (?<value>\d+)!$",
        RegexOptions.Compiled);

    private static readonly Regex CastLotRegex = new(
        @"^(?<player>.+?) casts (his|her|their) lot for the",
        RegexOptions.Compiled);

    private static readonly Regex YouCastLotRegex = new(
        @"^You cast your lot for the",
        RegexOptions.Compiled);

    private static readonly Regex ObtainedRegex = new(
        @"^(?<player>.+?) obtains? a",
        RegexOptions.Compiled);

    public LootTracker(
        IAddonLifecycle addonLifecycle,
        IChatGui chatGui,
        IDataManager dataManager,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IPluginLog log)
    {
        this.addonLifecycle = addonLifecycle;
        this.chatGui = chatGui;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.log = log;

        this.addonLifecycle.RegisterListener(
            AddonEvent.PostSetup,
            AddonName,
            OnNeedGreedSetup);

        this.addonLifecycle.RegisterListener(
            AddonEvent.PostRefresh,
            AddonName,
            OnNeedGreedRefresh);

        // PostDraw corre em todos os frames em que a janela está visível — garante
        // que os itens aparecem assim que existem, sem depender do jogador interagir.
        this.addonLifecycle.RegisterListener(
            AddonEvent.PostDraw,
            AddonName,
            OnNeedGreedRefresh);

        this.addonLifecycle.RegisterListener(
            AddonEvent.PreFinalize,
            AddonName,
            OnNeedGreedFinalize);

        this.chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(
            AddonEvent.PostSetup,
            AddonName,
            OnNeedGreedSetup);

        addonLifecycle.UnregisterListener(
            AddonEvent.PostRefresh,
            AddonName,
            OnNeedGreedRefresh);

        addonLifecycle.UnregisterListener(
            AddonEvent.PostDraw,
            AddonName,
            OnNeedGreedRefresh);

        addonLifecycle.UnregisterListener(
            AddonEvent.PreFinalize,
            AddonName,
            OnNeedGreedFinalize);

        chatGui.ChatMessage -= OnChatMessage;
    }

    private void OnNeedGreedSetup(AddonEvent type, AddonArgs args)
    {
        var withinGracePeriod =
            chestClosedAt.HasValue &&
            DateTime.Now - chestClosedAt.Value < ChestReopenGracePeriod;

        if (!chestIsOpen && (CurrentSession.CurrentChest == null || !withinGracePeriod))
        {
            var dungeonName = GetCurrentDutyName();

            CurrentSession.StartNewChest(dungeonName);

            log.Information($"[LootTracker] Window NeedGreed opened (PostSetup). Chest: {dungeonName}");

            ChestOpened?.Invoke();
        }
        else if (!chestIsOpen)
        {
            log.Information("[LootTracker] Window NeedGreed reopened quickly — reusing previous chest.");
        }

        chestIsOpen = true;
    }

    private void OnNeedGreedFinalize(AddonEvent type, AddonArgs args)
    {
        log.Information("[LootTracker] Window NeedGreed closing (PreFinalize).");

        chestIsOpen = false;
        chestClosedAt = DateTime.Now;
    }

    /// <summary>
    /// Tenta obter o nome da zona/dungeon atual. Se a sheet não tiver o dado
    /// (ex.: fora de instância) devolve um nome genérico.
    /// </summary>
    private string GetCurrentDutyName()
    {
        try
        {
            var territoryId = clientState.TerritoryType;
            var territorySheet = dataManager.GetExcelSheet<TerritoryType>();
            var territoryRow = territorySheet.GetRowOrDefault(territoryId);

            var placeName = territoryRow?.PlaceName.ValueNullable?.Name.ToString();

            return string.IsNullOrWhiteSpace(placeName) ? "Unknown Location" : placeName;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[LootTracker] Failed to resolve current duty name.");
            return "Unknown Location";
        }
    }

    private unsafe void OnNeedGreedRefresh(AddonEvent type, AddonArgs args)
    {
        if (args.Addon.Address == nint.Zero)
            return;

        var addon = (AddonNeedGreed*)args.Addon.Address;

        var count = Math.Min((int)addon->NumItems, 16);

        var chest = CurrentSession.CurrentChest ?? CurrentSession.StartNewChest(GetCurrentDutyName());

        for (var i = 0; i < count; i++)
        {
            var item = addon->Items[i];

            if (item.ItemId == 0)
                continue;

            var isNewItem = !chest.Items.ContainsKey(item.ItemId);

            var trackedItem = chest.Items.GetOrAdd(
                item.ItemId,
                id => new LootItem
                {
                    ItemId = id
                });

            trackedItem.IconId = item.IconId;

            if (string.IsNullOrEmpty(trackedItem.ItemName))
            {
                trackedItem.ItemName = ResolveItemName(item.ItemId);
            }

            if (isNewItem)
            {
                log.Information(
                    $"[LootTracker] New item tracked: Id={item.ItemId} Name={trackedItem.ItemName}");
            }
        }
    }

    /// <summary>
    /// Tenta descobrir o ClassJob atual do jogador, para desenhar o ícone da
    /// classe na UI. Para "You" usa o ClientState; para os restantes procura
    /// no IPartyList (cobre o grupo inteiro, não só o próprio jogador).
    /// Devolve null se não for possível resolver (ex.: jogador fora do grupo).
    /// </summary>
    private uint? ResolveClassJobId(string playerName)
    {
        try
        {
            if (playerName == "You")
            {
                return objectTable.LocalPlayer?.ClassJob.RowId;
            }

            foreach (var member in partyList)
            {
                var memberName = $"{member.Name}@{member.World.Value.Name}";

                if (memberName == playerName)
                    return member.ClassJob.RowId;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"[LootTracker] Failed to resolve class job for {playerName}.");
        }

        return null;
    }

    /// <summary>
    /// Faz o lookup do nome do item na sheet Item do jogo, a partir do ItemId.
    /// </summary>
    private string ResolveItemName(uint itemId)
    {
        try
        {
            var itemSheet = dataManager.GetExcelSheet<Item>();
            var row = itemSheet.GetRowOrDefault(itemId);

            return row?.Name.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"[LootTracker] Failed to resolve item name for ItemId={itemId}.");
            return string.Empty;
        }
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var type = message.LogKind;
        var chatMessage = message.Message;

        if (type != XivChatType.LootRoll &&
            type != XivChatType.LootNotice)
            return;

        var itemPayload = chatMessage.Payloads
            .OfType<ItemPayload>()
            .FirstOrDefault();

        if (itemPayload == null)
            return;

        var playerPayload = chatMessage.Payloads
            .OfType<PlayerPayload>()
            .FirstOrDefault();

        var playerNameFromPayload = playerPayload != null
            ? $"{playerPayload.PlayerName}@{playerPayload.World.Value.Name}"
            : null;

        var text = chatMessage.TextValue;

        LootChatEvent? evt = null;

        if (YouRollValueRegex.Match(text) is { Success: true } youRoll)
        {
            evt = new LootChatEvent(
                LootChatEventKind.RollValue,
                "You",
                itemPayload.ItemId,
                youRoll.Groups["type"].Value,
                int.Parse(youRoll.Groups["value"].Value));
        }
        else if (RollValueRegex.Match(text) is { Success: true } roll)
        {
            var pName = playerNameFromPayload ?? roll.Groups["player"].Value;

            evt = new LootChatEvent(
                LootChatEventKind.RollValue,
                pName,
                itemPayload.ItemId,
                roll.Groups["type"].Value,
                int.Parse(roll.Groups["value"].Value));
        }
        else if (YouCastLotRegex.IsMatch(text))
        {
            evt = new LootChatEvent(
                LootChatEventKind.CastLot,
                "You",
                itemPayload.ItemId,
                null,
                null);
        }
        else if (CastLotRegex.Match(text) is { Success: true } cast)
        {
            var pName = playerNameFromPayload ?? cast.Groups["player"].Value;

            evt = new LootChatEvent(
                LootChatEventKind.CastLot,
                pName,
                itemPayload.ItemId,
                null,
                null);
        }
        else if (ObtainedRegex.Match(text) is { Success: true } obtain)
        {
            var matchedPlayer = obtain.Groups["player"].Value;

            var pName = matchedPlayer.Equals("You", StringComparison.OrdinalIgnoreCase)
                ? "You"
                : playerNameFromPayload ?? matchedPlayer;

            evt = new LootChatEvent(
                LootChatEventKind.Obtained,
                pName,
                itemPayload.ItemId,
                null,
                null);
        }

        if (evt == null)
            return;

        ProcessLootEvent(evt);
    }

    private void ProcessLootEvent(LootChatEvent evt)
    {
        log.Information($"[LootTracker] Loot event received: {evt}");

        var chest = CurrentSession.CurrentChest ?? CurrentSession.StartNewChest(GetCurrentDutyName());

        var item = chest.Items.GetOrAdd(
            evt.ItemId,
            id => new LootItem { ItemId = id });

        switch (evt.Kind)
        {
            case LootChatEventKind.RollValue:
                if (evt.RollType == null ||
                    !Enum.TryParse<RollType>(evt.RollType, ignoreCase: true, out var rollType))
                {
                    return;
                }

                var existingRoll = item.Rolls.FirstOrDefault(r => r.PlayerName == evt.PlayerName);

                if (existingRoll != null)
                {
                    existingRoll.Type = rollType;
                    existingRoll.Value = evt.RollValue;
                    existingRoll.ClassJobId ??= ResolveClassJobId(evt.PlayerName);
                }
                else
                {
                    item.Rolls.Add(new LootRoll
                    {
                        PlayerName = evt.PlayerName,
                        Type = rollType,
                        Value = evt.RollValue,
                        ClassJobId = ResolveClassJobId(evt.PlayerName)
                    });
                }

                break;

            case LootChatEventKind.Obtained:
                item.Winner = evt.PlayerName;
                item.IsComplete = true;

                InferPassForRemainingParticipants(item);

                break;

            // CastLot indica que o jogador participou (abriu a decisão), mas não
            // diz que tipo escolheu. Guardamos o nome para, quando o item for
            // obtido, conseguirmos inferir quem passou por exclusão.
            case LootChatEventKind.CastLot:
                item.Participants.TryAdd(evt.PlayerName, 0);
                break;

            case LootChatEventKind.Unable:
            default:
                break;
        }
    }

    /// <summary>
    /// O jogo não emite mensagem de chat quando alguém escolhe Pass, por isso
    /// assumimos Pass para qualquer participante que tenha "cast lot" mas nunca
    /// tenha aparecido com um roll de Need/Greed até o item ser atribuído.
    /// </summary>
    private void InferPassForRemainingParticipants(LootItem item)
    {
        foreach (var participant in item.Participants.Keys)
        {
            var alreadyRolled = item.Rolls.Any(r => r.PlayerName == participant);

            if (alreadyRolled)
                continue;

            item.Rolls.Add(new LootRoll
            {
                PlayerName = participant,
                Type = RollType.Pass,
                Value = null,
                ClassJobId = ResolveClassJobId(participant)
            });
        }
    }
}