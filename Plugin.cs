using Dalamud.Game.Command;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Internal.Network;
using Dalamud.Plugin;
using System;
using System.Windows.Forms;

namespace PennyHelper
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Penny Helper";

        private const string commandName = "/penny";
        private const string helpName = "help";
        private const string alwaysOnName = "alwayson";
        private const string deltaName = "delta";
        private const string verboseName = "verbose";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private bool enabled;
        private uint lastItem;
        private uint lastPrice;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;

            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);

            this.enabled = false;
            this.lastItem = 0;
            this.lastPrice = 0;

            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = $"Toggles Penny Helper mode. {commandName} {helpName} for additional options"
            });

            this.pi.Framework.Network.OnNetworkMessage += OnNetworkEvent;
        }

        public void Dispose()
        {
            this.pi.Framework.Network.OnNetworkMessage -= OnNetworkEvent;
            this.pi.CommandManager.RemoveHandler(commandName);
            this.pi.Dispose();
        }

        private void PrintSetting(string name, bool setting)
        {
            this.pi.Framework.Gui.Chat.Print(name + (setting ? " enabled." : " disabled."));
        }

        private void OnCommand(string command, string args)
        {
            if (args == "")
            {
                this.enabled = !this.enabled;
                PrintSetting("Penny Helper", this.enabled);
                if (this.configuration.alwaysOn) this.pi.Framework.Gui.Chat.Print($"(this setting is ignored since {alwaysOnName} is set to true)");
                return;
            }
            var argArray = args.Split(' ');
            switch (argArray[0])
            {
                case helpName:
                    this.pi.Framework.Gui.Chat.Print($"{commandName}: toggles Penny Helper (does not do anything if {alwaysOnName} is set to true)");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {helpName}: displays this help page");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {alwaysOnName}: Toggles whether Penny Helper is always on; this setting is saved");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {deltaName} <delta>: Sets the undercutting amount to be <delta>; this setting is saved");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {verboseName}: Toggles whether Penny Helper prints whenver it writes to clipboard; this setting is saved");
                    return;
                case alwaysOnName:
                    this.configuration.alwaysOn = !this.configuration.alwaysOn;
                    this.configuration.Save();
                    PrintSetting($"Penny Helper {alwaysOnName}", this.configuration.alwaysOn);
                    return;
                case deltaName:
                    if (argArray.Length < 2)
                    {
                        this.pi.Framework.Gui.Chat.Print($"{commandName} {deltaName} <delta> is missing its <delta> argument.");
                        return;
                    }
                    var arg = argArray[1];
                    try
                    {
                        this.configuration.delta = int.Parse(arg);
                        this.configuration.Save();
                        this.pi.Framework.Gui.Chat.Print($"Penny Helper {deltaName} set to {this.configuration.delta}.");
                    }
                    catch (FormatException)
                    {
                        this.pi.Framework.Gui.Chat.Print($"Unable to read '{arg}' as an integer.");
                    }
                    catch (OverflowException)
                    {
                        this.pi.Framework.Gui.Chat.Print($"'{arg}' is out of range.");
                    }
                    return;
                case verboseName:
                    this.configuration.verbose = !this.configuration.verbose;
                    this.configuration.Save();
                    PrintSetting($"Penny Helper {verboseName}", this.configuration.verbose);
                    return;
                default:
                    this.pi.Framework.Gui.Chat.Print($"Unknown subcommand used. View {commandName} {helpName} for valid subcommands.");
                    return;
            }
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (!this.enabled && !this.configuration.alwaysOn) return;
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!this.pi.Data.IsDataReady) return;
            if (opCode != this.pi.Data.ServerOpCodes["MarketBoardOfferings"]) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);
            var catalogId = listing.ItemListings[0].CatalogId;
            var price = listing.ItemListings[0].PricePerUnit;
            if (this.lastItem == catalogId && this.lastPrice < price) return;
            this.lastItem = catalogId;
            this.lastPrice = price;
            var newPrice = listing.ItemListings[0].PricePerUnit - this.configuration.delta;
            Clipboard.SetText(newPrice.ToString());
            if (this.configuration.verbose) this.pi.Framework.Gui.Chat.Print($"{newPrice} copied to clipboard.");
        }
    }
}
