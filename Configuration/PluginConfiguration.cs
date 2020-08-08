using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MergeVersions.Configuration
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
