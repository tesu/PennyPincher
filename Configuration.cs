namespace PennyPincher
{
    using System;
    using Dalamud.Configuration;
    using Dalamud.Plugin;

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        /// <inheritdoc/>
        public int Version { get; set; } = 1;

        public bool alwaysOn { get; set; } = false;

        public int delta { get; set; } = 1;

        public bool hq { get; set; } = false;

        public int min { get; set; } = 1;

        public int mod { get; set; } = 1;

        public bool smart { get; set; } = true;

        public bool verbose { get; set; } = true;

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
