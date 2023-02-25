using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Data;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.Game.Network.Structures;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
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
        [PluginService] public static KeyState KeyState { get; private set; } = null!;

        private const string commandName = "/penny";
        
        private int configMin;
        private int configMod;
        private int configMultiple;
        private int configDelta;

        private bool configAlwaysOn;
        private bool configHq;
        private bool configVerbose;

        private bool _config;
        private Configuration configuration;
        private Lumina.Excel.ExcelSheet<Item> items;
        private bool newRequest;
        private bool useHq;
        private bool itemHq;
        [Signature("E8 ?? ?? ?? ?? 48 85 C0 74 14 83 7B 44 00")]
        private GetFilePointer getFilePtr;
        [Signature("48 89 5C 24 ?? 55 56 57 48 83 EC 50 4C 89 64 24", DetourName = nameof(AddonRetainerSell_OnSetup))]
        private Hook<AddonOnSetup> retainerSellSetup;
        private List<MarketBoardCurrentOfferings> _cache = new();

        public PennyPincher()
        {
            var pluginConfig = PluginInterface.GetPluginConfig();
            if (pluginConfig is Configuration)
            {
                configuration = (Configuration)pluginConfig;
            }
            else if (pluginConfig is OldConfiguration)
            {
                OldConfiguration oldConfig = (OldConfiguration)pluginConfig;
                configuration = new Configuration
                {
                    alwaysOn = oldConfig.alwaysOn,
                    delta = oldConfig.delta,
                    hq = oldConfig.alwaysHq,
                    min = oldConfig.min,
                    mod = oldConfig.mod,
                    multiple = oldConfig.multiple,
                    verbose = oldConfig.verbose
                };
            }
            else
            {
                configuration = new Configuration();
            }
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
                SignatureHelper.Initialise(this);
                retainerSellSetup.Enable();
            }
            catch (Exception e)
            {
                getFilePtr = null;
                PluginLog.LogError(e.ToString());
            }
        }

        private delegate IntPtr GetFilePointer(byte index);
        private delegate IntPtr AddonOnSetup(IntPtr addon, uint a2, IntPtr dataPtr);

        public string Name => "Penny Pincher";

        public void Dispose()
        {
            retainerSellSetup?.Dispose();
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
            ImGui.TextWrapped("*Subtracts an additional [<lowest price> %% <modulo>] from the price before applying the delta (no effect if modulo is 1).\nThis can be used to make the last digits of your copied prices consistent.");

            ImGui.InputInt("Multiple†", ref configMultiple);
            ImGui.TextWrapped("†Subtracts an additional [<lowest price> %% <multiple>] from the price after applying the delta (no effect if multiple is 1).\nThis can be used to undercut by multiples of an amount.");

            ImGui.Separator();

            ImGui.Checkbox($"Copy prices when opening all marketboards (instead of just retainer marketboards)", ref configAlwaysOn);
            ImGui.Checkbox($"Undercut HQ prices", ref configHq);
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

                var shiftHeld = KeyState[(byte)Dalamud.DrunkenToad.ModifierKey.Enum.VkShift];
                useHq = shiftHeld ^ (configuration.hq || itemHq);
            }
            if (opCode != Data.ServerOpCodes["MarketBoardOfferings"] || !newRequest) return;
            if (!configuration.alwaysOn && !Retainer()) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);

            // collect data for data integrity
            _cache.Add(listing);
            if (!IsDataValid(listing)) return;

            var i = 0;
            if (useHq && items.Single(j => j.RowId == listing.ItemListings[0].CatalogId).CanBeHq)
            {
                while (i < listing.ItemListings.Count && (!listing.ItemListings[i].IsHq || IsOwnRetainer(listing.ItemListings[i].RetainerId))) i++;
            }
            else
            {
                while (i < listing.ItemListings.Count && IsOwnRetainer(listing.ItemListings[i].RetainerId)) i++;
            }

            if (i == listing.ItemListings.Count) return;

            var price = listing.ItemListings[i].PricePerUnit - (listing.ItemListings[i].PricePerUnit % configuration.mod) - configuration.delta;
            price -= (price % configuration.multiple);
            price = Math.Max(price, configuration.min);

            ImGui.SetClipboardText(price.ToString());
            if (configuration.verbose)
            {
                Chat.Print((useHq ? "[HQ] " : string.Empty) + $"{price:n0} copied to clipboard.");
            }

            newRequest = false;
        }

        private unsafe IntPtr AddonRetainerSell_OnSetup(IntPtr addon, uint a2, IntPtr dataPtr)
        {
            var result = retainerSellSetup.Original(addon, a2, dataPtr);

            string nodeText = ((AddonRetainerSell*)addon)->ItemName->NodeText.ToString();
            itemHq = nodeText.Contains('\uE03C');

            return result;
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
            return (neededItems == actualItems);
        }

        private unsafe bool IsOwnRetainer(ulong retainerId)
        {
            var retainerManager = RetainerManager.Instance();
            for (int i = 0; i < retainerManager->GetRetainerCount(); ++i)
            {
                if (retainerId == retainerManager->Retainer[i]->RetainerID)
                {
                    return true;
                }
            }

            return false;
        }
    }
}