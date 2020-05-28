using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TMDbBoxSets.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public int MinimumNumberOfMovies { get; set; }

        public PluginConfiguration()
        {
            MinimumNumberOfMovies = 2;
        }
    }
}
