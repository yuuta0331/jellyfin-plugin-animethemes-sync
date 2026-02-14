namespace Jellyfin.Plugin.AnimeThemesSync.Configuration
{
    /// <summary>
    /// Configuration for a specific media type (Series/Movie).
    /// </summary>
    public class MediaTypeConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaTypeConfig"/> class.
        /// </summary>
        public MediaTypeConfig()
        {
            Audio = new ThemeConfig();
            Video = new ThemeConfig();
        }

        /// <summary>
        /// Gets or sets the audio theme configuration.
        /// </summary>
        public ThemeConfig Audio { get; set; }

        /// <summary>
        /// Gets or sets the video theme configuration.
        /// </summary>
        public ThemeConfig Video { get; set; }
    }
}
