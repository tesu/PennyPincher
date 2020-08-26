using Dalamud.Game.Command;
using Dalamud.Game.Internal.Network;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PennyPincher
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Penny Pincher";

        private const string commandName = "/penny";
        private const string helpName = "help";
        private const string alwaysOnName = "alwayson";
        private const string deltaName = "delta";
        private const string hqName = "hq";
        private const string smartName = "smart";
        private const string verboseName = "verbose";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private bool enabled;
        private bool newRequest;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetFilePointer(byte index);
        private GetFilePointer getFilePtr;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;

            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);

            this.enabled = false;
            this.newRequest = false;

            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = $"Toggles {Name} mode. {commandName} {helpName} for additional options"
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
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {hqName}: Toggles whether to undercut from HQ items only, or from both NQ and HQ");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {smartName}: Toggles whether {Name} should automatically toggle when you access a retainer");
                    this.pi.Framework.Gui.Chat.Print($"{commandName} {verboseName}: Toggles whether {Name} prints whenever it copies to clipboard");
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
            if (!this.enabled && !this.configuration.alwaysOn && (!this.configuration.smart || !Retainer())) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);
            var i = 0;
            while (this.configuration.hq && i < listing.ItemListings.Count && !listing.ItemListings[i].IsHq) {
                i++;
            }
            if (i == listing.ItemListings.Count) return; // didn't find HQ item
            var price = listing.ItemListings[i].PricePerUnit - this.configuration.delta;
            Clipboard.SetText(price.ToString());
            if (this.configuration.verbose) this.pi.Framework.Gui.Chat.Print($"{price} copied to clipboard.");
            this.newRequest = false;
        }
    }
}
