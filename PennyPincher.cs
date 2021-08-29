namespace PennyPincher
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Dalamud.Data;
    using Dalamud.Game;
    using Dalamud.Game.Command;
    using Dalamud.Game.Internal;
    using Dalamud.Game.Internal.Network;
    using Dalamud.Game.Network.Structures;
    using Dalamud.Plugin;
    using ImGuiNET;
    using Lumina.Excel.GeneratedSheets;

    public class PennyPincher
    {
        private const string commandName = "/penny";
        private const string helpName = "help";
        private const string deltaName = "delta";
        private const string hqName = "hq";
        private const string modName = "mod";
        private const string smartName = "smart";
        private const string verboseName = "verbose";

        private DalamudPluginInterface pi;
        private CommandManager c;
        private DataManager d;
        private Framework f;
        private Configuration configuration;
        private Lumina.Excel.ExcelSheet<Item> items;
        private bool newRequest;
        private GetFilePointer getFilePtr;

        public PennyPincher(DalamudPluginInterface pluginInterface, CommandManager commands, DataManager data, Framework framework, SigScanner sigScanner)
        {
            this.pi = pluginInterface;
            this.c = commands;
            this.d = data;
            this.f = framework;

            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);
            this.items = this.d.GetExcelSheet<Item>();
            this.newRequest = false;

            this.c.AddHandler(commandName, new CommandInfo(this.OnCommand)
            {
                HelpMessage = $"Toggles {Name}. {commandName} {helpName} for additional options",
            });

            this.f.Network.OnNetworkMessage += this.OnNetworkEvent;

            try
            {
                var ptr = sigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 14 83 7B 44 00");
                this.getFilePtr = Marshal.GetDelegateForFunctionPointer<GetFilePointer>(ptr);
            }
            catch (Exception e)
            {
                this.getFilePtr = null;
                PluginLog.LogError(e.ToString());
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetFilePointer(byte index);

        public static string Name => "Penny Pincher";

        public void Dispose()
        {
            this.f.Network.OnNetworkMessage -= this.OnNetworkEvent;
            this.c.RemoveHandler(commandName);
            this.pi.Dispose();
        }

        private void PrintSetting(string name, bool setting)
        {
            this.f.Gui.Chat.Print(name + (setting ? " enabled." : " disabled."));
        }

        private void OnCommand(string command, string args)
        {
            if (args == string.Empty)
            {
                this.configuration.alwaysOn = !this.configuration.alwaysOn;
                this.configuration.Save();
                this.PrintSetting($"{Name}", this.configuration.alwaysOn);
                return;
            }

            var argArray = args.Split(' ');
            switch (argArray[0])
            {
                case helpName:
                    this.f.Gui.Chat.Print($"{commandName}: Toggles whether {Name} is always on (supersedes {smartName})");
                    this.f.Gui.Chat.Print($"{commandName} {deltaName} <delta>: Sets the undercutting amount to be <delta>");
                    this.f.Gui.Chat.Print($"{commandName} {hqName}: Toggles whether {Name} should only undercut HQ items when you're listing an HQ item");
                    this.f.Gui.Chat.Print($"{commandName} {modName} <mod>: Adjusts base price by subtracting <price> % <mod> from <price> before subtracting <delta>. This makes the last digits of your posted prices consistent.");
                    this.f.Gui.Chat.Print($"{commandName} {smartName}: Toggles whether {Name} should automatically copy when you're using a retainer");
                    this.f.Gui.Chat.Print($"{commandName} {verboseName}: Toggles whether {Name} prints whenever it copies to clipboard");
                    this.f.Gui.Chat.Print($"{commandName} {helpName}: Displays this help page");
                    return;
                case "alwayson":
                    this.configuration.alwaysOn = !this.configuration.alwaysOn;
                    this.configuration.Save();
                    this.PrintSetting($"{Name}", this.configuration.alwaysOn);
                    this.f.Gui.Chat.Print($"Note that \"{commandName} alwayson\" has been renamed to \"{commandName}\".");
                    return;
                case modName:
                    if (argArray.Length < 2)
                    {
                        this.f.Gui.Chat.Print($"{commandName} {modName} missing <mod> argument.");
                        return;
                    }

                    var a = argArray[1];
                    try
                    {
                        this.configuration.mod = int.Parse(a);
                        this.configuration.Save();
                        this.f.Gui.Chat.Print($"{Name} {modName} set to {this.configuration.mod}.");
                    }
                    catch (FormatException)
                    {
                        this.f.Gui.Chat.Print($"Unable to read '{a}' as an integer.");
                    }
                    catch (OverflowException)
                    {
                        this.f.Gui.Chat.Print($"'{a}' is out of range.");
                    }

                    return;
                case deltaName:
                    if (argArray.Length < 2)
                    {
                        this.f.Gui.Chat.Print($"{commandName} {deltaName} <delta> is missing its <delta> argument.");
                        return;
                    }

                    var arg = argArray[1];
                    try
                    {
                        this.configuration.delta = int.Parse(arg);
                        this.configuration.Save();
                        this.f.Gui.Chat.Print($"{Name} {deltaName} set to {this.configuration.delta}.");
                    }
                    catch (FormatException)
                    {
                        this.f.Gui.Chat.Print($"Unable to read '{arg}' as an integer.");
                    }
                    catch (OverflowException)
                    {
                        this.f.Gui.Chat.Print($"'{arg}' is out of range.");
                    }

                    return;
                case hqName:
                    this.configuration.hq = !this.configuration.hq;
                    this.configuration.Save();
                    this.PrintSetting($"{Name} {hqName}", this.configuration.hq);
                    return;
                case smartName:
                    this.configuration.smart = !this.configuration.smart;
                    this.configuration.Save();
                    this.PrintSetting($"{Name} {smartName}", this.configuration.smart);
                    return;
                case verboseName:
                    this.configuration.verbose = !this.configuration.verbose;
                    this.configuration.Save();
                    this.PrintSetting($"{Name} {verboseName}", this.configuration.verbose);
                    return;
                default:
                    this.f.Gui.Chat.Print($"Unknown subcommand used. Run {commandName} {helpName} for valid subcommands.");
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
            if (!this.d.IsDataReady) return;
            if (opCode == this.d.ServerOpCodes["MarketBoardItemRequestStart"]) this.newRequest = true;
            if (opCode != this.d.ServerOpCodes["MarketBoardOfferings"] || !this.newRequest) return;
            if (!this.configuration.alwaysOn && (!this.configuration.smart || !this.Retainer())) return;
            var listing = MarketBoardCurrentOfferings.Read(dataPtr);
            var i = 0;
            if (this.configuration.hq && this.items.Single(j => j.RowId == listing.ItemListings[0].CatalogId).CanBeHq)
            {
                while (i < listing.ItemListings.Count && !listing.ItemListings[i].IsHq) i++;
                if (i == listing.ItemListings.Count) return;
            }

            var price = listing.ItemListings[i].PricePerUnit - (listing.ItemListings[i].PricePerUnit % this.configuration.mod) - this.configuration.delta;
            ImGui.SetClipboardText(price.ToString());
            if (this.configuration.verbose)
            {
                var hqPrefix = this.configuration.hq ? "[HQ] " : string.Empty;
                this.pi.Framework.Gui.Chat.Print(hqPrefix + $"{price} copied to clipboard.");
            }

            this.newRequest = false;
        }
    }
}