using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Command;
using Dalamud.Game.Internal.Network;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin;
using System;
using System.Windows.Forms;

namespace PennyHelper
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Penny Pincher";

        private const string commandName = "/penny";
        private const string helpName = "help";
        private const string alwaysOnName = "alwayson";
        private const string deltaName = "delta";
        private const string smartName = "smart";
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
                HelpMessage = $"Toggles {Name} mode. {commandName} {helpName} for additional options"
            });

            this.pi.Framework.Network.OnNetworkMessage += OnNetworkEvent;
            this.pi.Framework.Gui.Chat.OnChatMessage += OnChatEvent;
        }

        public void Dispose()
        {
            this.pi.Framework.Gui.Chat.OnChatMessage -= OnChatEvent;
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
                PrintSetting($"{Name}", this.enabled);
                if (this.configuration.alwaysOn) this.pi.Framework.Gui.Chat.Print($"(this setting is ignored since {alwaysOnName} is set to true)");
                return;
            }
            var argArray = args.Split(' ');
            switch (argArray[0])
            {
                case helpName:
                    this.pi.Framework.Gui.Chat.Print($"{commandName}: toggles {Name} (resets to false on game start, does not do anything if {alwaysOnName} is set to true)");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {helpName}: displays this help page");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {alwaysOnName}: Toggles whether {Name} is always on");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {deltaName} <delta>: Sets the undercutting amount to be <delta>");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {smartName}: Toggles whether {Name} should automatically turn on/off when you access a retainer");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {verboseName}: Toggles whether {Name} prints whenever it writes to clipboard");
                    return;
                case alwaysOnName:
                    this.configuration.alwaysOn = !this.configuration.alwaysOn;
                    this.configuration.Save();
                    PrintSetting($"{Name} {alwaysOnName}", this.configuration.alwaysOn);
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
                        this.pi.Framework.Gui.Chat.Print($"{Name} {deltaName} set to {this.configuration.delta}.");
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
                case smartName:
                    this.configuration.smart = !this.configuration.smart;
                    this.configuration.Save();
                    PrintSetting($"{Name} {smartName}", this.configuration.smart);
                    return;
                case verboseName:
                    this.configuration.verbose = !this.configuration.verbose;
                    this.configuration.Save();
                    PrintSetting($"{Name} {verboseName}", this.configuration.verbose);
                    return;
                default:
                    this.pi.Framework.Gui.Chat.Print($"Unknown subcommand used. Run {commandName} {helpName} for valid subcommands.");
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

        private void OnChatEvent(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (!this.configuration.smart) return;
            if (!type.Equals(XivChatType.Echo)) return;
            if (message.TextValue.StartsWith("You are no longer selling items in the "))
            {
                this.enabled = true;
                if (this.configuration.verbose) PrintSetting($"{Name}", this.enabled);
            }
            if (message.TextValue.StartsWith("You are now selling items in the "))
            {
                this.enabled = false;
                if (this.configuration.verbose) PrintSetting($"{Name}", this.enabled);
            }
        }
    }
}
