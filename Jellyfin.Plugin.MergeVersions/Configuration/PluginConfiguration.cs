using MediaBrowser.Model.Plugins;
using System;

namespace Jellyfin.Plugin.MergeVersions.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {

        public String[] LocationsExcluded { get; set; }

        // Folders whose version becomes the default of a merged item, highest priority first.
        public String[] PreferredLocations { get; set; }

        public PluginConfiguration()
        {
            LocationsExcluded = Array.Empty<String>();
            PreferredLocations = Array.Empty<String>();
        }
    }
}
