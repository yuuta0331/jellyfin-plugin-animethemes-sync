
namespace AnimeThemesSync.Shared.Configuration
{
    /// <summary>
    /// Configuration for a specific theme type (Audio/Video).
    /// </summary>
    public class ThemeConfig
    {
        private int _maxThemes = 1;
        private int _volume = 100;

        /// <summary>
        /// Gets or sets the max number of themes to download. 0 to disable.
        /// </summary>
        public int MaxThemes
        {
            get => _maxThemes;
            set => _maxThemes = value < 0 ? 0 : value;
        }

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
        public int Volume
        {
            get => _volume;
            set => _volume = value < 0 ? 0 : value > 100 ? 100 : value;
        }
    }
}
