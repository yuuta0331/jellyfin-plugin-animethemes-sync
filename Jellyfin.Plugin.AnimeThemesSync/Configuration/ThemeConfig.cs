using System;

namespace Jellyfin.Plugin.AnimeThemesSync.Configuration
{
    /// <summary>
    /// Configuration for a specific theme type (Audio/Video).
    /// </summary>
    public class ThemeConfig
    {
        /// <summary>
        /// Gets or sets the max number of themes to download. 0 to disable.
        /// </summary>
        public int MaxThemes { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore overlapping themes.
        /// </summary>
        public bool IgnoreOverlaps { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore themes with credits.
        /// </summary>
        public bool IgnoreCredits { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore OP themes.
        /// </summary>
        public bool IgnoreOp { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore ED themes.
        /// </summary>
        public bool IgnoreEd { get; set; } = true;

        /// <summary>
        /// Gets or sets the volume (0-100).
        /// </summary>
        public int Volume { get; set; } = 100;
    }
}
