using System;
using System.Linq;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Game.Network.Structures;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Num = System.Numerics;

namespace PennyPincher
{
    public class PennyPincher : IDalamudPlugin
    {
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IChatGui Chat { get; private set; } = null!;
        [PluginService] public static IDataManager Data { get; private set; } = null!;
        [PluginService] public static IKeyState KeyState { get; private set; } = null!;
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
        [PluginService] public static IMarketBoard MarketBoard { get; private set; } = null!;

        private const string commandName = "/penny";
        
        private int configMin;
        private int configMod;
        private int configMultiple;
        private int configDelta;

        private bool configAlwaysOn;
        private bool configHq;
        private bool configUndercutSelf;
        private bool configVerbose;

        private bool _config;
        private Configuration configuration;
        private Lumina.Excel.ExcelSheet<Item> items;
        private bool newRequest;
        private bool useHq;
        private bool itemHq;
        [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 4C 89 74 24 ?? 49 8B F0 44 8B F2", DetourName = nameof(AddonRetainerSell_OnSetup))]
        private Hook<AddonOnSetup>? retainerSellSetup;
        private unsafe delegate void* MarketBoardItemRequestStart(int* a1,int* a2,int* a3);

        //If the signature for these are ever lost, find the ProcessZonePacketDown signature in Dalamud and then find the relevant function based on the opcode.
        [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 48 8B FA E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 4A", DetourName = nameof(MarketBoardItemRequestStartDetour), UseFlags = SignatureUseFlags.Hook)]
        private Hook<MarketBoardItemRequestStart>? marketBoardItemRequestStart;

        public PennyPincher()
        {
            var pluginConfig = PluginInterface.GetPluginConfig();
            if (pluginConfig is Configuration) configuration = (Configuration)pluginConfig;
            else configuration = new Configuration();
            LoadConfig();

            MarketBoard.OfferingsReceived += MarketBoardOnOfferingsReceived;
            items = Data.GetExcelSheet<Item>();
            newRequest = false;

            PluginInterface.UiBuilder.Draw += DrawWindow;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            CommandManager.AddHandler(commandName, new CommandInfo(Command)
            {
                HelpMessage = $"Opens the {Name} config menu",
            });

            try
            {
                unsafe
                {
                    GameInteropProvider.InitializeFromAttributes(this);
                    marketBoardItemRequestStart!.Enable();
                    retainerSellSetup!.Enable();
                }
            }
            catch (Exception e)
            {
                marketBoardItemRequestStart = null;
                retainerSellSetup = null;
                Log.Error(e.ToString());
            }
        }

        private void MarketBoardOnOfferingsReceived(IMarketBoardCurrentOfferings currentOfferings)
        {
            if (!newRequest) return;
            if (!configuration.alwaysOn && !Retainer()) return;

            var skipNq = useHq && items.Single(j => j.RowId == currentOfferings.ItemListings[0].ItemId).CanBeHq;
            var i = 0;
            while (i < currentOfferings.ItemListings.Count)
            {
                if (!configuration.undercutSelf && IsOwnRetainer(currentOfferings.ItemListings[i].RetainerId)) i++;
                else if (skipNq && !currentOfferings.ItemListings[i].IsHq) i++;
                else break;
            }
            if (i >= currentOfferings.ItemListings.Count) return;

            var price = currentOfferings.ItemListings[i].PricePerUnit - (currentOfferings.ItemListings[i].PricePerUnit % configuration.mod) - configuration.delta;
            price -= (price % configuration.multiple);
            price = Math.Max(price, configuration.min);

            ImGui.SetClipboardText(price.ToString());
            if (configuration.verbose) Chat.Print((useHq ? "\uE03C" : string.Empty) + $"{price:n0} copied to clipboard.");

            newRequest = false;
        }

        private void ParseNetworkEvent(IntPtr dataPtr, PennyPincherPacketType packetType)
        {
            if (packetType == PennyPincherPacketType.MarketBoardItemRequestStart)
            {
                newRequest = true;
                useHq = KeyState[VirtualKey.SHIFT] ^ (configuration.hq && itemHq);
            }
        }

        public void Dispose()
        {
            MarketBoard.OfferingsReceived -= MarketBoardOnOfferingsReceived;
            marketBoardItemRequestStart?.Dispose();
            retainerSellSetup?.Dispose();
            PluginInterface.UiBuilder.Draw -= DrawWindow;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            CommandManager.RemoveHandler(commandName);
        }

        private unsafe void* MarketBoardItemRequestStartDetour(int* a1,int* a2,int* a3)
        {
            try
            {
                if (a3 != null)
                {
                    var ptr = (IntPtr)a2;
                    ParseNetworkEvent(ptr, PennyPincherPacketType.MarketBoardItemRequestStart);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Market board item request start detour crashed while parsing.");
            }
            
            return marketBoardItemRequestStart!.Original(a1, a2, a3);
        }

        private delegate IntPtr AddonOnSetup(IntPtr addon, uint a2, IntPtr dataPtr);

        public string Name => "Penny Pincher";
        
        private void Command(string command, string arguments)
        {
            if (arguments == "hq")
            {
                configHq = !configHq;
                Chat.Print("Penny Pincher HQ mode " + (configHq ? "enabled." : "disabled."));
                SaveConfig();
            }
            else
            {
                _config = true;
            }
        }
        
        private void OpenConfigUi()
        {
            _config = true;
        }

        private void DrawWindow()
        {
            if (!_config) return;
            
            ImGui.SetNextWindowSize(new Num.Vector2(550, 270), ImGuiCond.FirstUseEver);
            ImGui.Begin($"{Name} Config", ref _config);
            
            ImGui.InputInt("Amount to undercut by", ref configDelta);
            
            ImGui.InputInt("Minimum price to copy", ref configMin);
            
            ImGui.InputInt("Modulo*", ref configMod);
            ImGui.TextWrapped("*Subtracts an additional [<lowest price> %% <modulo>] from the price BEFORE applying the delta (no effect if modulo is 1).\nThis can be used to make the last digits of your copied prices consistent.");

            ImGui.InputInt("Multiple†", ref configMultiple);
            ImGui.TextWrapped("†Subtracts an additional [<lowest price> %% <multiple>] from the price AFTER applying the delta (no effect if multiple is 1).\nThis can be used to undercut by multiples of an amount.");

            ImGui.Separator();

            ImGui.Checkbox($"Copy prices when opening all marketboards (instead of just retainer marketboards)", ref configAlwaysOn);
            ImGui.Checkbox($"Undercut your own retainers", ref configUndercutSelf);
            ImGui.Checkbox($"Undercut HQ prices when listing HQ item", ref configHq);
            ImGui.TextWrapped("Note that you can temporarily switch from HQ to NQ or vice versa by holding Shift when opening the marketboard");
            ImGui.Checkbox($"Print chat message when prices are copied to clipboard", ref configVerbose);

            ImGui.Separator();
            if (ImGui.Button("Save and Close Config"))
            {
                SaveConfig();
                _config = false;
            }

            ImGui.End();
        }

        private void LoadConfig()
        {
            configDelta = configuration.delta;
            configMin = configuration.min;
            configMod = configuration.mod;
            configMultiple = configuration.multiple;

            configAlwaysOn = configuration.alwaysOn;
            configHq = configuration.hq;
            configUndercutSelf = configuration.undercutSelf;
            configVerbose = configuration.verbose;
        }

        private void SaveConfig()
        {
            if (configMin < 1)
            {
                Chat.Print("Minimum price must be positive.");
                configMin = 1;
            }

            if (configMod < 1)
            {
                Chat.Print("Modulo must be positive.");
                configMod = 1;
            }

            if (configMultiple < 1)
            {
                Chat.Print("Multiple must be positive.");
                configMod = 1;
            }
            
            configuration.delta = configDelta;
            configuration.min = configMin;
            configuration.mod = configMod;
            configuration.multiple = configMultiple;

            configuration.alwaysOn = configAlwaysOn;
            configuration.hq = configHq;
            configuration.undercutSelf = configUndercutSelf;
            configuration.verbose = configVerbose;
            
            PluginInterface.SavePluginConfig(configuration);
        }
        
        private unsafe bool Retainer()
        {
            return ItemOrderModule.Instance()->ActiveRetainerId != 0;
        }

        private unsafe IntPtr AddonRetainerSell_OnSetup(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            var result = retainerSellSetup!.Original(addon, a2, dataPtr);

            string nodeText = ((AddonRetainerSell*)addon)->ItemName->NodeText.ToString();
            itemHq = nodeText.Contains('\uE03C');

            return result;
        }

        private unsafe bool IsOwnRetainer(ulong retainerId)
        {
            var retainerManager = RetainerManager.Instance();
            for (uint i = 0; i < retainerManager->GetRetainerCount(); ++i)
            {
                if (retainerId == retainerManager->GetRetainerBySortedIndex(i)->RetainerId) return true;
            }
            return false;
        }
    }

    enum PennyPincherPacketType
    {
        MarketBoardItemRequestStart,
        MarketBoardOfferings
    }
}