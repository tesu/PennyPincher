using System;
using System.Collections.Generic;
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

        private int configMode;
        private bool configHq;
        private bool configVerbose;

        private bool _config;
        private Configuration configuration;
        private Lumina.Excel.ExcelSheet<Item> items;
        private bool newRequest;
        private GetFilePointer getFilePtr;
        private List<MarketBoardCurrentOfferings> _cache = new();

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
        }
        
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
            ImGui.TextWrapped("*Subtracts an additional [<lowest price> %% <modulo>] from the price (no effect if modulo is 1).\nThis can be used to make the last digits of your copied prices consistent.");

            ImGui.Separator();

            string[] modes = { "Never", "Only at Retainer", "Always" };
            ImGui.Combo("When to copy", ref configMode, modes, modes.Length);
            ImGui.Checkbox($"When listing an item that can be HQ, only undercut HQ items", ref configHq);
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

            configMode = configuration.alwaysOn ? 2 : (configuration.smart ? 1 : 0);

            configHq = configuration.hq;
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
            
            configuration.delta = configDelta;
            configuration.min = configMin;
            configuration.mod = configMod;

            configuration.alwaysOn = configMode == 2;
            configuration.smart = configMode == 1;

            configuration.hq = configHq;
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
            if (opCode == Data.ServerOpCodes["MarketBoardItemRequestStart"])
            {
                newRequest = true;

                // clear cache on new request so we can verify that we got all the data we need when we inspect the price
                _cache.Clear();
            }
            if (opCode != Data.ServerOpCodes["MarketBoardOfferings"] || !newRequest) return;
            if (!configuration.alwaysOn && (!configuration.smart || !Retainer())) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);

            // collect data for data integrity
            _cache.Add(listing);

            var i = 0;
            if (configuration.hq && items.Single(j => j.RowId == listing.ItemListings[0].CatalogId).CanBeHq)
            {
                while (i < listing.ItemListings.Count && !listing.ItemListings[i].IsHq) i++;
                if (i == listing.ItemListings.Count) return;
            }

            if (!IsDataValid(listing))
            {
                return;
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

        private bool IsDataValid(MarketBoardCurrentOfferings listing)
        {
            // handle early items / if the first request has less than 10
            if (listing.ListingIndexStart == 0 && listing.ListingIndexEnd == 0)
            {
                return true;
            }

            // handle paged requests. 10 per request
            var neededItems = listing.ListingIndexStart + listing.ItemListings.Count;
            var actualItems = _cache.Sum(x => x.ItemListings.Count);
            if (neededItems == actualItems)
            {
                return true;
            }

            return false;
        }
    }
}