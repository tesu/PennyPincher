using System;
using Dalamud.Configuration;

namespace PennyPincher
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        /// <inheritdoc/>
        public int Version { get; set; } = 2;

        public bool alwaysOn { get; set; } = false;

        public int delta { get; set; } = 1;

        public bool hq { get; set; } = false;

        public int min { get; set; } = 1;

        public int mod { get; set; } = 1;

        public int multiple { get; set; } = 1;

        public bool undercutSelf { get; set; } = false;

        public bool verbose { get; set; } = true;
    }

    [Serializable]
    public class OldConfiguration : IPluginConfiguration
    {
        /// <inheritdoc/>
        public int Version { get; set; } = 1;

        public bool alwaysOn { get; set; } = false;

        public bool alwaysHq { get; set; } = false;

        public int delta { get; set; } = 1;

        public bool hq { get; set; } = true;

        public int min { get; set; } = 1;

        public int mod { get; set; } = 1;

        public int multiple { get; set; } = 1;

        public bool smart { get; set; } = true;

        public bool verbose { get; set; } = true;
    }
}
