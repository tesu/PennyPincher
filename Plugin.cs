using Dalamud.Game.Command;
using Dalamud.Game.Internal.Network;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PennyPincher
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Penny Pincher";

        private const string commandName = "/penny";
        private const string helpName = "help";
        private const string deltaName = "delta";
        private const string hqName = "hq";
        private const string modName = "mod";
        private const string smartName = "smart";
        private const string verboseName = "verbose";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private bool newRequest;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetFilePointer(byte index);
        private GetFilePointer getFilePtr;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;

            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);

            this.newRequest = false;

            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = $"Toggles {Name}. {commandName} {helpName} for additional options"
            });

            this.pi.Framework.Network.OnNetworkMessage += OnNetworkEvent;

            try
            {
                var ptr = this.pi.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 14 83 7B 44 00");
                this.getFilePtr = Marshal.GetDelegateForFunctionPointer<GetFilePointer>(ptr);
            } catch (Exception e)
            {
                this.getFilePtr = null;
                PluginLog.LogError(e.ToString());
            }
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
                this.configuration.alwaysOn = !this.configuration.alwaysOn;
                this.configuration.Save();
                PrintSetting($"{Name}", this.configuration.alwaysOn);
                return;
            }
            var argArray = args.Split(' ');
            switch (argArray[0])
            {
                case helpName:
                    this.pi.Framework.Gui.Chat.Print($"{commandName}: Toggles whether {Name} is always on (supersedes {smartName})");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {deltaName} <delta>: Sets the undercutting amount to be <delta>");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {hqName}: Toggles whether {Name} should only undercut HQ items when you're listing an HQ item");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {modName} <mod>: Adjusts base price by subtracting <price> % <mod> from <price> before subtracting <delta>. This makes the last digits of your posted prices consistent.");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {smartName}: Toggles whether {Name} should automatically copy when you're using a retainer");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {verboseName}: Toggles whether {Name} prints whenever it copies to clipboard");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {helpName}: Displays this help page");
                    return;
                case "alwayson":
                    this.configuration.alwaysOn = !this.configuration.alwaysOn;
                    this.configuration.Save();
                    PrintSetting($"{Name}", this.configuration.alwaysOn);
                    this.pi.Framework.Gui.Chat.Print($"Note that \"{commandName} alwayson\" has been renamed to \"{commandName}\".");
                    return;
                case modName:
                    if (argArray.Length < 2)
                    {
                        this.pi.Framework.Gui.Chat.Print($"{commandName} {modName} missing <mod> argument.");
                        return;
                    }
                    var a = argArray[1];
                    try
                    {
                        this.configuration.mod = int.Parse(a);
                        this.configuration.Save();
                        this.pi.Framework.Gui.Chat.Print($"{Name} {modName} set to {this.configuration.mod}.");
                    }
                    catch (FormatException)
                    {
                        this.pi.Framework.Gui.Chat.Print($"Unable to read '{a}' as an integer.");
                    }
                    catch (OverflowException)
                    {
                        this.pi.Framework.Gui.Chat.Print($"'{a}' is out of range.");
                    }
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
                case hqName:
                    this.configuration.hq = !this.configuration.hq;
                    this.configuration.Save();
                    PrintSetting($"{Name} {hqName}", this.configuration.hq);
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

        private bool Retainer()
        {
            return (this.getFilePtr == null) ? false : Marshal.ReadInt64(this.getFilePtr(7), 0xB0) != 0;
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!this.pi.Data.IsDataReady) return;
            if (opCode == this.pi.Data.ServerOpCodes["MarketBoardItemRequestStart"]) this.newRequest = true;
            if (opCode != this.pi.Data.ServerOpCodes["MarketBoardOfferings"] || !this.newRequest) return;
            if (!this.configuration.alwaysOn && (!this.configuration.smart || !Retainer())) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);
            var i = 0;
            bool isCurrentItemHQ = false;

            if (this.configuration.hq && Retainer())
            {
                // Load addon ptr from memory and get the name of the item being listed
                var toolTipPtr = pi.Framework.Gui.GetUiObjectByName("ItemDetail", 1) + 0x258;

                // Checks for HQ symbol in item name
                isCurrentItemHQ = GetSeStringText(GetSeString(toolTipPtr)).Contains("");

                if (isCurrentItemHQ)
                {
                    while (i < listing.ItemListings.Count && !listing.ItemListings[i].IsHq) i++;
                    if (i == listing.ItemListings.Count) return;
                }
            }

            var price = listing.ItemListings[i].PricePerUnit - (listing.ItemListings[i].PricePerUnit % this.configuration.mod) - this.configuration.delta;
            Clipboard.SetText(price.ToString());
            if (this.configuration.verbose) {
                var hqPrefix = isCurrentItemHQ ? "[HQ] " : "";
                this.pi.Framework.Gui.Chat.Print(hqPrefix + $"{price} copied to clipboard.");
            }
            this.newRequest = false;
        }

        /// <summary>
        /// Text from addon to SeString
        /// </summary>
        /// <param name="textPtr">addon ptr</param>
        /// <returns></returns>
        private SeString GetSeString(IntPtr textPtr) {
            var size = 0;
            while (Marshal.ReadByte(textPtr, size) != 0)
                size++;

            var bytes = new byte[size];
            Marshal.Copy(textPtr, bytes, 0, size);

            return GetSeString(bytes);
        }

        /// <summary>
        /// SeString to string
        /// </summary>
        /// <param name="sestring"></param>
        /// <returns></returns>
        private string GetSeStringText(SeString sestring) {
            var pieces = sestring.Payloads.OfType<TextPayload>().Select(t => t.Text);
            var text = string.Join("", pieces).Replace('\n', ' ').Trim();
            return text;
        }

        /// <summary>
        /// Parse bytes to SeString
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private SeString GetSeString(byte[] bytes) {
            return pi.SeStringManager.Parse(bytes);
        }
    }
}