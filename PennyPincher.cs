namespace PennyPincher
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Dalamud.Data;
    using Dalamud.Game;
    using Dalamud.Game.Command;
    using Dalamud.Game.Gui;
    using Dalamud.Game.Network;
    using Dalamud.Game.Network.Structures;
    using Dalamud.Logging;
    using Dalamud.Plugin;
    using ImGuiNET;
    using Lumina.Excel.GeneratedSheets;

    public class PennyPincher : IDalamudPlugin
    {
        private const string commandName = "/penny";
        private const string helpName = "help";
        private const string deltaName = "delta";
        private const string hqName = "hq";
        private const string minName = "min";
        private const string modName = "mod";
        private const string smartName = "smart";
        private const string verboseName = "verbose";

        private DalamudPluginInterface pi;
        private ChatGui chat;
        private CommandManager c;
        private DataManager d;
        private GameNetwork gn;
        private Configuration configuration;
        private Lumina.Excel.ExcelSheet<Item> items;
        private bool newRequest;
        private GetFilePointer getFilePtr;

        public PennyPincher(DalamudPluginInterface pluginInterface, ChatGui chat, CommandManager commands, DataManager data, GameNetwork gameNetwork, SigScanner sigScanner)
        {
            this.pi = pluginInterface;
            this.chat = chat;
            this.c = commands;
            this.d = data;
            this.gn = gameNetwork;

            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);
            this.items = this.d.GetExcelSheet<Item>();
            this.newRequest = false;

            this.c.AddHandler(commandName, new CommandInfo(this.OnCommand)
            {
                HelpMessage = $"Toggles {this.Name}. {commandName} {helpName} for additional options",
            });

            this.gn.NetworkMessage += this.OnNetworkEvent;

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

        public string Name => "Penny Pincher";

        public void Dispose()
        {
            this.gn.NetworkMessage -= this.OnNetworkEvent;
            this.c.RemoveHandler(commandName);
            this.pi.Dispose();
        }

        private void PrintSetting(string name, bool setting)
        {
            this.chat.Print(name + (setting ? " enabled." : " disabled."));
        }

        private void OnCommand(string command, string args)
        {
            if (args == string.Empty)
            {
                this.configuration.alwaysOn = !this.configuration.alwaysOn;
                this.configuration.Save();
                this.PrintSetting($"{this.Name}", this.configuration.alwaysOn);
                return;
            }

            var argArray = args.Split(' ');
            switch (argArray[0])
            {
                case helpName:
                    this.chat.Print($"{commandName}: Toggles whether {this.Name} is always on (supersedes {smartName})");
                    this.chat.Print($"{commandName} {deltaName} <delta>: Sets the undercutting amount to be <delta>");
                    this.chat.Print($"{commandName} {hqName}: Toggles whether {this.Name} should only undercut HQ items when you're listing an HQ item");
                    this.chat.Print($"{commandName} {minName} <min>: Sets a minimum value to be copied. <min> cannot be below 1.");
                    this.chat.Print($"{commandName} {modName} <mod>: Adjusts base price by subtracting <price> % <mod> from <price> before subtracting <delta>. This makes the last digits of your posted prices consistent.");
                    this.chat.Print($"{commandName} {smartName}: Toggles whether {this.Name} should automatically copy when you're using a retainer");
                    this.chat.Print($"{commandName} {verboseName}: Toggles whether {this.Name} prints whenever it copies to clipboard");
                    this.chat.Print($"{commandName} {helpName}: Displays this help page");
                    return;
                case "alwayson":
                    this.configuration.alwaysOn = !this.configuration.alwaysOn;
                    this.configuration.Save();
                    this.PrintSetting($"{this.Name}", this.configuration.alwaysOn);
                    return;
                case minName:
                    if (argArray.Length < 2)
                    {
                        this.chat.Print($"{commandName} {minName} missing <min> argument.");
                        return;
                    }

                    var minArg = argArray[1];
                    try
                    {
                        var minArgVal = int.Parse(minArg);
                        if (minArgVal < 1)
                        {
                            this.chat.Print($"{commandName} {minName} <min> cannot be lower than 1.");
                            return;
                        }

                        this.configuration.min = minArgVal;
                        this.configuration.Save();
                        this.chat.Print($"{this.Name} {minName} set to {this.configuration.min}.");
                    }
                    catch (FormatException)
                    {
                        this.chat.Print($"Unable to read '{minArg}' as an integer.");
                    }
                    catch (OverflowException)
                    {
                        this.chat.Print($"'{minArg}' is out of range.");
                    }

                    return;
                case modName:
                    if (argArray.Length < 2)
                    {
                        this.chat.Print($"{commandName} {modName} missing <mod> argument.");
                        return;
                    }

                    var modArg = argArray[1];
                    try
                    {
                        this.configuration.mod = int.Parse(modArg);
                        this.configuration.Save();
                        this.chat.Print($"{this.Name} {modName} set to {this.configuration.mod}.");
                    }
                    catch (FormatException)
                    {
                        this.chat.Print($"Unable to read '{modArg}' as an integer.");
                    }
                    catch (OverflowException)
                    {
                        this.chat.Print($"'{modArg}' is out of range.");
                    }

                    return;
                case deltaName:
                    if (argArray.Length < 2)
                    {
                        this.chat.Print($"{commandName} {deltaName} <delta> is missing its <delta> argument.");
                        return;
                    }

                    var arg = argArray[1];
                    try
                    {
                        this.configuration.delta = int.Parse(arg);
                        this.configuration.Save();
                        this.chat.Print($"{this.Name} {deltaName} set to {this.configuration.delta}.");
                    }
                    catch (FormatException)
                    {
                        this.chat.Print($"Unable to read '{arg}' as an integer.");
                    }
                    catch (OverflowException)
                    {
                        this.chat.Print($"'{arg}' is out of range.");
                    }

                    return;
                case hqName:
                    this.configuration.hq = !this.configuration.hq;
                    this.configuration.Save();
                    this.PrintSetting($"{this.Name} {hqName}", this.configuration.hq);
                    return;
                case smartName:
                    this.configuration.smart = !this.configuration.smart;
                    this.configuration.Save();
                    this.PrintSetting($"{this.Name} {smartName}", this.configuration.smart);
                    return;
                case verboseName:
                    this.configuration.verbose = !this.configuration.verbose;
                    this.configuration.Save();
                    this.PrintSetting($"{this.Name} {verboseName}", this.configuration.verbose);
                    return;
                default:
                    this.chat.Print($"Unknown subcommand used. Run {commandName} {helpName} for valid subcommands.");
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
            price = Math.Max(price, this.configuration.min);
            ImGui.SetClipboardText(price.ToString());
            if (this.configuration.verbose)
            {
                this.chat.Print((this.configuration.hq ? "[HQ] " : string.Empty) + $"{price:n0} copied to clipboard.");
            }

            this.newRequest = false;
        }
    }
}