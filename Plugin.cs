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

        private const string toggleName = "/penny";
        private const string alwaysOnName = "/pennyalwayson";
        private const string deltaName = "/pennydelta";
        private const string verboseName = "/pennyverbose";

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

            this.pi.CommandManager.AddHandler(toggleName, new CommandInfo(OnToggle)
            {
                HelpMessage = "Toggles Penny Helper mode."
            });

            this.pi.CommandManager.AddHandler(alwaysOnName, new CommandInfo(OnAlwaysOn)
            {
                HelpMessage = "Toggles Penny Helper always on setting (default: false)."
            });

            this.pi.CommandManager.AddHandler(deltaName, new CommandInfo(OnDelta)
            {
                HelpMessage = "Sets Penny Helper delta setting (default: 1)."
            });

            this.pi.CommandManager.AddHandler(verboseName, new CommandInfo(OnVerbose)
            {
                HelpMessage = "Toggles Penny Helper verbose setting (default: true)."
            });

            this.pi.Framework.Network.OnNetworkMessage += OnNetworkEvent;
        }

        private void PrintSetting(string name, bool setting)
        {
            this.pi.Framework.Gui.Chat.Print(name + (setting ? " enabled." : " disabled."));
        }

        public void Dispose()
        {
            this.pi.Framework.Network.OnNetworkMessage -= OnNetworkEvent;
            this.pi.CommandManager.RemoveHandler(toggleName);
            this.pi.CommandManager.RemoveHandler(alwaysOnName);
            this.pi.CommandManager.RemoveHandler(deltaName);
            this.pi.CommandManager.RemoveHandler(verboseName);
            this.pi.Dispose();
        }

        private void OnToggle(string command, string args)
        {
            this.enabled = !this.enabled;
            PrintSetting("Penny Helper", this.enabled);
            if (this.configuration.alwaysOn) this.pi.Framework.Gui.Chat.Print("(this setting is ignored since always on is set to true)");
        }

        private void OnAlwaysOn(string command, string args)
        {
            this.configuration.alwaysOn = !this.configuration.alwaysOn;
            this.configuration.Save();
            PrintSetting("Penny Helper always on", this.configuration.alwaysOn);
        }

        private void OnDelta(string command, string args)
        {
            try
            {
                this.configuration.delta = int.Parse(args);
                this.configuration.Save();
                this.pi.Framework.Gui.Chat.Print($"Penny Helper delta set to {this.configuration.delta}.");
            }
            catch (FormatException)
            {
                this.pi.Framework.Gui.Chat.Print($"Unable to read '{args}' as an integer.");
            }
            catch (OverflowException)
            {
                this.pi.Framework.Gui.Chat.Print($"'{args}' is out of range.");
            }
        }

        private void OnVerbose (string command, string args)
        {
            this.configuration.verbose = !this.configuration.verbose;
            this.configuration.Save();
            PrintSetting("Penny Helper verbose", this.configuration.verbose);
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (!this.enabled && !this.configuration.alwaysOn) return;
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!this.pi.Data.IsDataReady) return;
            if (opCode != this.pi.Data.ServerOpCodes["MarketBoardOfferings"]) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);
            var catalogId = listing.ItemListings[0].CatalogId;
            var price = (uint) (listing.ItemListings[0].PricePerUnit - this.configuration.delta);
            if (this.lastItem == catalogId && this.lastPrice < price) return;
            this.lastItem = catalogId;
            this.lastPrice = price;
            Clipboard.SetText(price.ToString());
            if (this.configuration.verbose) this.pi.Framework.Gui.Chat.Print($"{price} copied to clipboard.");
        }
    }
}
