using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.Game.Network.Structures;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Num = System.Numerics;

namespace PennyPincher
{
    public class PennyPincher : IDalamudPlugin
    {
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static CommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static ChatGui Chat { get; private set; } = null!;
        [PluginService] public static DataManager Data { get; private set; } = null!;
        [PluginService] public static GameNetwork GameNetwork { get; private set; } = null!;
        [PluginService] public static SigScanner SigScanner { get; private set; } = null!;
        
        private const string commandName = "/penny";
        
        private int configMin;
        private int configMod;
        private int configDelta;
        private bool configAlwaysOn;
        private bool configHq;
        private bool configSmart;
        private bool configVerbose;

        private bool _config;
        private Configuration configuration;
        private Lumina.Excel.ExcelSheet<Item> items;
        private bool newRequest;
        private GetFilePointer getFilePtr;

        public PennyPincher()
        {
            configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            LoadConfig();
            
            items = Data.GetExcelSheet<Item>();
            newRequest = false;

            PluginInterface.UiBuilder.Draw += DrawWindow;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

            CommandManager.AddHandler(commandName, new CommandInfo(Command)
            {
                HelpMessage = $"Opens the {Name} config menu",
            });

            GameNetwork.NetworkMessage += OnNetworkEvent;

            try
            {
                var ptr = SigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 14 83 7B 44 00");
                getFilePtr = Marshal.GetDelegateForFunctionPointer<GetFilePointer>(ptr);
            }
            catch (Exception e)
            {
                getFilePtr = null;
                PluginLog.LogError(e.ToString());
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetFilePointer(byte index);

        public string Name => "Penny Pincher";

        public void Dispose()
        {
            GameNetwork.NetworkMessage -= OnNetworkEvent;
            PluginInterface.UiBuilder.Draw -= DrawWindow;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            CommandManager.RemoveHandler(commandName);
            PluginInterface.Dispose();
        }
        
        private void Command(string command, string arguments)
        {
            _config = true;
        }
        
        private void OpenConfigUi()
        {
            _config = true;
        }

        private void DrawWindow()
        {
            if (!_config) return;
            
            ImGui.SetNextWindowSize(new Num.Vector2(600, 600), ImGuiCond.FirstUseEver);
            ImGui.Begin($"{Name} Config", ref _config);
            
            ImGui.InputInt("Delta", ref configDelta);
            ImGui.TextWrapped("Sets the undercutting amount to be <delta>.");
            
            ImGui.InputInt("Minimum Price", ref configMin);
            ImGui.TextWrapped("Sets a minimum value to be copied. <min> cannot be below 1.");
            
            ImGui.InputInt("Mod", ref configMod);
            ImGui.TextWrapped("Adjusts base price by subtracting <price> % <mod> from <price> before subtracting <delta>.\nThis makes the last digits of your posted prices consistent.");

            ImGui.Separator();
            
            ImGui.Checkbox($"Always On: Toggles whether {Name} is always on (supersedes 'Smart Mode')", ref configAlwaysOn);
            ImGui.Checkbox($"HQ: Toggles whether {Name} should only undercut HQ items when you're listing an HQ item", ref configHq);
            ImGui.Checkbox($"Smart Mode: Toggles whether {Name} should automatically copy when you're using a retainer", ref configSmart);
            ImGui.Checkbox($"Verbose: Toggles whether {Name} prints whenever it copies to clipboard", ref configVerbose);

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

            configAlwaysOn = configuration.alwaysOn;
            configHq = configuration.hq;
            configSmart = configuration.smart;
            configVerbose = configuration.verbose;
        }

        private void SaveConfig()
        {
            if (configMin < 1)
            {
                Chat.Print($"{Name}: <min> cannot be lower than 1.");
                return;
            }
            
            configuration.delta = configDelta;
            configuration.min = configMin;
            configuration.mod = configMod;

            configuration.alwaysOn = configAlwaysOn;
            configuration.hq = configHq;
            configuration.smart = configSmart;
            configuration.verbose = configVerbose;
            
            PluginInterface.SavePluginConfig(configuration);
        }
        
        private bool Retainer()
        {
            return (getFilePtr != null) && Marshal.ReadInt64(getFilePtr(7), 0xB0) != 0;
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!Data.IsDataReady) return;
            if (opCode == Data.ServerOpCodes["MarketBoardItemRequestStart"]) newRequest = true;
            if (opCode != Data.ServerOpCodes["MarketBoardOfferings"] || !newRequest) return;
            if (!configuration.alwaysOn && (!configuration.smart || !Retainer())) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);
            var i = 0;
            if (configuration.hq && items.Single(j => j.RowId == listing.ItemListings[0].CatalogId).CanBeHq)
            {
                while (i < listing.ItemListings.Count && !listing.ItemListings[i].IsHq) i++;
                if (i == listing.ItemListings.Count) return;
            }

            var price = listing.ItemListings[i].PricePerUnit - (listing.ItemListings[i].PricePerUnit % configuration.mod) - configuration.delta;
            price = Math.Max(price, configuration.min);
            ImGui.SetClipboardText(price.ToString());
            if (configuration.verbose)
            {
                Chat.Print((configuration.hq ? "[HQ] " : string.Empty) + $"{price:n0} copied to clipboard.");
            }

            newRequest = false;
        }
    }
}