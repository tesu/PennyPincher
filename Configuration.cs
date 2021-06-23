using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace PennyPincher
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public bool alwaysOn { get; set; } = false;
        public int delta { get; set; } = 1;
        public bool hq { get; set; } = true;
        public int mod { get; set; } = 1;
        public bool smart { get; set; } = true;
        public bool verbose { get; set; } = true;

        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
